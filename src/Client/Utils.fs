namespace Utils


module Typography =

    open Feliz
    open MaterialUI.Typography

    let private createTypography v a =
        MaterialTypography.create [
            v
            MaterialTypography.color "inherit"
            prop.text (a |> string)
        ]

    let subtitle1 a =
        a |> createTypography(MaterialTypography.variant "subtitle1")

    let subtitle2 a =
        a |> createTypography (MaterialTypography.variant "subtitle2")

    let body1 a =
        a |> createTypography (MaterialTypography.variant "body1")

    let body2 a =
        a |> createTypography (MaterialTypography.variant "body2")

    let caption a =
        a |> createTypography (MaterialTypography.variant "caption")

    let button a =
        a |> createTypography (MaterialTypography.variant "button")

    let h1 a =
        a |> createTypography (MaterialTypography.variant "h1")

    let h2 a =
        a |> createTypography (MaterialTypography.variant "h2")

    let h3 a =
        a |> createTypography (MaterialTypography.variant "h3")

    let h4 a =
        a |> createTypography (MaterialTypography.variant "h4")

    let h5 a =
        a |> createTypography (MaterialTypography.variant "h5")

    let h6 a =
        a |> createTypography (MaterialTypography.variant "h6")


module Logging =

    open Browser.Dom

    let log (msg: string) a = console.log (box msg, [| box a |])

    let error (msg: string) e = console.error (box msg, [| box e |])

    let warning (msg: string) a = console.warn (box msg, [| box a |])


module GoogleDocs =

    open System
    open Fable.SimpleHttp
    open Shared

    let inline getUrl parseResponse msg url =
        async {
            let! (statusCode, responseText) = Http.get url

            let result =
                match statusCode with
                | 200 ->
                    responseText
                    |> Csv.parseCSV
                    |> parseResponse
                    |> Ok
                    |> Finished
                | _ -> Finished(Error $"Status {statusCode} => {responseText}")
                |> msg

            return result
        }


    let createUrl sheet id =
        $"https://docs.google.com/spreadsheets/d/{id}/gviz/tq?tqx=out:csv&sheet={sheet}"

    //https://docs.google.com/spreadsheets/d/1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM/edit?usp=sharing
    [<Literal>]
    let dataUrlId =
        "1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM"


    let loadBolusMedication msg =
        dataUrlId
        |> createUrl "emergencylist"
        |> getUrl EmergencyTreatment.parse msg


    let loadContinuousMedication msg =
        dataUrlId
        |> createUrl "continuousmeds"
        |> getUrl ContinuousMedication.parse msg


    let loadProducts msg =
        dataUrlId
        |> createUrl "products"
        |> getUrl Products.parse msg