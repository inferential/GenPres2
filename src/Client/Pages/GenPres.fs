namespace Pages


open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


open Elmish
open Shared


module GenPres =


    module private Elmish =


        open Global


        type State =
            {
                SideMenuItems: (JSX.Element option * string * bool) []
                SideMenuIsOpen: bool
                Configuration: Configuration Option
                ShowDisclaimer: bool
            }


        type Msg =
            | SideMenuClick of string
            | ToggleMenu
            | AcceptDisclaimer


        let pages =
            [
                LifeSupport
                ContinuousMeds
                Prescribe
                Formulary
                Parenteralia
            ]


        let init lang terms page : State * Cmd<Msg> =

            let state =
                {
                    SideMenuItems =
                        pages
                        |> List.toArray
                        |> Array.map (fun p ->
                            let b = p = page
                            match p |> pageToString terms lang with
                            | s when p = LifeSupport -> (Mui.Icons.FireExtinguisher |> Some), s, b
                            | s when p = ContinuousMeds -> (Mui.Icons.Vaccines |> Some), s, b
                            | s when p = Prescribe -> (Mui.Icons.Message |> Some), s, b
                            | s when p = Formulary -> (Mui.Icons.LocalPharmacy |> Some), s, b
                            | s when p = Parenteralia -> (Mui.Icons.Bloodtype |> Some), s, b
                            | s -> None, s, b
                        )

                    SideMenuIsOpen = false
                    Configuration = None
                    ShowDisclaimer = true
                }

            state, Cmd.none


        let update lang terms updatePage (msg: Msg) (state: State) =
            match msg with
            | AcceptDisclaimer ->
                { state with
                    ShowDisclaimer = false
                },
                Cmd.none

            | ToggleMenu ->
                { state with
                    SideMenuIsOpen = not state.SideMenuIsOpen
                },
                Cmd.none

            | SideMenuClick s ->

                pages
                |> List.map (fun p -> p |> pageToString terms lang, p)
                |> List.tryFind (fst >> ((=) s))
                |> Option.map snd
                |> Option.defaultValue LifeSupport
                |> updatePage

                { state with

                    SideMenuItems =
                        state.SideMenuItems
                        |> Array.map (fun (icon, item, _) ->
                            if item = s then
                                printfn $"{s} true"
                                (icon, item, true)
                            else
                                printfn $"{s} false"
                                (icon, item, false)
                        )
                },
                Cmd.none


    open Elmish



    [<JSX.Component>]
    let View
        (props: {|
            patient: Patient option
            updatePatient: Patient option -> unit
            updatePage: Global.Pages -> unit
            bolusMedication: Deferred<Intervention list>
            continuousMedication: Deferred<Intervention list>
            products: Deferred<Product list>
            scenario: Deferred<ScenarioResult>
            updateScenario : ScenarioResult -> unit
            selectOrder : (Scenario * Order option) -> unit
            order : Deferred<Order option>
            loadOrder : Order -> unit
            updateScenarioOrder : unit -> unit
            formulary: Deferred<Formulary>
            updateFormulary : Formulary -> unit
            page : Global.Pages
            localizationTerms : Deferred<string[][]>
            languages : Localization.Locales []
            switchLang : Localization.Locales -> unit |}) =

        let lang = React.useContext(Global.languageContext)
        let deps = [| box props.page; box props.updatePage; box lang |]
        let state, dispatch = React.useElmish (init lang props.localizationTerms props.page, update lang props.localizationTerms props.updatePage, deps)

        let notFound =
            JSX.jsx
                $"""
            <React.Fragment>
                <Typography>
                    Nog niet geimplementeerd
                </Typography>
            </React.Fragment>
            """
        let modalStyle =
            {|
                position="absolute"
                top= "50%"
                left= "50%"
                transform= "translate(-50%, -50%)"
                width= 400
                bgcolor= "background.paper"
                boxShadow= 24
            |}

        JSX.jsx
            $"""
        import {{ ThemeProvider }} from '@mui/material/styles';
        import CssBaseline from '@mui/material/CssBaseline';
        import React from "react";
        import Stack from '@mui/material/Stack';
        import Box from '@mui/material/Box';
        import Container from '@mui/material/Container';
        import Typography from '@mui/material/Typography';
        import Modal from '@mui/material/Modal';

        <React.Fragment>
            <Box>
                {Components.TitleBar.View({|
                    title = $"GenPRES 2023 {props.page |> (Global.pageToString props.localizationTerms lang)}"
                    toggleSideMenu = fun _ -> ToggleMenu |> dispatch
                    languages = props.languages
                    switchLang = props.switchLang
                |})}
            </Box>
            <React.Fragment>
                {
                    Components.SideMenu.View({|
                        anchor = "left"
                        isOpen = state.SideMenuIsOpen
                        toggle = (fun _ -> ToggleMenu |> dispatch)
                        menuClick = SideMenuClick >> dispatch
                        items =  state.SideMenuItems
                    |})
                }
            </React.Fragment>
            <Container sx={ {| height="87%"; mt= 4 |} } >
                <Stack sx={ {| height="100%" |} }>
                    <Box sx={ {| flexBasis=1 |} } >
                        {
                            Views.Patient.View({|
                                patient = props.patient
                                updatePatient = props.updatePatient
                                localizationTerms = props.localizationTerms
                            |})
                        }
                    </Box>
                    <Box sx={ {| maxHeight = "80%"; mt=4; overflowY="auto" |} }>
                        {
                            match props.page with
                            | Global.Pages.LifeSupport ->
                                Views.EmergencyList.View ({|
                                    interventions = props.bolusMedication
                                    localizationTerms = props.localizationTerms
                                |})
                            | Global.Pages.ContinuousMeds ->
                                Views.ContinuousMeds.View ({|
                                    interventions = props.continuousMedication
                                    localizationTerms = props.localizationTerms
                                |})
                            | Global.Pages.Prescribe ->
                                Views.Prescribe.View ({|
                                    order = props.order
                                    scenarios = props.scenario
                                    updateScenario = props.updateScenario
                                    selectOrder = props.selectOrder
                                    loadOrder = props.loadOrder
                                    updateScenarioOrder = props.updateScenarioOrder
                                    localizationTerms = props.localizationTerms
                                |})
                            | Global.Pages.Formulary ->
                                Views.Formulary.View ({|
                                    order = props.formulary
                                    updateFormulary = props.updateFormulary
                                    localizationTerms = props.localizationTerms
                                |})
                            | _ -> notFound
                        }
                    </Box>
                </Stack>
            </Container>
            <Modal open={state.ShowDisclaimer} onClose={fun () -> ()} >
                <Box sx={modalStyle}>
                    {
                        Views.Disclaimer.View {|
                            accept = fun _ -> AcceptDisclaimer |> dispatch
                            languages = props.languages
                            switchLang = props.switchLang
                            localizationTerms = props.localizationTerms
                        |}
                    }
                </Box>
            </Modal>

        </React.Fragment>
        """