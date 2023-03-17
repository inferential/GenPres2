namespace Views

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


module Prescribe =


    module private Elmish =

        open Feliz
        open Feliz.UseElmish
        open Elmish
        open Shared
        open Utils
        open FSharp.Core


        type State =
            {
                Dialog: string list
                Indication: string option
                Medication: string option
                Route: string option
            }


        type Msg =
            | RowClick of int * string list
            | CloseDialog
            | IndicationChange of string option
            | MedicationChange of string option
            | RouteChange of string option


        let empty =
            {
                Dialog = []
                Indication = None
                Medication = None
                Route = None
            }


        let init (scenarios: Deferred<ScenarioResult>) =
            let state =
                match scenarios with
                | Resolved sc ->
                    {
                        Dialog = []
                        Indication = sc.Indication
                        Medication = sc.Medication
                        Route = sc.Route
                    }
                | _ -> empty
            state, Cmd.none


        let update
            (scenarios: Deferred<ScenarioResult>)
            updateScenario
            (msg: Msg)
            state
            =
            let clear (sc : ScenarioResult) =
                { sc with
                    Indication = None
                    Medication = None
                    Route = None
                }

            match msg with
            | RowClick (i, xs) ->
                Logging.log "rowclick:" i
                { state with Dialog = xs }, Cmd.none

            | CloseDialog -> { state with Dialog = [] }, Cmd.none

            | IndicationChange s ->
                printfn $"indication change {s}"
                match scenarios with
                | Resolved sc ->
                    if s |> Option.isNone then ScenarioResult.empty
                    else
                        { sc with Indication = s }
                    |> updateScenario
                | _ -> ()

                { state with Indication = s }, Cmd.none

            | MedicationChange s ->
                match scenarios with
                | Resolved sc ->
                    if s |> Option.isNone then ScenarioResult.empty
                    else
                        { sc with Medication = s }
                    |> updateScenario
                | _ -> ()

                { state with Medication = s }, Cmd.none

            | RouteChange s ->
                match scenarios with
                | Resolved sc ->
                    if s |> Option.isNone then ScenarioResult.empty
                    else
                        { sc with Route = s }
                    |> updateScenario
                | _ -> ()

                { state with Route = s }, Cmd.none


    open Elmish


    [<JSX.Component>]
    let View (props:
        {|
            scenarios: Deferred<Shared.Types.ScenarioResult>
            updateScenario: Shared.Types.ScenarioResult -> unit
        |}) =
        let state, dispatch =
            React.useElmish (
                init props.scenarios,
                update props.scenarios props.updateScenario,
                [| box props.scenarios; box props.updateScenario |]
            )

        let select isLoading lbl selected dispatch xs =
            Components.SimpleSelect.View({|
                updateSelected = dispatch
                label = lbl
                selected = selected
                values = xs
                isLoading = isLoading
            |})

        let progress =
            match props.scenarios with
            | Resolved _ -> JSX.jsx $"<></>"
            | _ ->
                JSX.jsx
                    $"""
                import CircularProgress from '@mui/material/CircularProgress';

                <Box sx={ {| mt = 5; display = "flex"; p = 20 |} }>
                    <CircularProgress />
                </Box>
                """

        let typoGraphy (items : Shared.Types.TextItem[]) =
            let print item =
                match item with
                | Shared.Types.Normal s ->
                    JSX.jsx
                        $"""
                    <Typography color={Mui.Colors.Grey.``700``} display="inline">{s}</Typography>
                    """
                | Shared.Types.Bold s ->
                    JSX.jsx
                        $"""
                    <Typography
                    color={Mui.Colors.BlueGrey.``700``}
                    display="inline"
                    >
                    <strong> {s} </strong>
                    </Typography>
                    """
                | Shared.Types.Italic s ->
                    JSX.jsx
                        $"""
                    <Typography
                    color={Mui.Colors.Grey.``700``}
                    display="inline"
                    >
                    <strong> {s} </strong>
                    </Typography>
                    """

            JSX.jsx
                $"""
            import Typography from '@mui/material/Typography';

            <Box display="inline" >
                {items |> Array.map print}
            </Box>
            """

        let displayScenarios med (sc : Shared.Types.Scenario) =
            if med |> Option.isNone then JSX.jsx $"""<></>"""
            else
                let med =
                    med |> Option.defaultValue ""
                    |> fun s -> $"{s} {sc.Shape} {sc.DoseType}"

                let text s =
                    JSX.jsx
                        $"""
                    import React from 'react';
                    import Typography from '@mui/material/Typography';

                    <React.Fragment>
                        <Typography
                            sx={ {| display= "inline" |} }
                            component="span"
                            variant="body1"
                            color="text.primary"
                        >
                            {s}
                        </Typography>
                    </React.Fragment>
                    """

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import List from '@mui/material/List';
                import ListItem from '@mui/material/ListItem';
                import Divider from '@mui/material/Divider';
                import ListItemText from '@mui/material/ListItemText';
                import ListItemIcon from '@mui/material/ListItemIcon';
                import Avatar from '@mui/material/Avatar';
                import Typography from '@mui/material/Typography';

                <Box sx={ {| mt=3; height="100%" |} } >
                    <Typography variant="h6">
                        {med}
                    </Typography>
                    <List sx={ {| width="100%"; maxWidth= 800; bgcolor = "background.paper" |} }>
                    <ListItem alignItems="flex-start">
                        <ListItemIcon>
                            {Mui.Icons.Notes}
                        </ListItemIcon>
                        <ListItemText
                            primary="Voorschrift"
                            secondary={sc.Prescription |> typoGraphy}
                        />
                    </ListItem>
                    <Divider variant="inset" component="li" />
                    <ListItem alignItems="flex-start">
                        <ListItemIcon>
                            {Mui.Icons.Vaccines}
                        </ListItemIcon>
                        <ListItemText
                            primary="Bereiding"
                            secondary={sc.Preparation |> typoGraphy}
                        />
                    </ListItem>
                    <Divider variant="inset" component="li" />
                    <ListItem alignItems="flex-start">
                        <ListItemIcon>
                            {Mui.Icons.MedicationLiquid}
                        </ListItemIcon>
                        <ListItemText
                            primary="Toediening"
                            secondary={sc.Administration |> typoGraphy}
                        />
                    </ListItem>
                    </List>
                </Box>
                """

        let stackDirection =
            if  Mui.Hooks.useMediaQuery "(max-width:900px)" then "column" else "row"

        let content =
            JSX.jsx
                $"""
            import CardContent from '@mui/material/CardContent';
            import Typography from '@mui/material/Typography';
            import Stack from '@mui/material/Stack';

            <CardContent>
                <Typography sx={ {| fontSize=14 |} } color="text.secondary" gutterBottom>
                Medicatie scenario's
                </Typography>
                <Stack direction={stackDirection} spacing={3} >
                    {
                        match props.scenarios with
                        | Resolved scrs -> false, scrs.Indication, scrs.Indications
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading "Indicaties" sel (IndicationChange >> dispatch)
                    }
                    {
                        match props.scenarios with
                        | Resolved scrs -> false, scrs.Medication, scrs.Medications
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading "Medicatie" sel (MedicationChange >> dispatch)
                    }
                    {
                        match props.scenarios with
                        | Resolved scrs -> false, scrs.Route, scrs.Routes
                        | _ -> true, None, [||]
                        |> fun (isLoading, sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select isLoading "Routes" sel (RouteChange >> dispatch)
                    }
                </Stack>
                <Stack direction="column" sx={ {| mt = 1 |} } >
                    {
                        match props.scenarios with
                        | Resolved sc ->
                            sc.Medication,
                            sc.Scenarios
                        | _ -> None, [||]
                        |> fun (med, scs) ->
                            scs
                            |> Array.map (displayScenarios med)
                    }
                </Stack>
            </CardContent>
            """

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Card from '@mui/material/Card';
        import CardActions from '@mui/material/CardActions';
        import CardContent from '@mui/material/CardContent';
        import Button from '@mui/material/Button';
        import Typography from '@mui/material/Typography';

        <Box sx={ {| height="100%" |} }>
            <Card sx={ {| minWidth = 275 |}  }>
                {content}
                {progress}
            <CardActions>
                <Button size="small">Bewerken</Button>
            </CardActions>
            </Card>
        </Box>
        """

