namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types



open Elmish
open Fable.Core.JsInterop


module ResponsiveTable =


    module private Cards =


        [<JSX.Component>]
        let CardTable (props :
            {|
                columns : {|  field : string; headerName : string; width : int; filterable : bool; sortable : bool |}[]
                rows : {| cells : {| field: string; value: string |} []; actions : ReactElement option |} []
                filter : ReactElement option
            |}) =

            let cards =
                props.rows
                |> Array.map (fun row ->
                    let content =
                        row.cells
                        |> Array.map (fun cell ->
                            if cell.field = "id" || String.IsNullOrWhiteSpace(cell.value) then JSX.jsx "<></>"
                            else
                                let b, s =
                                    match cell.value with
                                    | _ when cell.value.Contains("**") -> Mui.Colors.Blue.``900``, cell.value.Replace("**", "")
                                    | _ when cell.value.Contains("*") -> Mui.Colors.Blue.``900``, cell.value.Replace("*", "")
                                    | _ -> Mui.Colors.Grey.``700``, cell.value

                                let h =
                                    props.columns
                                    |> Array.tryFind (fun c -> c.field = cell.field)
                                    |> function
                                    | Some h -> $"{h.headerName.ToLower()}: "
                                    | None   -> $"{cell.field}: "

                                JSX.jsx
                                    $"""
                                import React from 'react';
                                import Stack from '@mui/material/Stack';
                                import Divider from '@mui/material/Divider';
                                import Typography from '@mui/material/Typography';

                                <React.Fragment>                                    
                                        <Stack direction="row" spacing={3} >
                                            <Typography minHeight={40} minWidth={80} variant="body2" color={Mui.Colors.Grey.``900``} >
                                                {h}
                                            </Typography>
                                            <Typography minHeight={40} color={b} variant="body2" >
                                                {s}
                                            </Typography>
                                        </Stack>
                                </React.Fragment>
                                """
                        )

                    JSX.jsx
                        $"""
                    import Card from '@mui/material/Card';
                    import CardHeader from '@mui/material/CardHeader';
                    import CardActions from '@mui/material/CardActions';
                    import CardContent from '@mui/material/CardContent';

                    <Grid item width={500} sx={ {| mb = 1 |} } >
                        <Card raised={true} >
                            <CardHeader>
                                Header
                            </CardHeader>
                            <CardContent>
                                {React.fragment (content |> unbox)}
                            </CardContent>
                            <CardActions>
                                {
                                    match row.actions with
                                    | _ -> JSX.jsx "<></>" |> toReact
                                }
                            </CardActions>
                        </Card>
                    </Grid>
                    """
                )

            JSX.jsx
                $"""
            import Grid from '@mui/material/Grid';
            import Box from '@mui/material/Box';
            import Stack from '@mui/material/Stack';

            <Stack id="responsive-card-table" >
                <Box sx={ {| mb=3 |} }>
                    {props.filter |> Option.defaultValue (JSX.jsx "<></>" |> toReact)}
                </Box>
                <Grid container rowSpacing={1} columnSpacing={ {| xs=1; sm=2; md=3 |} } >
                    {React.fragment (cards |> unbox)}
                </Grid>
            </Stack>
            """


    open Cards


    [<JSX.Component>]
    let View (props :
        {|
            columns : {|  field : string; headerName : string; width : int; filterable : bool; sortable : bool |}[]
            rows : {| cells : {| field: string; value: string |} []; actions : ReactElement option |} []
            rowCreate : string[] -> obj
        |}) =
        let state, setState = React.useState None

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let columnFilter =
            if props.rows |> Array.isEmpty then None
            else
                props.columns
                |> Array.tryFind (fun c -> c.filterable)

        let filter =
            columnFilter
            |> function
            | None   -> JSX.jsx "<></>"
            | Some column ->
                let data =
                    props.rows
                    |> Array.map (fun r -> r.cells)
                    |> Array.map (Array.filter (fun cell ->
                        cell.field = column.field
                    ))
                    |> Array.collect (Array.map (_.value))
                    |> Array.distinct
                    |> Array.sortBy (_.ToLower())

                SimpleSelect.View({|
                    label = "Filter"
                    selected = state
                    updateSelected = setState
                    values = data |> Array.map (fun s -> s, s)
                    isLoading = false
                |})
            |> toReact

        let rows =
            props.rows
            |> Array.filter (fun r ->
                match columnFilter with
                | None -> true
                | Some column ->
                    r.cells
                    |> Array.exists (fun cell ->
                        cell.field = column.field &&
                        (state |> Option.isNone || state.Value = cell.value)
                    )
            )

        if isMobile then
            {| columns = props.columns; rows = rows; filter = Some filter |}
            |> CardTable
        else
            let rows =
                rows
                |> Array.map (fun r -> r.cells)
                |> Array.map (Array.map (fun r -> r.value))
                |> Array.map props.rowCreate

            JSX.jsx
                $"""
            import {{DataGrid}} from '@mui/x-data-grid';

            <Box sx={ {| height="80vh" |} } >
                <Box sx={ {| mb=3 |} }>
                    {filter}
                </Box>
                <DataGrid
                    rows={rows}
                    initialState =
                        {
                            {| columns = {| columnVisibilityModel = {| id = false |} |} |}
                        }
                    columns=
                        {
                            props.columns
                            |> Array.map (fun c ->
                                match c.field with
                                | s when s = "id" ->
                                    {| c with hide = true |} |> box
                                | _ -> c |> box
                            )
                        }
                    pageSize={100}
                    autoPageSize={true}
                />
            </Box>
            """

