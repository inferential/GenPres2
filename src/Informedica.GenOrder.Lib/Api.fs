namespace Informedica.GenOrder.Lib


module Api =


    open Informedica.ZIndex.Lib
    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    let replace s =
        s
        |> String.replace "[" ""
        |> String.replace "]" ""
        |> String.replace "<" ""
        |> String.replace ">" ""


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
        let rec solve sr pr =
            pr
            |> DrugOrder.createDrugOrder sr
            |> DrugOrder.toOrderDto
            |> Order.Dto.fromDto
            |> Order.solveMinMax false logger
            |> Result.bind (increaseIncrements logger)
            |> function
            | Ok ord ->
                let ord =
                    pr.DoseRule.DoseLimits
                    |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
                    |> Array.fold (fun acc dl ->
                        let sn =
                            dl.DoseLimitTarget
                            |> DoseRule.DoseLimit.substanceDoseLimitTargetToString
                        acc
                        |> Order.setDoseUnit sn dl.DoseUnit
                    ) ord

                let dto = ord |> Order.Dto.toDto

                let compItems =
                    [
                        for c in dto.Orderable.Components do
                                c.ComponentQuantity.Variable.ValsOpt
                                |> Option.map (fun v ->
                                    {|
                                        shapeQty =
                                            v.Value
                                            |> Array.map (fst >> BigRational.parse)
                                        substs =
                                            [
                                                for i in c.Items do
                                                    i.ComponentConcentration.Variable.ValsOpt
                                                    |> Option.map (fun v ->
                                                        {|
                                                            name = i.Name
                                                            qty =
                                                                v.Value
                                                                |> Array.map (fst >> BigRational.parse)
                                                        |}
                                                    )
                                            ]
                                            |> List.choose id
                                    |}
                                )
                    ]
                    |> List.choose id

                let shps =
                    dto.Orderable.Components
                    |> List.choose _.ComponentQuantity.Variable.ValsOpt
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
            | Error (ord, m) -> Error (ord, pr, m)

        if rule.SolutionRules |> Array.isEmpty then [| solve None rule |]
        else
            rule.SolutionRules
            |> Array.map (fun sr -> solve (Some sr) rule)


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


    /// <summary>
    /// Use a Filter and a ScenarioResult to create a new ScenarioResult.
    /// </summary>
    let filter (sc : ScenarioResult) =

        if Env.getItem FilePath.GENPRES_PROD |> Option.isNone ||
           Env.getItem FilePath.GENPRES_PROD |> Option.map ((<>) "1") |> Option.defaultValue false
            then
            let path = $"{__SOURCE_DIRECTORY__}/log.txt"
            OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

        match sc.Patient.Weight, sc.Patient.Height, sc.Patient.Department with
        | Some w, Some h, d when d |> Option.isSome ->

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
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Shape = shp
                    Patient = {
                        Department = d
                        Age = sc.Patient.Age
                        GestAge = sc.Patient.GestAge
                        PMAge = sc.Patient.PMAge
                        Weight = Some w
                        Height = Some h
                        Diagnoses = [||]
                        Gender = sc.Patient.Gender
                        VenousAccess = sc.Patient.VenousAccess
                    }
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
                                        |> Array.choose (fun dl ->
                                            match dl.DoseLimitTarget with
                                            | SubstanceDoseLimitTarget s -> Some s
                                            | _ -> None
                                        )

                                    let useAdjust = pr.DoseRule |> DoseRule.useAdjust

                                    let prs, prp, adm =
                                        ord
                                        |> Order.Print.printOrderToMd useAdjust ns

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
                                        UseAdjust = useAdjust
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


    let calc (dto : Order.Dto.Dto) =
        try
            dto
            |> Order.Dto.fromDto
            |> fun ord ->
                if ord |> Order.isSolved then
                    let dto =
                        ord
                        |> Order.Dto.toDto
                    dto |> Order.Dto.cleanDose

                    dto
                    |> Order.Dto.fromDto
                    |> Order.applyConstraints
                    |> Order.solveMinMax false OrderLogger.logger.Logger
                    |> function
                    | Ok ord ->
                        ord
                        |> Order.minIncrMaxToValues OrderLogger.logger.Logger

                    | Error msgs ->
                        ConsoleWriter.writeErrorMessage $"{msgs}" true false
                        ord
                else
                    ord
                    |> Order.minIncrMaxToValues OrderLogger.logger.Logger
            |> Order.Dto.toDto
            |> Ok
        with
        | e ->
            printfn $"error calculating values from min incr max {e}"
            "error calculating values from min incr max"
            |> Error


    let solve (dto : Order.Dto.Dto) =
        dto
        |> Order.Dto.fromDto
        |> Order.solveOrder false OrderLogger.logger.Logger
        |> Result.map (fun o ->
            o
            |> Order.toString
            |> String.concat "\n"
            |> sprintf "solved order:\n%s"
            |> fun s -> ConsoleWriter.writeInfoMessage s true false

            o
        )
        |> Result.map Order.Dto.toDto


    let getDoseRules filter =
        DoseRule.get ()
        |> DoseRule.filter filter


    let getSolutionRules generic shape route =
        SolutionRule.get ()
        |> Array.filter (fun sr ->
            generic
            |> Option.map ((=) sr.Generic)
            |> Option.defaultValue true &&
            shape
            |> Option.map ((=) sr.Shape)
            |> Option.defaultValue true &&
            route
            |> Option.map ((=) sr.Route)
            |> Option.defaultValue true
        )


    type OrderAgent =
        {
            Start : unit -> unit
            Restart : unit -> unit
            Create : Patient -> ScenarioResult
            Filter : ScenarioResult -> ScenarioResult
            CalcOrder : Order.Dto.Dto -> Result<Order.Dto.Dto,string>
            SolveOrder : Order.Dto.Dto -> Result<Order.Dto.Dto, Order * Informedica.GenSolver.Lib.Types.Exceptions.Message list>
            DoseRules : Filter -> DoseRule array
            SolutionRules : string option -> string option -> string option -> SolutionRule array
        }


    /// The message typ for the OrderAgent.
    /// The order agent will be implement using the MailboxProcessor.
    type OrderAgentMessage =
        | Start of AsyncReplyChannel<unit>
        | Stop of AsyncReplyChannel<unit>
        | Create of Patient * AsyncReplyChannel<ScenarioResult>
        | Filter of ScenarioResult * AsyncReplyChannel<ScenarioResult>
        | Calc of Order.Dto.Dto * AsyncReplyChannel<Result<Order.Dto.Dto, string>>
        | Solve of Order.Dto.Dto * AsyncReplyChannel<Result<Order.Dto.Dto, Order * Informedica.GenSolver.Lib.Types.Exceptions.Message list>>
        | GetDoseRules of Filter * AsyncReplyChannel<DoseRule[]>
        | GetSolutionRules of (string option * string option * string option) * AsyncReplyChannel<SolutionRule[]>


    let private createAgent () = MailboxProcessor.Start(fun inbox ->

        let rec messageLoop() = async {
            let! msg = inbox.Receive()
            match msg with
            | Start reply ->
                // Implement Start logic
                reply.Reply() // Send back the result
                return! messageLoop()

            | Stop reply ->
                // Implement Stop logic
                reply.Reply()
                return ()

            | Create (patient, reply) ->
                // Implement Create logic
                let result = patient |> scenarioResult
                reply.Reply(result)
                return! messageLoop()

            | Filter (scenario, reply) ->
                // Implement Filter logic
                let result = scenario |> filter
                reply.Reply(result)
                return! messageLoop()

            | Calc (orderDto, reply) ->
                // Implement Calc logic
                let result = orderDto |> calc
                reply.Reply(result)
                return! messageLoop()

            | Solve (orderDto, reply) ->
                // Implement Solve logic
                let result = orderDto |> solve
                reply.Reply(result)
                return! messageLoop()

            | GetDoseRules (filter, reply) ->
                // Implement GetDoseRules logic
                let result = filter |> getDoseRules
                reply.Reply(result)
                return! messageLoop()

            | GetSolutionRules (opts, reply) ->
                // Implement GetSolutionRules logic
                let result =
                    let generic, shape, route = opts
                    getSolutionRules generic shape route
                reply.Reply(result)
                return! messageLoop()
        }

        messageLoop()
    )

    let mutable private agent : MailboxProcessor<OrderAgentMessage> option = None


(*
    /// implementation of the OrderAgent
    /// using the MailboxProcessor 'agent'
    let orderAgent : OrderAgent =
        {
            Start =
                fun () ->
                    agent <- createAgent () |> Some
                    match agent with
                    | Some agent ->
                        agent.PostAndAsyncReply(Start)
                        |> Async.RunSynchronously

            Restart =
                fun () ->
                    agent.PostAndAsyncReply(Stop)
                    |> Async.RunSynchronously
                    agent <- createAgent ()
            Create =
                fun patient ->
                    agent.PostAndAsyncReply(fun reply -> Create (patient, reply))
                    |> Async.RunSynchronously
            Filter =
                fun scenario ->
                    agent.PostAndAsyncReply(fun reply -> Filter (scenario, reply))
                    |> Async.RunSynchronously
            CalcOrder =
                fun orderDto ->
                    agent.PostAndAsyncReply(fun reply -> Calc (orderDto, reply))
                    |> Async.RunSynchronously
            SolveOrder =
                fun orderDto ->
                    agent.PostAndAsyncReply(fun reply -> Solve (orderDto, reply))
                    |> Async.RunSynchronously
            DoseRules =
                fun filter ->
                    agent.PostAndAsyncReply(fun reply -> GetDoseRules (filter, reply))
                    |> Async.RunSynchronously
            SolutionRules =
                fun generic shape route ->
                    agent.PostAndAsyncReply(fun reply -> GetSolutionRules ((generic, shape, route), reply))
                    |> Async.RunSynchronously
        }

*)
