namespace Informedica.GenForm.Lib


module RenalRule =

    open Informedica.GenCore.Lib.Calculations
    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL

    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges



    module DoseReduction =

        let [<Literal>] REL = "rel"
        let [<Literal>] ABS = "abs"

        let fromString s =
            match s with
            | _ when s |> String.equalsCapInsens ABS -> Absolute
            | _ when s |> String.equalsCapInsens REL -> Relative
            | _ ->
                ConsoleWriter.writeWarningMessage
                    $"{s} is not a valid dosereduction" true false
                NoReduction


        let toString = function
            | Absolute -> ABS
            | Relative -> REL
            | NoReduction -> ""



    module Limit =

        /// <summary>
        /// Create a RenalLimit
        /// </summary>
        /// <returns>
        /// RenalLimit
        /// </returns>
        let create
            doseLimitTarget
            doseReduction
            quantity
            normQuantityAdjust
            quantityAdjust
            perTime
            normPerTimeAdjust
            perTimeAdjust
            rate
            rateAdjust
            =
            {
                DoseLimitTarget = doseLimitTarget
                DoseReduction = doseReduction
                Quantity = quantity
                NormQuantityAdjust = normQuantityAdjust
                QuantityAdjust = quantityAdjust
                PerTime = perTime
                NormPerTimeAdjust = normPerTimeAdjust
                PerTimeAdjust = perTimeAdjust
                Rate = rate
                RateAdjust = rateAdjust
            }


    let create gen rte src dt fr it rf limits : RenalRule =
        {
            Generic = gen
            Route = rte
            Source = src
            RenalFunction = rf
            DoseType = dt
            Frequencies = fr
            IntervalTime = it
            RenalLimits = limits
        }


    let getData () =
        let dataUrlId = Web.getDataUrlIdGenPres ()
        Web.getDataFromSheet dataUrlId "RenalRules"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.map (fun r ->
                let get =
                    getColumn r >> String.trim
                let toBrOpt = BigRational.toBrs >> Array.tryHead

                {|
                    Generic = get "Generic"
                    Route = get "Route"
                    Source = get "Source"
                    IntDial = get "IntDial"
                    ContDial = get "ContDial"
                    PerDial = get "PerDial"
                    MinGFR = get "MinGFR" |> toBrOpt
                    MaxGFR = get "MaxGFR" |> toBrOpt
                    DoseType = get "DoseType"
                    DoseText = get "DoseText"
                    Frequencies = get "Freqs" |> BigRational.toBrs
                    DoseRed = get "DoseRed"
                    DoseUnit = get "DoseUnit"
                    AdjustUnit = get "AdjustUnit"
                    FreqUnit = get "FreqUnit"
                    RateUnit = get "RateUnit"
                    MinInterval = get "MinInt" |> toBrOpt
                    MaxInterval = get "MaxInt" |> toBrOpt
                    IntervalUnit = get "IntUnit"
                    Substance = get "Substance"
                    MinQty = get "MinQty" |> toBrOpt
                    MaxQty = get "MaxQty" |> toBrOpt
                    NormQtyAdj = get "NormQtyAdj" |> toBrOpt
                    MinQtyAdj = get "MinQtyAdj" |> toBrOpt
                    MaxQtyAdj = get "MaxQtyAdj" |> toBrOpt
                    MinPerTime = get "MinPerTime" |> toBrOpt
                    MaxPerTime = get "MaxPerTime" |> toBrOpt
                    NormPerTimeAdj = get "NormPerTimeAdj" |> toBrOpt
                    MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
                    MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
                    MinRate = get "MinRate" |> toBrOpt
                    MaxRate = get "MaxRate" |> toBrOpt
                    MinRateAdj = get "MinRateAdj" |> toBrOpt
                    MaxRateAdj = get "MaxRateAdj" |> toBrOpt
                |}
            )



    let fromTupleInclExcl = MinMax.fromTuple Inclusive Exclusive


    let fromTupleInclIncl = MinMax.fromTuple Inclusive Inclusive



    let createRenalFunction
        contDial
        intDial
        perDial
        minGFR
        maxGFR
        =
        match minGFR, maxGFR with
        | None, None ->
            match contDial, intDial, perDial with
            | "x", _, _ -> ContinuousHemodialysis |> Some
            | _, "x", _ -> IntermittentHemodialysis |> Some
            | _, _, "x" -> PeritonealDialysis |> Some
            | _, _, _ -> None
        | None, Some max ->
            (None, max |> BigRational.ToInt32 |>Some)
            |> EGFR |> Some
        | Some min, None ->
            (min |> BigRational.ToInt32 |> Some, None)
            |> EGFR |> Some
        | Some min, Some max ->
            (min |> BigRational.ToInt32 |> Some, max |> BigRational.ToInt32 |> Some)
            |> EGFR |> Some


    let fromData (data : {| AdjustUnit: string; ContDial: string; DoseRed: string; DoseText: string; DoseType: string; DoseUnit: string; FreqUnit: string; Frequencies: BigRational array; Generic: string; IntDial: string; IntervalUnit: string; MaxGFR: BigRational option; MaxInterval: BigRational option; MaxPerTime: BigRational option; MaxPerTimeAdj: BigRational option; MaxQty: BigRational option; MaxQtyAdj: BigRational option; MaxRate: BigRational option; MaxRateAdj: BigRational option; MinGFR: BigRational option; MinInterval: BigRational option; MinPerTime: BigRational option; MinPerTimeAdj: BigRational option; MinQty: BigRational option; MinQtyAdj: BigRational option; MinRate: BigRational option; MinRateAdj: BigRational option; NormPerTimeAdj: BigRational option; NormQtyAdj: BigRational option; PerDial: string; RateUnit: string; Route: string; Source: string; Substance: string |} array) =
        data
        |> Array.filter (fun r ->
            r.Generic <> "" &&
            r.Source <> ""
        )
        |> Array.choose (fun r ->
            createRenalFunction
                r.ContDial
                r.IntDial
                r.PerDial
                r.MinGFR
                r.MaxGFR
            |> Option.map (fun rf ->
                {| r with RenalFunction = rf |}
            )
        )
        |> Array.groupBy (fun r ->
            r.Generic,
            r.Route,
            r.Source,
            DoseType.fromString r.DoseType r.DoseText,
            match r.FreqUnit |> Units.freqUnit with
            | None -> None
            | Some u ->
                if r.Frequencies |> Array.isEmpty then None
                else
                    r.Frequencies
                    |> ValueUnit.withUnit u
                    |> Some
            ,
            (r.MinInterval, r.MaxInterval)
            |> fromTupleInclIncl (r.IntervalUnit |> Utils.Units.timeUnit)
            ,
            r.RenalFunction
        )
        |> Array.map (fun ((gen, rte, src, dt, fr, it, rf), rules) ->
            let limits =
                rules
                |> Array.filter (fun r -> r.DoseRed |> String.notEmpty)
                |> Array.map (fun r ->
                    let times =
                        Units.Count.times
                        |> Some

                    // the adjust unit
                    let adj =
                        if r.DoseRed = DoseReduction.REL then None
                        else
                            r.AdjustUnit |> Utils.Units.adjustUnit
                    // the dose unit
                    let du =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            r.DoseUnit |> Units.fromString
                    // the adjusted dose unit
                    let duAdj =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            match adj, du with
                            | Some adj, Some du ->
                                du
                                |> Units.per adj
                                |> Some
                            | _ -> None
                    // the time unit
                    let tu = r.FreqUnit |> Utils.Units.timeUnit
                    // the dose unit per time unit
                    let duTime =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            match du, tu with
                            | Some du, Some tu ->
                                du
                                |> Units.per tu
                                |> Some
                            | _ -> None
                    // the adjusted dose unit per time unit
                    let duAdjTime =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            match duAdj, tu with
                            | Some duAdj, Some tu ->
                                duAdj
                                |> Units.per tu
                                |> Some
                            | _ -> None
                    // the rate unit
                    let ru =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            r.RateUnit |> Units.fromString
                    // the dose unit per rate unit
                    let duRate =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            match du, ru with
                            | Some du, Some ru ->
                                du
                                |> Units.per ru
                                |> Some
                            | _ -> None
                    // the adjusted dose unit per rate unit
                    let duAdjRate =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            match duAdj, ru with
                            | Some duAdj, Some ru ->
                                duAdj
                                |> Units.per ru
                                |> Some
                            | _ -> None

                    Limit.create
                        (r.Substance |> LimitTarget.SubstanceLimitTarget)
                        (r.DoseRed |> DoseReduction.fromString)
                        ((r.MinQty, r.MaxQty) |> fromTupleInclIncl du)
                        (r.NormQtyAdj |> ValueUnit.withOptionalUnit duAdj)
                        ((r.MinQtyAdj, r.MaxQtyAdj) |> fromTupleInclIncl duAdj)
                        ((r.MinPerTime, r.MaxPerTime) |> fromTupleInclIncl duTime)
                        (r.NormPerTimeAdj |> ValueUnit.withOptionalUnit duAdjTime)
                        ((r.MinPerTimeAdj, r.MaxPerTimeAdj) |> fromTupleInclIncl duAdjTime)
                        ((r.MinRate, r.MaxRate) |> fromTupleInclIncl duRate)
                        ((r.MinRateAdj, r.MaxRateAdj) |> fromTupleInclIncl duAdjRate)
                )

            create
                gen
                rte
                src
                dt
                fr
                it
                rf
                limits
        )


    let get : unit -> RenalRule [] =
        fun () ->
            getData ()
            |> fromData
        |> Memoization.memoize


    let filter (filter : Filter) (renalRules : RenalRule []) =
        let eqs a (b : string) =
            a
            |> Option.map (fun x ->
                x |> String.equalsCapInsens b
            )
            |> Option.defaultValue true

        [|
            // renal rules only applies to patients at least 3 months of age
            fun _ ->
                filter.Patient.Age
                |> Option.map (fun a ->
                    let mo3 = Units.Time.month |> ValueUnit.singleWithValue 3N
                    a >? mo3
                ) |> Option.defaultValue false
            fun (rr : RenalRule) -> rr.Generic |> eqs filter.Generic
            fun (rr : RenalRule) ->
                rr.Route |> String.isNullOrWhiteSpace ||
                (filter.Route |> Option.isNone || rr.Route |> Mapping.eqsRoute filter.Route)
            fun (rr : RenalRule) ->
                rr.DoseType = NoDoseType ||
                filter.DoseType
                |> Option.map (DoseType.eqsType rr.DoseType)
                |> Option.defaultValue true
            fun (rr : RenalRule) ->
                filter.Patient.RenalFunction
                |> Option.map (fun rf ->
                    match rr.RenalFunction with
                    | ContinuousHemodialysis
                    | IntermittentHemodialysis
                    | PeritonealDialysis -> rr.RenalFunction = rf
                    | EGFR(rmin, rmax) ->
                        match rf with
                        | EGFR(pmin, pmax) ->
                            if pmin = pmax then
                                match pmin with
                                | None -> false
                                | Some v ->
                                    (rmin |> Option.map (fun rmin -> v >= rmin) |> Option.defaultValue true) &&
                                    (rmax |> Option.map (fun rmax -> v <= rmax) |> Option.defaultValue true)
                            else
                                pmin = rmin && pmax = rmax
                        | _ -> false
                )
                |> Option.defaultValue false
        |]
        |> Array.fold (fun (acc : RenalRule[]) pred ->
            acc |> Array.filter pred
        ) renalRules


    let adjustDoseRule (renalRule : RenalRule) (doseRule : DoseRule) =
        let adjustVU dr vu2 vu1 =
            match dr with
            | Relative ->
                vu1
                |> Option.map (fun vu1 ->
                    vu2
                    |> Option.map (fun vu2 ->
                        vu1 * vu2
                    )
                    |> Option.defaultValue vu1
                )
            | Absolute ->
                match vu1, vu2 with
                | Some v1, Some v2 ->
                    if v1 <? v2 then v1 else v2
                    |> Some
                | _ -> vu2
            | NoReduction -> vu1

        let adjustMinMax dr mm2 mm1 =
            match dr with
            | Absolute ->
                if mm2 = MinMax.empty then mm1 else
                    {
                        Min =
                            match mm1.Min, mm2.Min with
                            | Some lim1, Some lim2 ->
                                if lim1 |> Limit.st true true lim2 then lim1 |> Some
                                else
                                    lim2 |> Some
                            | _ -> mm2.Min
                        Max =
                            match mm1.Max, mm2.Max with
                            | Some lim1, Some lim2 ->
                                if lim1 |> Limit.st false false lim2 then lim1 |> Some
                                else
                                    lim2 |> Some
                            | _ -> mm2.Max
                    }
            | Relative ->
                if mm1 = MinMax.empty || mm2 = MinMax.empty then mm1
                else
                    {
                        Min =
                            match mm1.Min, mm2.Min with
                            | Some lim1, Some lim2 ->
                                MinMax.calcLimit (*) lim1 lim2
                                |> Some
                            | _ -> mm1.Min
                        Max =
                            match mm1.Max, mm2.Max with
                            | Some lim1, Some lim2 ->
                                MinMax.calcLimit (*) lim1 lim2
                                |> Some
                            | _ -> mm1.Max
                    }
            | NoReduction -> mm1

        { doseRule with
            RenalRule = Some renalRule.Source
            Frequencies =
                if renalRule.Frequencies |> Option.isNone then doseRule.Frequencies
                else
                    renalRule.Frequencies
            DoseLimits =
                doseRule.DoseLimits
                |> Array.map (fun dl ->
                    renalRule.RenalLimits
                    |> Array.tryFind (fun rl -> rl.DoseLimitTarget = dl.DoseLimitTarget)
                    |> function
                        | None -> dl
                        | Some rl when rl.DoseReduction = NoReduction -> dl
                        | Some rl ->
                            let normQtyAdj =
                                if dl.NormQuantityAdjust |> Option.isSome then dl.NormQuantityAdjust
                                else
                                    dl.NormPerTimeAdjust
                                    |> Option.bind (fun vu ->
                                        doseRule.Frequencies
                                        |> Option.map (fun f -> vu / f)
                                    )
                            { dl with
                                Quantity =
                                    dl.Quantity
                                    |> adjustMinMax
                                        rl.DoseReduction
                                        rl.Quantity
                                NormQuantityAdjust =
                                    normQtyAdj
                                    |> adjustVU
                                        rl.DoseReduction
                                        rl.NormQuantityAdjust
                                QuantityAdjust =
                                    dl.QuantityAdjust
                                    |> adjustMinMax
                                        rl.DoseReduction
                                        rl.QuantityAdjust
                                PerTime =
                                    dl.PerTime
                                    |> adjustMinMax
                                        rl.DoseReduction
                                        rl.PerTime
                                NormPerTimeAdjust =
                                    if rl.NormQuantityAdjust |> Option.isSome ||
                                       rl.QuantityAdjust <> MinMax.empty then None
                                    else
                                        dl.NormPerTimeAdjust
                                        |> adjustVU
                                            rl.DoseReduction
                                            rl.NormPerTimeAdjust
                                PerTimeAdjust =
                                    dl.PerTimeAdjust
                                    |> adjustMinMax
                                        rl.DoseReduction
                                        rl.PerTimeAdjust
                                Rate =
                                    dl.Rate
                                    |> adjustMinMax
                                        rl.DoseReduction
                                        rl.Rate
                                RateAdjust =
                                    dl.RateAdjust
                                    |> adjustMinMax
                                        rl.DoseReduction
                                        rl.RateAdjust

                            }
                )
        }
