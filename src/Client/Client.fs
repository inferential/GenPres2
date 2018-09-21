module Client

open Elmish
open Elmish.React

open Fable.Core
open Fable.Core.JsInterop

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch

open Fulma
open Component

open Shared

module Math = Utils.Math
module String = Utils.String
module Select = Component.Select
module Table = Component.Table


[<Emit("navigator.userAgent")>]
let userAgent : string = jsNative


[<Import("count", "./lib/gitCount.js")>]
let gitCount : int = jsNative


let version = sprintf "0.0.1.%i" gitCount


type MarkDown =
    abstract render : string -> obj


let md = 
    createNew (importDefault<MarkDown> "markdown-it") ()
    :?> MarkDown    


[<Pojo>]
type DangerousInnerHtml =
    { __html : string }


let htmlFromMarkdown str = 
    printfn "Markdown: %s" (str |> md.render |> string)
    div [ DangerouslySetInnerHTML { __html = md.render str |> string } ] []


// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model = 
    { 
        GenPres : GenPres option
        PatientModel : Patient.Model
        Page : Page
        Device : Device
        ShowMenu : NavbarMenu
        EmergencyModel : Emergency.Model
        CalculatorModel : Calculator.Model
    }
and NavbarMenu = { CalculatorMenu : bool; MainMenu : bool }
and Device = 
    {
        IsMobile : bool
        Width : float
        Height : float
    }
and Page =
    | CalculatorPage
    | EmergencyListPage
     

let createDevice () =
    let agent = userAgent |> String.toLower
    {
        IsMobile =
            [ "iphone"; "android"; "ipad"; "opera mini"; "windows mobile"; "windows phone"; "iemobile" ]
            |> List.exists (fun s -> agent |> String.contains s)
        Width = Fable.Import.Browser.screen.width
        Height = Fable.Import.Browser.screen.height        
    }

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
| PatientMsg of Patient.Msg
| EmergencyMsg of Emergency.Msg
| MenuMsg of MenuMsg
| ChangePage of Page
| CalculatorMsg of Calculator.Msg
| GenPresLoaded of Result<GenPres, exn>
and MenuMsg = CalculatorMenuMsg | MainMenuMsg


// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =

    printfn "User Agent = %s" userAgent
    
    let genpres = { Name = "GenPres OFFLINE"; Version = version }

    let device = createDevice ()

    let pat = Patient.init ()

    let showMenu = { CalculatorMenu = false; MainMenu = false }
    
    let initialModel = 
        { 
            GenPres = Some genpres
            Page = EmergencyListPage
            PatientModel = pat
            Device = device
            ShowMenu = showMenu
            EmergencyModel = Emergency.init device.IsMobile 
            CalculatorModel = Calculator.init pat
        }

    let loadCountCmd =
        Cmd.ofPromise
            ( fetchAs<GenPres> "http://localhost:8085/api/init" )
            []
            (Ok >> GenPresLoaded)
            (Error >> GenPresLoaded)

    initialModel, loadCountCmd


// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
    match msg with

    | PatientMsg msg ->
        let pat, cmd = Patient.update msg model.PatientModel
        { model with PatientModel = pat; CalculatorModel = Calculator.init pat }, Cmd.map PatientMsg cmd

    | EmergencyMsg msg ->
        { model with EmergencyModel = model.EmergencyModel |> Emergency.update msg }, Cmd.none
 
    | GenPresLoaded (Ok genpres) ->
        { model with GenPres = { genpres with Version = version } |> Some }, Cmd.none

    | CalculatorMsg msg ->
        { model with CalculatorModel = model.CalculatorModel |> Calculator.update msg  }, Cmd.none

    | ChangePage page ->
        { model with Page = page}, Cmd.none

    | GenPresLoaded (_) -> 
        let gp =
            match model.GenPres with 
            | Some gp' -> { gp' with Version = version } |> Some
            | None -> None
        printfn "GenPresLoaded %A" gp
        { model with GenPres = gp }, Cmd.none

    | MenuMsg msg ->
        match msg with
        | CalculatorMenuMsg -> 
            { model with ShowMenu =  { model.ShowMenu with CalculatorMenu = not model.ShowMenu.CalculatorMenu } }, Cmd.none
        | MainMenuMsg -> 
            { model with ShowMenu =  { model.ShowMenu with MainMenu = not model.ShowMenu.MainMenu } }, Cmd.none


let disclaimer =
    let txt = """
***Disclaimer***

Deze applicatie is nog in ontwikkeling en validatie van de inhoud heeft nog niet plaatsgevond. 
De gebruiker is zelf verantwoordelijk voor het gebruik van de getoonde informatie. Indien u fouten vindt
of suggesties hebt gaarne dit per [mail](mailto:c.w.bollen@umcutrecht.nl) vermelden. 

Verdere informatie kunt u vinden op de [PICU WKZ site](http://picuwkz.nl). De code voor deze webapplicatie
is te vinden op [Github](http://github.com/halcwb/GenPres2.git).
"""
    htmlFromMarkdown txt

let show model = 
    match model with
    | { GenPres = Some x } -> 
        div []
            [ Heading.h3 [ Heading.Option.CustomClass "has-text-white" ] [ x.Name |> str ]
              Heading.h6 [ Heading.Option.CustomClass "has-text-white" ] [ "versie " + x.Version |> str ]
            ]

    | { GenPres = None   } ->
        Heading.h3 [ Heading.Option.CustomClass "has-text-white" ] [ "Laden ..." |> str ]


let topView dispatch model =
    let openPEWS = fun _ -> CalculatorPage    |> ChangePage |> dispatch
    let openERL  = fun _ -> EmergencyListPage |> ChangePage |> dispatch

    let calcMenu isMobile (model : NavbarMenu) =
        if isMobile && not model.CalculatorMenu then []
        else
            [ Navbar.Item.a [ Navbar.Item.Props  [ OnClick openPEWS ] ] [ str "PEWS score" ] ]

    let mainMenu isMobile (model : NavbarMenu) =
        if isMobile && not model.MainMenu then []
        else
            [ Navbar.Item.a [ Navbar.Item.Props [ OnClick openERL ] ] [ str "Acute Opvang" ]
              Navbar.Item.a [] [ str "Medicatie Voorschrijven" ] ]    

    Navbar.navbar 
        [ Navbar.Color IsPrimary
          Navbar.Props [ Style [ CSSProp.Padding "10px" ] ]
          Navbar.HasShadow ]
        
        [ Navbar.Item.div [ ]
                [ show model ] 

          Navbar.End.div []
              [ Navbar.Item.div 
                    [ Navbar.Item.IsHoverable
                      Navbar.Item.HasDropdown ] 
                    [ Navbar.Item.a [ Navbar.Item.Props [OnClick (fun _ -> CalculatorMenuMsg |> MenuMsg |> dispatch )] ] 
                        [ Fulma.FontAwesome.Icon.faIcon 
                            [ Icon.Size IsSmall ] 
                            [ FontAwesome.Fa.icon FontAwesome.Fa.I.Calculator ] ]
                      Navbar.Dropdown.div [ Navbar.Dropdown.IsRight ] 
                         (calcMenu model.Device.IsMobile model.ShowMenu) ]
                          
                Navbar.Item.div 
                    [ Navbar.Item.IsHoverable
                      Navbar.Item.HasDropdown ] 
                    [ Navbar.Item.a [ Navbar.Item.Props [OnClick (fun _ -> MainMenuMsg |> MenuMsg |> dispatch )] ]  
                        [ Fulma.FontAwesome.Icon.faIcon 
                            [ Icon.Size IsSmall ] 
                            [ FontAwesome.Fa.icon FontAwesome.Fa.I.Bars ] ]
                      Navbar.Dropdown.div [ Navbar.Dropdown.IsRight ] 
                         (mainMenu model.Device.IsMobile model.ShowMenu) ] ] ]


let bottomView =
    Footer.footer [ ]
        [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Left) ] ]
            [ disclaimer ] ]


let view (model : Model) (dispatch : Msg -> unit) =

    let patView =
        div [ Style [ CSSProp.PaddingBottom "10px" ] ] 
            [ Patient.view model.Device.IsMobile model.PatientModel (PatientMsg >> dispatch) ]
    
    let content =
        match model.Page with
        | CalculatorPage    -> Calculator.view model.Device.IsMobile model.CalculatorModel (CalculatorMsg >> dispatch)
        | EmergencyListPage -> Emergency.view model.Device.IsMobile model.PatientModel model.EmergencyModel (EmergencyMsg >> dispatch)
    
    div [ ]
        [ model |> topView dispatch  

          Container.container [ Container.Props [Style [ CSSProp.Padding "10px"]] ]
              [ patView
                content ]
          bottomView  ] 


#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram init update view
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR
#endif
|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
