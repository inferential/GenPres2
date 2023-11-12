namespace Informedica.GenOrder.Lib


module Api =

    open System

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    /// <summary>
    /// Get all possible indications for a Patient
    /// </summary>
    let getIndications = PrescriptionRule.get >> PrescriptionRule.indications


    /// <summary>
    /// Get all possible generics for a Patient
    /// </summary>
    let getGenerics = PrescriptionRule.get >> PrescriptionRule.generics


    /// <summary>
    /// Get all possible routes for a Patient
    /// </summary>
    let getRoutes = PrescriptionRule.get >> PrescriptionRule.routes


    /// <summary>
    /// Get all possible shapes for a Patient
    /// </summary>
    let getShapes = PrescriptionRule.get >> PrescriptionRule.shapes


    /// <summary>
    /// Get all possible diagnoses for a Patient
    /// </summary>
    let getDiagnoses = PrescriptionRule.get >> PrescriptionRule.diagnoses


    /// <summary>
    /// Get all possible frequencies for a Patient
    /// </summary>
    let getFrequencies =  PrescriptionRule.get >> PrescriptionRule.frequencies


    /// <summary>
    /// Filter the indications using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterIndications = PrescriptionRule.filter >> PrescriptionRule.indications


    /// <summary>
    /// Filter the generics using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterGenerics = PrescriptionRule.filter >> PrescriptionRule.generics


    /// <summary>
    /// Filter the routes using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterRoutes = PrescriptionRule.filter >> PrescriptionRule.routes


    /// <summary>
    /// Filter the shapes using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterShapes = PrescriptionRule.filter >> PrescriptionRule.shapes


    /// <summary>
    /// Filter the diagnoses using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterDiagnoses = PrescriptionRule.filter >> PrescriptionRule.diagnoses


    /// <summary>
    /// Filter the frequencies using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterFrequencies =  PrescriptionRule.filter >> PrescriptionRule.shapes


    let private tryHead m = (Array.map m) >> Array.tryHead >> (Option.defaultValue "")


    /// <summary>
    /// Create a ProductComponent from a list of Products.
    /// DoseLimits are used to set the Dose for the ProductComponent.
    /// If noSubst is true, the substances will not be added to the ProductComponent.
    /// The freqUnit is used to set the TimeUnit for the Frequencies.
    /// </summary>
    /// <param name="noSubst">Whether or not to add the substances to the ProductComponent</param>
    /// <param name="freqUnit">The TimeUnit for the Frequencies</param>
    /// <param name="doseLimits">The DoseLimits for the ProductComponent</param>
    /// <param name="ps">The Products to create the ProductComponent from</param>
    let createProductComponent noSubst freqUnit (doseLimits : DoseLimit []) (ps : Product []) =
        { DrugOrder.productComponent with
            Name =
                ps
                |> tryHead (fun p -> p.Shape)
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace then "oplosvloeistof"
                    else s
            Shape =
                ps
                |> tryHead (fun p -> p.Shape)
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace then "oplosvloeistof"
                    else s
            Quantities =
                ps
                |> Array.collect (fun p -> p.ShapeQuantities)
                |> Array.distinct
                |> Array.toList
            TimeUnit = freqUnit
            RateUnit = "uur" //doseRule.RateUnit
            Divisible =
                ps
                |> Array.choose (fun p -> p.Divisible)
                |> Array.tryHead
            Substances =
                if noSubst then []
                else
                    ps
                    |> Array.collect (fun p -> p.Substances)
                    |> Array.groupBy (fun s -> s.Name)
                    |> Array.map (fun (n, xs) ->
                        {
                            Name = n
                            Concentrations =
                                xs
                                |> Array.choose (fun s -> s.Quantity)
                                |> Array.distinct
                                |> Array.toList
                            Unit = xs |> tryHead (fun x -> x.Unit)
                            TimeUnit = freqUnit
                            Dose =
                                doseLimits
                                |> Array.tryFind (fun l -> l.Substance = n)
                            Solution = None
                        }
                    )
                    |> Array.toList
        }


    /// <summary>
    /// Set the SolutionLimits for a list of SubstanceItems.
    /// </summary>
    /// <param name="sls">The SolutionLimits to set</param>
    /// <param name="items">The SubstanceItems to set the SolutionLimits for</param>
    let setSolutionLimit (sls : SolutionLimit[]) (items : SubstanceItem list) =
        items
        |> List.map (fun item ->
            match sls |> Array.tryFind (fun sl -> sl.Substance |> String.equalsCapInsens item.Name) with
            | None -> item
            | Some sl ->
                { item with
                    Solution = Some sl
                }
        )


    /// <summary>
    /// Create a DrugOrder from a PrescriptionRule and a SolutionRule.
    /// </summary>
    /// <param name="sr">The optional SolutionRule to use</param>
    /// <param name="pr">The PrescriptionRule to use</param>
    let createDrugOrder (sr: SolutionRule option) (pr : PrescriptionRule) =
        let parenteral = Product.Parenteral.get ()
        // adjust unit defaults to kg
        let au =
            if pr.DoseRule.AdjustUnit |> String.isNullOrWhiteSpace then "kg"
            else pr.DoseRule.AdjustUnit

        let dose =
            pr.DoseRule.DoseLimits
            |> Array.filter (fun dl ->
                dl.Substance |> String.isNullOrWhiteSpace
            )
            |> function
            | [| dl |] -> dl |> Some
            | _ -> None

        // if no subst, dose is based on shape
        let noSubst =
            dose
            |> Option.map (fun d -> d.DoseUnit = "keer")
            |> Option.defaultValue false

        { DrugOrder.drugOrder with
            Id = Guid.NewGuid().ToString()
            Name = pr.DoseRule.Generic
            Products =
                pr.DoseRule.Products
                |> createProductComponent noSubst pr.DoseRule.FreqUnit pr.DoseRule.DoseLimits
                |> List.singleton
            Quantities = []
            Frequencies = pr.DoseRule.Frequencies |> Array.toList
            FreqUnit = pr.DoseRule.FreqUnit
            Unit =
                pr.DoseRule.Products
                |> tryHead (fun p -> p.ShapeUnit)
            Time = pr.DoseRule.Time
            TimeUnit = pr.DoseRule.TimeUnit
            RateUnit = "uur"
            Route = pr.DoseRule.Route
            DoseCount =
                if pr.SolutionRules |> Array.isEmpty then Some 1N
                else None
            OrderType =
                match pr.DoseRule.DoseType with
                | Informedica.GenForm.Lib.Types.Continuous -> ContinuousOrder
                | _ when pr.DoseRule.TimeUnit |> String.isNullOrWhiteSpace -> DiscontinuousOrder
                | _ -> TimedOrder
            Dose = dose
            Adjust =
                if au = "kg" then
                    pr.Patient.Weight
                    |> Option.map (fun v -> v / 1000N)
                else pr.Patient |> Patient.calcBSA
            AdjustUnit = au
        }
        |> fun dro ->
                // add an optional solution rule
                match sr with
                | None -> dro
                | Some sr ->
                    { dro with
                        Dose =
                            { DoseRule.DoseLimit.limit with
                                Quantity  = sr.Volume
                                DoseUnit = "mL"
                            } |> Some
                        Quantities = sr.Volumes |> Array.toList
                        DoseCount = sr.DosePerc.Maximum
                        Products =
                            let ps =
                                dro.Products
                                |> List.map (fun p ->
                                    { p with
                                        Name = dro.Name
                                        Shape = p.Shape
                                        Substances =
                                            p.Substances
                                            |> setSolutionLimit sr.SolutionLimits
                                    }
                                )

                            let s =
                                // ugly hack to get default solution
                                sr.Solutions
                                |> Array.tryHead
                                |> Option.defaultValue "x"

                            parenteral
                            |> Array.tryFind (fun p ->
                                    s |> String.notEmpty &&
                                    p.Generic |> String.startsWith s
                                )
                            |> function
                            | Some p ->
                                [|p|]
                                |> createProductComponent true pr.DoseRule.FreqUnit [||]
                                |> List.singleton
                                |> List.append ps
                            | None ->
                                printfn $"couldn't find {s} in parenterals"
                                ps
                    }


    /// <summary>
    /// Increase the Orderable Quantity and Rate Increment of an Order.
    /// This allows speedy calculation by avoiding large amount
    /// of possible values.
    /// </summary>
    /// <param name="logger">The OrderLogger to use</param>
    /// <param name="ord">The Order to increase the increment of</param>
    let increaseIncrements logger ord = Order.increaseIncrements logger 10N 50N ord


    /// <summary>
    /// Evaluate a PrescriptionRule. The PrescriptionRule can result in
    /// multiple Orders, depending on the SolutionRules.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="rule"></param>
    /// <returns>
    /// An array of Results, containing the Order and the PrescriptionRule.
    /// </returns>
    let evaluate logger (rule : PrescriptionRule) =
        let rec solve retry sr pr =
            pr
            |> createDrugOrder sr
            |> DrugOrder.toOrderDto
            |> Order.Dto.fromDto
            |> Order.solveMinMax false logger
            |> Result.bind (increaseIncrements logger) // not sure if this is usable
            |> function
            | Ok ord ->
                let dto = ord |> Order.Dto.toDto

                let shps =
                    dto.Orderable.Components
                    |> List.choose (fun cDto -> cDto.ComponentQuantity.Variable.ValsOpt)
                    |> List.toArray
                    |> Array.collect (fun dto ->
                        dto.Value
                        |> Array.map (fst >> BigRational.parse)
                    )

                let sbsts =
                    dto.Orderable.Components
                    |> List.toArray
                    |> Array.collect (fun cDto ->
                        cDto.Items
                        |> List.toArray
                        |> Array.choose (fun iDto ->
                            iDto.ComponentConcentration.Variable.ValsOpt
                            |> Option.map (fun v ->
                                iDto.Name,
                                v.Value
                                |> Array.map (fst >> BigRational.parse)
                                |> Array.tryHead
                            )
                        )
                    )
                    |> Array.distinct

                let pr =
                    pr
                    |> PrescriptionRule.filterProducts
                        shps
                        sbsts

                Ok (ord, pr)
            | Error (ord, m) when retry ->
                    if sr |> Option.isSome then Error(ord, pr, m)
                    else
                        let dose = ord.Orderable.Components[0].Items[0].Dose.Quantity
                        printfn $"trying a second time with manual product: {dose |> OrderVariable.Quantity.toString}"
                        { pr with
                            DoseRule =
                                { pr.DoseRule with
                                    Products =
                                        pr.DoseRule.Products
                                        |> Array.map (fun p ->
                                            { p with
                                                Divisible = None
                                            }
                                        )
                                }
                        }
                        |> solve false None
            | Error (ord, m) -> Error (ord, pr, m)

        if rule.SolutionRules |> Array.isEmpty then [| solve true None rule |]
        else
            rule.SolutionRules
            |> Array.map (fun sr -> solve true (Some sr) rule)


    /// <summary>
    /// Create an initial ScenarioResult for a Patient.
    /// </summary>
    let scenarioResult pat =
        let rules = pat |> PrescriptionRule.get
        {
            Indications = rules |> PrescriptionRule.indications
            Generics = rules |> PrescriptionRule.generics
            Routes = rules |> PrescriptionRule.routes
            Shapes= rules |> PrescriptionRule.shapes
            Indication = None
            Generic = None
            Route = None
            Shape = None
            Patient = pat
            Scenarios = [||]
        }


    let replace s =
        s
        |> String.replace "[" ""
        |> String.replace "]" ""
        |> String.replace "<" ""
        |> String.replace ">" ""


    /// <summary>
    /// Use a Filter and a ScenarioResult to create a new ScenarioResult.
    /// </summary>
    let filter (sc : ScenarioResult) =

        if Env.getItem "GENPRES_PROD" |> Option.isNone then
            let path = $"{__SOURCE_DIRECTORY__}/log.txt"
            OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

        match sc.Patient.Weight, sc.Patient.Height, sc.Patient.Department with
        | Some w, Some h, d when d |> String.notEmpty ->

            let ind =
                if sc.Indication.IsSome then sc.Indication
                else sc.Indications |> Array.someIfOne
            let gen =
                if sc.Generic.IsSome then sc.Generic
                else sc.Generics |> Array.someIfOne
            let rte =
                if sc.Route.IsSome then sc.Route
                else sc.Routes |> Array.someIfOne
            let shp =
                if sc.Shape.IsSome then sc.Shape
                else sc.Shapes |> Array.someIfOne

            let filter =
                { Filter.filter with
                    Department = Some d
                    Age = sc.Patient.Age
                    GestAge = sc.Patient.GestAge
                    Weight = Some w
                    Height = Some h
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Shape = shp
                    Location = sc.Patient.VenousAccess
                }

            let inds = filter |> filterIndications
            let gens = filter |> filterGenerics
            let rtes = filter |> filterRoutes
            let shps = filter |> filterShapes

            let ind = inds |> Array.someIfOne
            let gen = gens |> Array.someIfOne
            let rte = rtes |> Array.someIfOne
            let shp = shps |> Array.someIfOne

            { sc with
                Indications = inds
                Generics = gens
                Routes = rtes
                Shapes = shps
                Indication = ind
                Generic = gen
                Route = rte
                Shape = shp
                Scenarios =
                    match ind, gen, rte, shp with
                    | Some _, Some _,    Some _, _
                    | Some _, Some _, _, Some _ ->
                        { filter with
                            Indication = ind
                            Generic = gen
                            Route = rte
                            Shape = shp
                        }
                        |> PrescriptionRule.filter
                        |> Array.collect (fun pr ->
                            pr
                            |> evaluate OrderLogger.logger.Logger
                            |> Array.mapi (fun i r -> (i, r))
                            |> Array.choose (function
                                | i, Ok (ord, pr) ->
                                    let ns =
                                        pr.DoseRule.DoseLimits
                                        |> Array.map (fun dl -> dl.Substance)

                                    let prs, prp, adm =
                                        ord
                                        |> Order.Print.printOrderToMd ns

                                    {
                                        No = i
                                        Indication = pr.DoseRule.Indication
                                        DoseType = pr.DoseRule.DoseType |> DoseType.toString
                                        Name = pr.DoseRule.Generic
                                        Shape = pr.DoseRule.Shape
                                        Route = pr.DoseRule.Route
                                        Prescription = prs |> replace
                                        Preparation =prp |> replace
                                        Administration = adm |> replace
                                        Order = Some ord
                                    }
                                    |> Some

                                | _, Error (_, _, errs) ->
                                    errs
                                    |> List.map string
                                    |> String.concat "\n"
                                    |> printfn "%s"
                                    None
                            )
                        )

                    | _ -> [||]
            }
        | _ ->
            { sc with
                Indications = [||]
                Generics = [||]
                Routes = [||]
                Shapes = [||]
                Scenarios = [||]
            }
