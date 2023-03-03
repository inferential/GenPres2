module App

open Fable.Core
open Browser
open Fable.React


module private Elmish =

    open Elmish
    open Feliz
    open Feliz.Router
    open Fable.Remoting.Client
    open Shared
    open Types
    open Global
    open Utils
    open System


    type Model =
        {
            CurrentPage: Pages Option
            SideMenuItems: (string * bool) []
            SideMenuIsOpen: bool
            Configuration: Configuration Option
            Language: Localization.Locales
            Patient: Patient option
            BolusMedication: Deferred<BolusMedication list>
            ContinuousMedication: Deferred<ContinuousMedication list>
            Products: Deferred<Product list>
            Scenarios: Deferred<ScenarioResult>
        }


    type Msg =
        | SideMenuClick of string
        | ToggleMenu
        | LanguageChange of string
        | UpdateLanguage of Localization.Locales
        | UpdatePatient of Patient option
        | UrlChanged of string list
        | LoadBolusMedication of
            AsyncOperationStatus<Result<BolusMedication list, string>>
        | LoadContinuousMedication of
            AsyncOperationStatus<Result<ContinuousMedication list, string>>
        | LoadProducts of AsyncOperationStatus<Result<Product list, string>>
        | LoadScenarios of AsyncOperationStatus<Result<ScenarioResult, string>>
        | UpdateScenarios of ScenarioResult


    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder Api.routerPaths
        |> Remoting.buildProxy<Api.IServerApi>


    // url needs to be in format: http://localhost:8080/#pat?ay=2&am=0&ad=1
    // * by: birth year
    // * bm: birth month
    // * bd: birth day
    // * wt: weight (kg)
    // * ln: length (cm)
    let parseUrlToPatient sl =

        match sl with
        | [] -> None
        | [ "pat"
            Route.Query [
                "by", Route.Int by
                "bm", Route.Int bm
                "bd", Route.Int bd
                "wt", Route.Number wt ] ] ->
            Logging.log "query params:" $"by: {by}"

            let age =
                Patient.Age.fromBirthDate DateTime.Now (DateTime(by, bm, bd))

            Patient.create
                (Some age.Years)
                (Some age.Months)
                (Some age.Weeks)
                (Some age.Days)
                (wt |> Some)
                None
            |> fun p ->
                Logging.log "parsed: " p
                p
        | _ ->
            sl
            |> String.concat ""
            |> Logging.warning "could not parse url"

            None


    let pages =
        [
            Pages.LifeSupport
            Pages.ContinuousMeds
            Pages.Prescribe
        ]


    let init () : Model * Cmd<Msg> =

        let initialState =
            {
                Configuration = None
                Language = Localization.Dutch
                Patient =
                    Router.currentUrl ()
                    |> parseUrlToPatient
                BolusMedication = HasNotStartedYet
                ContinuousMedication = HasNotStartedYet
                Products = HasNotStartedYet
                Scenarios = HasNotStartedYet
                CurrentPage = Pages.LifeSupport |> Some
                SideMenuItems =
                    pages
                    |> List.toArray
                    |> Array.map (fun p ->
                        p
                        |> pageToString Localization.Dutch,
                        false
                    )
                SideMenuIsOpen = false
            }

        let cmds =
            Cmd.batch [
                Cmd.ofMsg (LoadBolusMedication Started)
                Cmd.ofMsg (LoadContinuousMedication Started)
                Cmd.ofMsg (LoadProducts Started)
                Cmd.ofMsg (LoadScenarios Started)
            ]

        initialState, cmds


    let update (msg: Msg) (state: Model) =
        match msg with
        | ToggleMenu ->
            { state with
                SideMenuIsOpen = not state.SideMenuIsOpen
            },
            Cmd.none

        | SideMenuClick s ->
            let pageToString = Global.pageToString Localization.Dutch

            { state with
                CurrentPage =
                    pages
                    |> List.map (fun p -> p |> pageToString, p)
                    |> List.tryFind (fst >> ((=) s))
                    |> Option.map snd

                SideMenuItems =
                    state.SideMenuItems
                    |> Array.map (fun (item, _) ->
                        if item = s then
                            printfn $"{s} true"
                            (item, true)
                        else
                            printfn $"{s} false"
                            (item, false)
                    )
            },
            Cmd.none


        | LanguageChange s ->
            //TODO: doesn't work anymore
            state, Cmd.none

        | UpdateLanguage l -> { state with Language = l }, Cmd.none

        | UpdatePatient p ->
            Logging.log "update patient app" p
            { state with Patient = p },
            Cmd.ofMsg (LoadScenarios Started)
        | UrlChanged sl ->
            Logging.log "url changed" sl

            { state with
                Patient = sl |> parseUrlToPatient
            },
            Cmd.none
        | LoadBolusMedication Started ->
            { state with
                BolusMedication = InProgress
            },
            Cmd.fromAsync (GoogleDocs.loadBolusMedication LoadBolusMedication)
        | LoadBolusMedication (Finished (Ok meds)) ->

            { state with
                BolusMedication = meds |> Resolved
            },
            Cmd.none
        | LoadBolusMedication (Finished (Error s)) ->
            Logging.error "cannot load emergency treatment" s
            state, Cmd.none
        | LoadContinuousMedication Started ->
            { state with
                ContinuousMedication = InProgress
            },
            Cmd.fromAsync (
                GoogleDocs.loadContinuousMedication LoadContinuousMedication
            )
        | LoadContinuousMedication (Finished (Ok meds)) ->

            { state with
                ContinuousMedication = meds |> Resolved
            },
            Cmd.none
        | LoadContinuousMedication (Finished (Error s)) ->
            Logging.error "cannot load continuous medication" s
            state, Cmd.none
        | LoadProducts Started ->
            { state with Products = InProgress },
            Cmd.fromAsync (GoogleDocs.loadProducts LoadProducts)
        | LoadProducts (Finished (Ok prods)) ->

            { state with
                Products = prods |> Resolved
            },
            Cmd.none
        | LoadProducts (Finished (Error s)) ->
            Logging.error "cannot load products" s
            state, Cmd.none

        | LoadScenarios Started ->
            let scenarios =
                match state.Scenarios with
                | Resolved sc when state.Patient.IsSome ->
                    { sc with
                        Age =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getAgeInDays
                            | None -> sc.Age
                        Weight =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getWeight
                            | None -> sc.Weight
                        Height =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getHeight
                            | None -> sc.Height
                    }
                | _ -> ScenarioResult.empty

            let load =
                async {
                    let! result = serverApi.getScenarioResult scenarios
                    return Finished result |> LoadScenarios
                }

            { state with Scenarios = InProgress }, Cmd.fromAsync load

        | LoadScenarios (Finished (Ok result)) ->
            let text = result.Scenarios |> String.concat "\n"
            if result.Scenarios |> List.length> 0 then printfn $"scenarios: |{text}|"

            { state with
                Scenarios = Resolved result
            },
            Cmd.none

        | LoadScenarios (Finished (Error msg)) ->
            Logging.log "scenarios" msg
            state, Cmd.none

        | UpdateScenarios sc ->
            let sc =
                { sc with
                    Weight =
                        match state.Patient with
                        | Some pat -> pat |> Patient.getWeight
                        | None -> sc.Weight
                }

            { state with Scenarios = Resolved sc },
            Cmd.ofMsg (LoadScenarios Started)


    let calculatInterventions calc meds pat =
        meds
        |> Deferred.bind (fun xs ->
            match pat with
            | None -> InProgress
            | Some p ->
                let a = p |> Patient.getAgeInYears
                let w = p |> Patient.getWeight
                xs |> calc a w |> Resolved
        )

open Elmish
open Shared

// Entry point must be in a separate file
// for Vite Hot Reload to work

[<JSX.Component>]
let App () =
    let state, dispatch = React.useElmish (init, update, [||])

    let bm =
        calculatInterventions
            EmergencyTreatment.calculate
            state.BolusMedication
            state.Patient

    let cm =
        let calc =
            fun _ w meds ->
                match w with
                | Some w' -> ContinuousMedication.calculate w' meds
                | None -> []

        calculatInterventions calc state.ContinuousMedication state.Patient

    Main.GenPres({|
        updateLang = (UpdateLanguage >> dispatch)
        patient = state.Patient
        updatePatient = (UpdatePatient >> dispatch)
        bolusMedication = bm
        continuousMedication = cm
        products = state.Products
        scenarios = state.Scenarios
        updateScenarios = (UpdateScenarios >> dispatch)
        toggleMenu = (fun _ -> ToggleMenu |> dispatch)
        currentPage = state.CurrentPage
        sideMenuIsOpen = state.SideMenuIsOpen
        sideMenuClick = SideMenuClick >> dispatch
        sideMenuItems = state.SideMenuItems
    |})

let root = ReactDomClient.createRoot (document.getElementById ("genpres-app"))
root.render (App() |> toReact)
