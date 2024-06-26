
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Aether"
#r "nuget: Markdig"
#r "nuget: ClosedXML"


#r "../../Informedica.Utils.Lib/bin/Debug/net8.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net8.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net8.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.GenSolver.Lib/bin/Debug/net8.0/Informedica.GenSolver.Lib.dll"
#r "../../Informedica.GenForm.Lib/bin/Debug/net8.0/Informedica.GenForm.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net8.0/Informedica.ZIndex.Lib.dll"

// These can be loaded all at once.

#load "../Types.fs"
#load "../Utils.fs"
#load "../Logging.fs"
#load "../Exceptions.fs"
#load "../WrappedString.fs"
#load "../ValueUnit.fs"
#load "../Variable.fs"
#load "../OrderVariable.fs"
#load "../Solver.fs"


open MathNet.Numerics
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


/// Types and functions that deal with an order.
/// An `Order` models the `Prescription` of an
/// `Orderable` with a `StartStop` start date and
/// stop date.
[<RequireQualifiedAccess>]
module Order =


    open System
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open WrappedString


    /// Utility functions to
    /// enable mapping of a `Variable`s
    /// to an `Order`
    module Mapping =


        let [<Literal>] qty = OrderVariable.Quantity.name
        let [<Literal>] cnc = OrderVariable.Concentration.name
        let [<Literal>] ptm = OrderVariable.PerTime.name
        let [<Literal>] rte = OrderVariable.Rate.name
        let [<Literal>] tot = OrderVariable.Total.name
        let [<Literal>] qtyAdj = OrderVariable.QuantityAdjust.name
        let [<Literal>] ptmAdj = OrderVariable.PerTimeAdjust.name
        let [<Literal>] rteAdj = OrderVariable.RateAdjust.name
        let [<Literal>] totAdj = OrderVariable.TotalAdjust.name
        let [<Literal>] cnt = OrderVariable.Count.name
        let [<Literal>] frq = OrderVariable.Frequency.name
        let [<Literal>] tme = OrderVariable.Time.name
        let [<Literal>] itm = "itm" //Orderable.Literals.item
        let [<Literal>] cmp = "cmp" //Orderable.Literals.comp
        let [<Literal>] orb = "orb" //Orderable.Literals.orderable
        let [<Literal>] dos = "dos" //Orderable.Literals.dose
        let [<Literal>] prs = "prs" //"Prescription"
        let [<Literal>] ord = "ord" // "Order"
        let [<Literal>] adj = "adj" // "Adjust"

        let [<Literal>] discontinuous = 3
        let [<Literal>] continuous = 4
        let [<Literal>] timed = 5
        let [<Literal>] once = 6
        let [<Literal>] onceTimed = 7


        let private getEquations_ indx =
            Web.getDataFromGenPres "Equations"
            |> Array.skip 1
            |> Array.filter (fun xs -> xs[indx] = "x")
            |> Array.map (Array.item 1)
            |> Array.toList


        /// <summary>
        /// Get a string list of Equations and
        /// use an index to filter out the relevant equations
        /// </summary>
        /// <param name="indx">The index to filter the equations</param>
        /// <remarks>
        /// The indx can be 3 for discontinuous equations, 4 for continuous
        /// and 5 for timed equations.
        /// </remarks>
        let getEquations indx =
            indx
            |> Memoization.memoize getEquations_


        /// <summary>
        /// Create an Equations mapping for an `Order`
        /// </summary>
        /// <param name="ord">The Order to Map</param>
        /// <param name="eqs">The equations as a string list</param>
        /// <returns>
        /// A tuple of `SumMapping` and `ProductMapping`
        /// </returns>
        let getEqsMapping (ord: Order) (eqs : string list) =
            let sumEqs =
                eqs
                |> List.filter (String.contains "sum")

            let prodEqs =
                eqs
                |> List.filter (String.contains "sum" >> not)

            let itmEqs =
                prodEqs
                |> List.filter (String.contains "[itm]")

            let cmpEqs =
                prodEqs
                |> List.filter (fun e ->
                    itmEqs
                    |> List.exists ((=) e)
                    |> not &&
                    e.Contains("[cmp]")
                )

            let orbEqs =
                prodEqs
                |> List.filter (fun e ->
                    itmEqs
                    |> List.exists ((=) e)
                    |> not &&
                    cmpEqs
                    |> List.exists((=) e)
                    |> not
                )

            let idN = [ord.Id |> Id.toString] |> Name.create
            let orbN = [ord.Id |> Id.toString; ord.Orderable.Name |> Name.toString] |> Name.create

            ord.Orderable.Components
            |> List.fold (fun acc c ->
                let cmpN =
                    [
                        yield! orbN |> Name.toStringList
                        c.Name |> Name.toString
                    ]
                    |> Name.create

                let itms =
                    c.Items
                    |> List.collect (fun i ->
                        itmEqs
                        |> List.map (fun s ->
                            let itmN =
                                [
                                    yield! cmpN |> Name.toStringList
                                    i.Name |> Name.toString
                                ]
                                |> Name.create
                            s
                            |> String.replace "[cmp]" $"{cmpN |> Name.toString}"
                            |> String.replace "[itm]" $"{itmN |> Name.toString}"
                        )
                    )

                let cmps =
                    cmpEqs
                    |> List.map (String.replace "[cmp]" $"{cmpN |> Name.toString}")

                acc
                |> List.append cmps
                |> List.append itms
            ) []
            |> fun es ->
                let sumEqs =
                    sumEqs
                    |> List.map (fun e ->
                        match e
                              |> String.replace "sum(" ""
                              |> String.replace ")" ""
                              |> String.split " = " with
                        | [lv; rv] ->
                            ord.Orderable.Components
                            |> List.map(fun c ->
                                let cmpN =
                                    [
                                        yield! orbN |> Name.toStringList
                                        c.Name |> Name.toString
                                    ]
                                    |> Name.create

                                rv
                                |> String.replace "[cmp]" $"{cmpN |> Name.toString}"
                            )
                            |> String.concat " + "
                            |> fun s -> $"{lv} = {s}"
                        | _ ->
                            ConsoleWriter.writeErrorMessage
                                $"could not match {e}"
                                true false
                            ""
                    )
                    |> List.filter (String.isNullOrWhiteSpace >> not)
                    |> List.map (String.replace "[orb]" $"{orbN |> Name.toString}")
                    |> SumMapping

                let prodEqs =
                    es
                    |> List.append orbEqs
                    |> List.append es
                    |> List.map (String.replace "[orb]" $"{orbN |> Name.toString}")
                    |> List.map (String.replace "[ord]" $"{idN |> Name.toString}")
                    |> List.distinct
                    |> ProductMapping

                sumEqs, prodEqs



    /// Types and functions to deal
    /// with an `Orderable`, i.e. something
    /// that can be ordered.
    [<RequireQualifiedAccess>]
    module Orderable =


        open Informedica.GenSolver.Lib


        type Name = Types.Name


        /// Contains string constants
        /// to create `Variable` names
        module Literals =

            [<Literal>]
            let item = Mapping.itm
            [<Literal>]
            let comp = Mapping.cmp
            [<Literal>]
            let orderable = Mapping.orb
            [<Literal>]
            let order = Mapping.ord
            [<Literal>]
            let dose = Mapping.dos


        module Dose =

            module Quantity = OrderVariable.Quantity
            module PerTime = OrderVariable.PerTime
            module Rate = OrderVariable.Rate
            module Total = OrderVariable.Total
            module QuantityAdjust = OrderVariable.QuantityAdjust
            module PerTimeAdjust = OrderVariable.PerTimeAdjust
            module RateAdjust = OrderVariable.RateAdjust
            module TotalAdjust = OrderVariable.TotalAdjust


            /// <summary>
            /// Create a `Dose` with
            /// </summary>
            /// <param name="qty">The quantity of the dose</param>
            /// <param name="ptm">The per time of the dose</param>
            /// <param name="rte">The rate of the dose</param>
            /// <param name="tot">The total of the dose</param>
            /// <param name="qty_adj">The quantity adjust of the dose</param>
            /// <param name="ptm_adj">The per time adjust of the dose</param>
            /// <param name="rte_adj">The rate adjust of the dose</param>
            /// <param name="tot_adj">The total adjust of the dose</param>
            let create
                qty
                ptm
                rte
                tot
                qty_adj
                ptm_adj
                rte_adj
                tot_adj =
                {
                    Quantity = qty
                    PerTime = ptm
                    Rate = rte
                    Total = tot
                    QuantityAdjust = qty_adj
                    PerTimeAdjust = ptm_adj
                    RateAdjust = rte_adj
                    TotalAdjust = tot_adj
                }


            /// <summary>
            /// Create a new `Dose` with
            /// </summary>
            /// <param name="n">The name of the dose</param>
            let createNew n =
                let un = Unit.NoUnit
                let n = n |> Name.add Literals.dose

                let qty = Quantity.create n un
                let ptm = PerTime.create n un un
                let rte = Rate.create n un un
                let tot = Total.create n un
                let qty_adj = QuantityAdjust.create n un un
                let rte_adj = RateAdjust.create n un un un
                let ptm_adj = PerTimeAdjust.create n un un un
                let tot_adj = TotalAdjust.create n un un

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Return a Dose as a list of OrderVariables
            /// </summary>
            /// <param name="dos">The dose</param>
            let toOrdVars (dos : Dose) =
                let qty = dos.Quantity |> Quantity.toOrdVar
                let ptm = dos.PerTime |> PerTime.toOrdVar
                let rte = dos.Rate |> Rate.toOrdVar
                let tot = dos.Total |> Total.toOrdVar
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.toOrdVar
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.toOrdVar
                let rte_adj = dos.RateAdjust |> RateAdjust.toOrdVar
                let tot_adj = dos.TotalAdjust |> TotalAdjust.toOrdVar

                [
                    qty
                    ptm
                    rte
                    tot
                    qty_adj
                    ptm_adj
                    rte_adj
                    tot_adj
                ]


            /// <summary>
            /// Create a new Dose from a list of OrderVariables using
            /// an old Dose.
            /// </summary>
            /// <param name="ovars">The list of OrderVariables</param>
            /// <param name="dos">The old Dose</param>
            let fromOrdVars ovars (dos: Dose) =
                let qty = dos.Quantity |> Quantity.fromOrdVar ovars
                let ptm = dos.PerTime |> PerTime.fromOrdVar ovars
                let rte = dos.Rate |> Rate.fromOrdVar ovars
                let tot = dos.Total |> Total.fromOrdVar ovars
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.fromOrdVar ovars
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.fromOrdVar ovars
                let rte_adj = dos.RateAdjust |> RateAdjust.fromOrdVar ovars
                let tot_adj = dos.TotalAdjust |> TotalAdjust.fromOrdVar ovars

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Apply constraints to a Dose
            /// </summary>
            /// <param name="dos">The Dose</param>
            let applyConstraints (dos: Dose) =
                let qty = dos.Quantity |> Quantity.applyConstraints
                let ptm = dos.PerTime |> PerTime.applyConstraints
                let rte = dos.Rate |> Rate.applyConstraints
                let tot = dos.Total |> Total.applyConstraints
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.applyConstraints
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.applyConstraints
                let rte_adj = dos.RateAdjust |> RateAdjust.applyConstraints
                let tot_adj = dos.TotalAdjust |> TotalAdjust.applyConstraints

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Increase the increment of a Dose to a maximum
            /// count using a list of increments.
            /// </summary>
            /// <param name="maxCount">The maximum count</param>
            /// <param name="incrs">The list of increments</param>
            /// <param name="dos">The Dose</param>
            let increaseIncrement maxCount incrs (dos: Dose) =
                let qty = dos.Quantity
                let ptm = dos.PerTime
                let rte = dos.Rate |> Rate.increaseIncrement maxCount incrs
                let tot = dos.Total
                let qty_adj = dos.QuantityAdjust
                let ptm_adj = dos.PerTimeAdjust
                let rte_adj = dos.RateAdjust
                let tot_adj = dos.TotalAdjust

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            let setDoseUnit du (dos : Dose) =
                let qty = dos.Quantity |> Quantity.setDoseUnit du
                let ptm = dos.PerTime |> PerTime.setDoseUnit du
                let rte = dos.Rate |> Rate.setDoseUnit du
                let tot = dos.Total |> Total.setDoseUnit du
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.setDoseUnit du
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.setDoseUnit du
                let rte_adj = dos.RateAdjust |> RateAdjust.setDoseUnit du
                let tot_adj = dos.TotalAdjust |> TotalAdjust.setDoseUnit du

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Create a string list from a Dose where each string is
            /// a variable name with the value range and the Unit
            /// </summary>
            let toString = toOrdVars >> List.map (OrderVariable.toString false)


            module Print =

                let quantityTo toStr (d : Dose) =
                    d.Quantity
                    |> toStr


                let doseQuantityToString prec =
                    quantityTo (Quantity.toValueUnitString prec)


                let doseQuantityToMd prec =
                    quantityTo (Quantity.toValueUnitMarkdown prec)


                let quantityAdjustTo toStr (d : Dose) =
                    d.QuantityAdjust
                    |> toStr

                let doseQuantityAdjustToString prec =
                    quantityAdjustTo (QuantityAdjust.toValueUnitString prec)


                let doseQuantityAdjustToMd prec =
                    quantityAdjustTo (QuantityAdjust.toValueUnitMarkdown prec)


                let perTimeTo toStr (d : Dose) =
                    d.PerTime
                    |> toStr


                let dosePerTimeToString prec =
                    perTimeTo (PerTime.toValueUnitString prec)


                let dosePerTimeToMd prec =
                    perTimeTo (PerTime.toValueUnitMarkdown prec)


                let perTimeAdjustTo toStr (d : Dose) =
                    d.PerTimeAdjust
                    |> toStr


                let dosePerTimeAdjustToString prec =
                    perTimeAdjustTo (PerTimeAdjust.toValueUnitString prec)


                let dosePerTimeAdjustToMd prec =
                    perTimeAdjustTo (PerTimeAdjust.toValueUnitMarkdown prec)


                let rateTo toStr (d : Dose) =
                    d.Rate
                    |> toStr


                let doseRateToString prec =
                    rateTo (Rate.toValueUnitString prec)


                let doseRateToMd prec =
                    rateTo (Rate.toValueUnitMarkdown prec)


                let rateAdjustTo toStr (d : Dose) =
                    d.RateAdjust
                    |> toStr


                let doseRateAdjustToString prec =
                    rateAdjustTo (RateAdjust.toValueUnitString prec)


                let doseRateAdjustToMd prec =
                    rateAdjustTo (RateAdjust.toValueUnitMarkdown prec)



            /// Functions to create a Dose Dto and vice versa.
            module Dto =


                module Units = ValueUnit.Units
                module Quantity = OrderVariable.Quantity
                module QuantityPerTime = OrderVariable.PerTime
                module Rate = OrderVariable.Rate
                module Total = OrderVariable.Total
                module QuantityAdjust = OrderVariable.QuantityAdjust
                module QuantityPerTimeAdjust = OrderVariable.PerTimeAdjust
                module RateAdjust = OrderVariable.RateAdjust
                module TotalAdjust = OrderVariable.TotalAdjust


                type Dto () =
                    member val Quantity = OrderVariable.Dto.dto () with get, set
                    member val PerTime = OrderVariable.Dto.dto () with get, set
                    member val Rate = OrderVariable.Dto.dto () with get, set
                    member val Total = OrderVariable.Dto.dto () with get, set
                    member val QuantityAdjust = OrderVariable.Dto.dto () with get, set
                    member val PerTimeAdjust = OrderVariable.Dto.dto () with get, set
                    member val RateAdjust = OrderVariable.Dto.dto () with get, set
                    member val TotalAdjust = OrderVariable.Dto.dto () with get, set


                let fromDto (dto: Dto) =

                    let qty = dto.Quantity |> Quantity.fromDto
                    let ptm = dto.PerTime |> PerTime.fromDto
                    let rte = dto.Rate |> Rate.fromDto
                    let tot = dto.Total |> Total.fromDto
                    let qty_adj = dto.QuantityAdjust |> QuantityAdjust.fromDto
                    let ptm_adj = dto.PerTimeAdjust |> PerTimeAdjust.fromDto
                    let rte_adj = dto.RateAdjust |> RateAdjust.fromDto
                    let tot_adj = dto.TotalAdjust |> TotalAdjust.fromDto

                    create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


                let toDto (dos : Dose) =
                    let dto = Dto ()

                    dto.Quantity <- dos.Quantity |> Quantity.toDto
                    dto.PerTime <- dos.PerTime |> PerTime.toDto
                    dto.Rate <- dos.Rate |> Rate.toDto
                    dto.Total <- dos.Total |> Total.toDto
                    dto.QuantityAdjust <- dos.QuantityAdjust |> QuantityAdjust.toDto
                    dto.PerTimeAdjust <- dos.PerTimeAdjust |> PerTimeAdjust.toDto
                    dto.RateAdjust <- dos.RateAdjust |> RateAdjust.toDto
                    dto.TotalAdjust <- dos.TotalAdjust |> TotalAdjust.toDto

                    dto


                let dto () = Dto ()

                let clean (dto: Dto) =
                    dto.Quantity |> OrderVariable.Dto.clean
                    dto.PerTime |> OrderVariable.Dto.clean
                    dto.Rate |> OrderVariable.Dto.clean
                    dto.Total |> OrderVariable.Dto.clean
                    dto.QuantityAdjust |> OrderVariable.Dto.clean
                    dto.PerTimeAdjust |> OrderVariable.Dto.clean
                    dto.RateAdjust |> OrderVariable.Dto.clean
                    dto.TotalAdjust |> OrderVariable.Dto.clean


        /// Type and functions that models an
        /// `Order` `Item` that is contained in
        /// a `Component`
        [<RequireQualifiedAccess>]
        module Item =

            module Quantity = OrderVariable.Quantity
            module Concentration = OrderVariable.Concentration
            module Total = OrderVariable.Total
            module Rate = OrderVariable.Rate


            /// <summary>
            /// Create an `Item` with
            /// </summary>
            /// <param name="n">The name of the Item</param>
            /// <param name="cmp_qty">The quantity of the item in the Component</param>
            /// <param name="orb_qty">The quantity of the item in the Orderable</param>
            /// <param name="cmp_cnc">The concentration of the item in the Component</param>
            /// <param name="orb_cnc">The concentration of the item in the Orderable</param>
            /// <param name="dos">The dose of the item</param>
            let create
                n
                cmp_qty
                orb_qty
                cmp_cnc
                orb_cnc
                dos =
                {
                    Name = n
                    ComponentQuantity = cmp_qty
                    OrderableQuantity = orb_qty
                    ComponentConcentration = cmp_cnc
                    OrderableConcentration = orb_cnc
                    Dose = dos
                }


            /// <summary>
            /// Create a new `Item` with
            /// </summary>
            /// <param name="id">The Id of the Item</param>
            /// <param name="orbN">The name of the Orderable</param>
            /// <param name="cmpN">The name of the Component</param>
            /// <param name="itmN">The name of the Item</param>
            let createNew id orbN cmpN itmN =
                let un = Unit.NoUnit
                let n =
                    [ id; orbN; cmpN; itmN ]
                    |> Name.create

                let cmp_qty = let n = n |> Name.add Literals.comp in Quantity.create n un
                let orb_qty = let n = n |> Name.add Literals.orderable in Quantity.create n un
                let cmp_cnc = let n = n |> Name.add Literals.comp in Concentration.create n un un
                let orb_cnc = let n = n |> Name.add Literals.orderable in Concentration.create n un un
                let dos     = Dose.createNew n

                create (itmN |> Name.fromString) cmp_qty orb_qty cmp_cnc orb_cnc dos


            /// Apply **f** to an `item`
            let apply f (itm: Item) = itm |> f


            /// Utility method to facilitate type inference
            let get = apply id


            /// Get the `Name` of an `Item`
            let getName itm = (itm |> get).Name


            /// Get the `Item` dose
            let getDose itm = (itm |> get).Dose


            /// <summary>
            /// Return an Item as a list of OrderVariables
            /// </summary>
            /// <param name="itm">The Item</param>
            let toOrdVars itm =
                let itm_cmp_qty = (itm |> get).ComponentQuantity |> Quantity.toOrdVar
                let itm_orb_qty = itm.OrderableQuantity          |> Quantity.toOrdVar
                let itm_cmp_cnc = itm.ComponentConcentration     |> Concentration.toOrdVar
                let itm_orb_cnc = itm.OrderableConcentration     |> Concentration.toOrdVar

                [
                    itm_cmp_qty
                    itm_orb_qty
                    itm_cmp_cnc
                    itm_orb_cnc
                    yield! itm.Dose |> Dose.toOrdVars
                ]


            /// <summary>
            /// Create a new Item from a list of OrderVariables using
            /// an old Item.
            /// </summary>
            /// <param name="ovars">The list of OrderVariables</param>
            /// <param name="itm">The old Item</param>
            let fromOrdVars ovars itm =
                let cmp_qty = (itm |> get).ComponentQuantity |> Quantity.fromOrdVar ovars
                let orb_qty = itm.OrderableQuantity          |> Quantity.fromOrdVar ovars
                let cmp_cnc = itm.ComponentConcentration     |> Concentration.fromOrdVar ovars
                let orb_cnc = itm.OrderableConcentration     |> Concentration.fromOrdVar ovars
                let dos = itm.Dose |> Dose.fromOrdVars ovars

                create itm.Name cmp_qty orb_qty cmp_cnc orb_cnc dos


            /// <summary>
            /// Apply constraints to an Item
            /// </summary>
            /// <param name="itm">The Item</param>
            let applyConstraints itm =
                let cmp_qty = (itm |> get).ComponentQuantity |> Quantity.applyConstraints
                let orb_qty = itm.OrderableQuantity          |> Quantity.applyConstraints
                let cmp_cnc = itm.ComponentConcentration     |> Concentration.applyConstraints
                let orb_cnc = itm.OrderableConcentration     |> Concentration.applyConstraints
                let dos = itm.Dose |> Dose.applyConstraints

                create itm.Name cmp_qty orb_qty cmp_cnc orb_cnc dos


            let setDoseUnit sn du itm =
                if itm
                   |> getName
                   |> Name.toStringList
                   |> List.exists ((=) sn)
                   |> not then itm
                else
                    { itm with Dose = itm.Dose |> Dose.setDoseUnit du }


            /// <summary>
            /// Create a string list from a Item where each string is
            /// a variable name with the value range and the Unit
            /// </summary>
            let toString = toOrdVars >> List.map (OrderVariable.toString false)


            module Print =


                let concentrationTo get toStr (i : Item) =
                    i
                    |> get
                    |> toStr


                let itemComponentConcentrationToString =
                    concentrationTo
                        (_.ComponentConcentration)
                        (Concentration.toValueUnitString -1)


                let itemComponentConcentrationToMd =
                    concentrationTo
                        (_.ComponentConcentration)
                        (Concentration.toValueUnitMarkdown -1)


                let itemOrderableConcentrationToString =
                    concentrationTo
                        (_.OrderableConcentration)
                        (Concentration.toValueUnitString -1)


                let itemOrderableConcentrationToMd =
                    concentrationTo
                        (_.OrderableConcentration)
                        (Concentration.toValueUnitMarkdown -1)


                let doseTo toStr (i : Item) =
                    i.Dose
                    |> toStr


                let itemDoseQuantityToString =
                    doseTo (Dose.Print.doseQuantityToString 3)


                let itemDoseQuantityToMd =
                    doseTo (Dose.Print.doseQuantityToMd 3)


                let itemDoseQuantityAdjustToString =
                    doseTo (Dose.Print.doseQuantityAdjustToString 3)


                let itemDoseQuantityAdjustToMd =
                    doseTo (Dose.Print.doseQuantityAdjustToMd 3)


                let itemDosePerTimeToString =
                    doseTo (Dose.Print.dosePerTimeToString 3)


                let itemDosePerTimeToMd =
                    doseTo (Dose.Print.dosePerTimeToMd 3)


                let itemDosePerTimeAdjustToString =
                    doseTo (Dose.Print.dosePerTimeAdjustToString 3)


                let itemDosePerTimeAdjustToMd =
                    doseTo (Dose.Print.dosePerTimeAdjustToMd 3)


                let itemDoseRateToString =
                    doseTo (Dose.Print.doseRateToString 3)


                let itemDoseRateToMd =
                    doseTo (Dose.Print.doseRateToMd 3)


                let itemDoseRateAdjustToString =
                    doseTo (Dose.Print.doseRateAdjustToString 3)


                let itemDoseRateAdjustToMd =
                    doseTo (Dose.Print.doseRateAdjustToMd 3)



            /// Functions to create a Item Dto and vice versa.
            module Dto =

                module Units = ValueUnit.Units
                module Id = WrappedString.Id
                module Name = WrappedString.Name
                module Quantity = OrderVariable.Quantity
                module Concentration = OrderVariable.Concentration


                type Dto () =
                    member val Name = "" with get, set
                    member val ComponentQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
                    member val ComponentConcentration = OrderVariable.Dto.dto () with get, set
                    member val OrderableConcentration = OrderVariable.Dto.dto () with get, set
                    member val Dose = Dose.Dto.dto () with get, set


                let fromDto (dto: Dto) =
                    let n = dto.Name |> Name.fromString
                    let cmp_qty = dto.ComponentQuantity |> Quantity.fromDto
                    let orb_qty = dto.OrderableQuantity |> Quantity.fromDto
                    let cmp_cnc = dto.ComponentConcentration |> Concentration.fromDto
                    let orb_cnc = dto.OrderableConcentration |> Concentration.fromDto
                    let dos = dto.Dose |> Dose.Dto.fromDto

                    create n cmp_qty orb_qty cmp_cnc orb_cnc dos


                let toDto (itm : Item) =
                    let dto = Dto ()

                    dto.Name <- itm.Name |> Name.toString
                    dto.ComponentQuantity <-
                        itm.ComponentQuantity
                        |> Quantity.toDto
                    dto.OrderableQuantity <-
                        itm.OrderableQuantity
                        |> Quantity.toDto
                    dto.ComponentConcentration <-
                        itm.ComponentConcentration
                        |> Concentration.toDto
                    dto.OrderableConcentration <-
                        itm.OrderableConcentration
                        |> Concentration.toDto
                    dto.Dose <-
                        itm.Dose |> Dose.Dto.toDto

                    dto


                /// <summary>
                /// Create a new Item Dto
                /// </summary>
                /// <param name="id">The Id of the Item</param>
                /// <param name="orbN">The name of the Orderable</param>
                /// <param name="cmpN">The name of the Component</param>
                /// <param name="itmN">The name of the Item</param>
                let dto id orbN cmpN itmN =
                    createNew id orbN cmpN itmN
                    |> toDto






        /// Types and functions to model a
        /// `Component` in an `Orderable`.
        /// A `Component` contains a list
        /// of `Item`s
        [<RequireQualifiedAccess>]
        module Component =


            module Name = Name
            module Quantity = OrderVariable.Quantity
            module Concentration = OrderVariable.Concentration
            module Count = OrderVariable.Count


            /// <summary>
            /// Create a `Component` with
            /// </summary>
            /// <param name="id">The Id of the Component</param>
            /// <param name="nm">The name of the Component</param>
            /// <param name="sh">The shape of the Component</param>
            /// <param name="cmp_qty">The quantity of the Component</param>
            /// <param name="orb_qty">The quantity of the Component in the Orderable</param>
            /// <param name="orb_cnt">The count of the Component in the Orderable</param>
            /// <param name="ord_qty">The quantity of the Component in the Order</param>
            /// <param name="ord_cnt">The count of the Component in the Order</param>
            /// <param name="orb_cnc">The concentration of the Component in the Orderable</param>
            /// <param name="dos">The dose of the Component</param>
            /// <param name="ii">The list of Items in the Component</param>
            let create
                id
                nm
                sh
                cmp_qty
                orb_qty
                orb_cnt
                ord_qty
                ord_cnt
                orb_cnc
                dos
                ii =
                {
                    Id = id
                    Name = nm
                    Shape = sh
                    ComponentQuantity = cmp_qty
                    OrderableQuantity = orb_qty
                    OrderableCount = orb_cnt
                    OrderQuantity = ord_qty
                    OrderCount = ord_cnt
                    OrderableConcentration = orb_cnc
                    Dose = dos
                    Items = ii
                }


            /// <summary>
            /// Create a new `Component` with
            /// </summary>
            /// <param name="id">The Id of the Component</param>
            /// <param name="orbN">The name of the Orderable</param>
            /// <param name="cmpN">The name of the Component</param>
            /// <param name="sh">The shape of the Component</param>
            let createNew id orbN cmpN sh =
                let un = Unit.NoUnit
                let nm = [ id; orbN; cmpN ] |> Name.create
                let id = Id.create id

                let cmp_qty = let n = nm |> Name.add Literals.comp in Quantity.create n un
                let orb_qty = let n = nm |> Name.add Literals.orderable in Quantity.create n un
                let orb_cnt = let n = nm |> Name.add Literals.orderable in Count.create n
                let ord_qty = let n = nm |> Name.add Literals.order in Quantity.create n un
                let ord_cnt = let n = nm |> Name.add Literals.order in Count.create n
                let orb_cnc = let n = nm |> Name.add Literals.orderable in Concentration.create n un un
                let dos     = Dose.createNew nm

                create id (cmpN |> Name.fromString) sh cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos []


            /// Apply **f** to a `Component` **comp**
            let apply f (comp: Component) = comp |> f


            /// Utility to facilitate type inference
            let get = apply id


            /// Get the name of a `Component`
            let getName cmp = (cmp |> get).Name


            /// Get the `Item`s in an `Component`
            let getItems cmp = (cmp |> get).Items


            /// <summary>
            /// Return a Component as a list of OrderVariables
            /// </summary>
            /// <param name="cmp">The Component</param>
            let toOrdVars cmp =
                let cmp_qty = (cmp |> get).ComponentQuantity |> Quantity.toOrdVar
                let orb_qty = cmp.OrderableQuantity          |> Quantity.toOrdVar
                let orb_cnt = cmp.OrderableCount             |> Count.toOrdVar
                let orb_cnc = cmp.OrderableConcentration     |> Concentration.toOrdVar
                let ord_qty = cmp.OrderQuantity              |> Quantity.toOrdVar
                let ord_cnt = cmp.OrderCount                 |> Count.toOrdVar

                [
                    cmp_qty
                    orb_qty
                    orb_cnt
                    orb_cnc
                    ord_qty
                    ord_cnt
                    yield! cmp.Dose |> Dose.toOrdVars
                    yield! cmp.Items |> List.collect Item.toOrdVars
                ]



            /// <summary>
            /// Create a new Component from a list of OrderVariables using
            /// an old Component.
            /// </summary>
            /// <param name="ovars">The list of OrderVariables</param>
            /// <param name="cmp">The old Component</param>
            let fromOrdVars ovars cmp =
                let cmp_qty = (cmp |> get).ComponentQuantity |> Quantity.fromOrdVar ovars
                let orb_qty = cmp.OrderableQuantity          |> Quantity.fromOrdVar ovars
                let orb_cnt = cmp.OrderableCount             |> Count.fromOrdVar ovars
                let orb_cnc = cmp.OrderableConcentration     |> Concentration.fromOrdVar ovars
                let ord_qty = cmp.OrderQuantity              |> Quantity.fromOrdVar ovars
                let ord_cnt = cmp.OrderCount                 |> Count.fromOrdVar ovars
                let dos = cmp.Dose |> Dose.fromOrdVars ovars

                cmp.Items
                |> List.map (Item.fromOrdVars ovars)
                |> create cmp.Id cmp.Name cmp.Shape cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos


            /// <summary>
            /// Apply constraints to a Component
            /// </summary>
            /// <param name="cmp">The Component</param>
            let applyConstraints cmp =
                let cmp_qty = (cmp |> get).ComponentQuantity |> Quantity.applyConstraints
                let orb_qty = cmp.OrderableQuantity          |> Quantity.applyConstraints
                let orb_cnt = cmp.OrderableCount             |> Count.applyConstraints
                let orb_cnc = cmp.OrderableConcentration     |> Concentration.applyConstraints
                let ord_qty = cmp.OrderQuantity              |> Quantity.applyConstraints
                let ord_cnt = cmp.OrderCount                 |> Count.applyConstraints
                let dos = cmp.Dose |> Dose.applyConstraints

                cmp.Items
                |> List.map Item.applyConstraints
                |> create cmp.Id cmp.Name cmp.Shape cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos



            /// <summary>
            /// Increase the increment of a Component to a maximum
            /// count using a list of increments.
            /// </summary>
            /// <param name="maxCount">The maximum count</param>
            /// <param name="incrs">The list of increments</param>
            /// <param name="cmp">The Component</param>
            let increaseIncrement maxCount incrs cmp =
                let cmp_qty = (cmp |> get).ComponentQuantity
                let orb_qty = cmp.OrderableQuantity |> Quantity.increaseIncrement maxCount incrs
                let orb_cnt = cmp.OrderableCount
                let orb_cnc = cmp.OrderableConcentration
                let ord_qty = cmp.OrderQuantity
                let ord_cnt = cmp.OrderCount
                let dos = cmp.Dose

                cmp.Items
                |> create cmp.Id cmp.Name cmp.Shape cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos


            let setDoseUnit sn du cmp =
                { cmp with
                    Items = cmp.Items |> List.map (Item.setDoseUnit sn du)
                }


            /// <summary>
            /// Create a string list from a Component where each string is
            /// a variable name with the value range and the Unit
            /// </summary>
            let toString = toOrdVars >> List.map (OrderVariable.toString false)



            module Print =


                let quantityTo get toStr (c : Component) =
                    c
                    |> get
                    |> toStr


                let componentQuantityToString =
                    quantityTo
                        (_.ComponentQuantity)
                        (Quantity.toValueUnitString -1)


                let componentQuantityToMd =
                    quantityTo
                        (_.ComponentQuantity)
                        (Quantity.toValueUnitMarkdown -1)


                let componentOrderableQuantityToString =
                    quantityTo
                        (_.OrderableQuantity)
                        (Quantity.toValueUnitString -1)


                let componentOrderableQuantityToMd =
                    quantityTo
                        (_.OrderableQuantity)
                        (Quantity.toValueUnitMarkdown -1)


                let componentOrderQuantityToString prec =
                    quantityTo
                        (_.OrderQuantity)
                        (Quantity.toValueUnitString prec)


                let componentOrderQuantityToMd prec =
                    quantityTo
                        (_.OrderQuantity)
                        (Quantity.toValueUnitMarkdown prec)


                let countTo get toStr (c : Component) =
                    c
                    |> get
                    |> toStr


                let componentOrderableCountToString prec =
                    countTo
                        (_.OrderableCount)
                        (Count.toValueUnitString prec)


                let componentOrderableCountToMd prec =
                    countTo
                        (_.OrderableCount)
                        (Count.toValueUnitMarkdown prec)


                let componentOrderCountToString prec =
                    countTo
                        (_.OrderCount)
                        (Count.toValueUnitString prec)


                let componentOrderCountToMd prec =
                    countTo
                        (_.OrderCount)
                        (Count.toValueUnitMarkdown prec)


                let componentOrderableConcentrationTo toStr (c : Component) =
                    c.OrderableConcentration
                    |> toStr


                let componentOrderableConcentrationToString prec =
                    componentOrderableConcentrationTo (Concentration.toValueUnitString prec)


                let componentOrderableConcentrationToMd prec =
                    componentOrderableConcentrationTo (Concentration.toValueUnitMarkdown prec)


                let doseTo toStr (c : Component) =
                    c.Dose
                    |> toStr


                let componentDoseQuantityToString =
                    doseTo (Dose.Print.doseQuantityToString -1)


                let componentDoseQuantityToMd =
                    doseTo (Dose.Print.doseQuantityToMd -1)


                let componentDoseQuantityAdjustToString prec =
                    doseTo (Dose.Print.doseQuantityAdjustToString prec)


                let componentDoseQuantityAdjustToMd prec =
                    doseTo (Dose.Print.doseQuantityAdjustToMd prec)


                let componentDosePerTimeToString prec =
                    doseTo (Dose.Print.dosePerTimeToString prec)


                let componentDosePerTimeToMd prec =
                    doseTo (Dose.Print.dosePerTimeToMd prec)


                let componentDosePerTimeAdjustToString prec =
                    doseTo (Dose.Print.dosePerTimeAdjustToString prec)


                let componentDosePerTimeAdjustToMd prec =
                    doseTo (Dose.Print.dosePerTimeAdjustToMd prec)


                let componentDoseRateToString prec =
                    doseTo (Dose.Print.doseRateToString prec)


                let componentDoseRateToMd prec =
                    doseTo (Dose.Print.doseRateToMd prec)


                let componentDoseRateAdjustToString prec =
                    doseTo (Dose.Print.doseRateAdjustToString prec)


                let componentDoseRateAdjustToMd prec =
                    doseTo (Dose.Print.doseRateAdjustToMd prec)



            /// Helper functions for the Component Dto
            module Dto =

                module Units = ValueUnit.Units
                module Id = WrappedString.Id
                module Name = WrappedString.Name
                module Quantity = OrderVariable.Quantity
                module Concentration = OrderVariable.Concentration
                module CT = OrderVariable.Count


                type Dto () =
                    member val Id = "" with get, set
                    member val Name = "" with get, set
                    member val Shape = "" with get, set
                    member val ComponentQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderableCount = OrderVariable.Dto.dto () with get, set
                    member val OrderQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderCount = OrderVariable.Dto.dto () with get, set
                    member val OrderableConcentration = OrderVariable.Dto.dto () with get, set
                    member val Dose = Dose.Dto.dto () with get, set
                    member val Items : Item.Dto.Dto list = [] with get, set


                let fromDto (dto: Dto) =

                    let id = dto.Id |> Id.create
                    let n = dto.Name |> Name.fromString
                    let s = dto.Shape
                    let cmp_qty = dto.ComponentQuantity |> Quantity.fromDto
                    let orb_qty = dto.OrderableQuantity |> Quantity.fromDto
                    let orb_cnt = dto.OrderableCount    |> Count.fromDto
                    let orb_cnc = dto.OrderableConcentration |> Concentration.fromDto
                    let ord_qty = dto.OrderQuantity |> Quantity.fromDto
                    let ord_cnt = dto.OrderCount    |> Count.fromDto
                    let ii =
                        dto.Items
                        |> List.map Item.Dto.fromDto

                    let dos = dto.Dose |> Dose.Dto.fromDto

                    create id n s cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos ii


                let toDto (cmp : Component) =
                    let dto = Dto ()

                    dto.Name <- cmp.Name |> Name.toString
                    dto.Shape <- cmp.Shape
                    dto.ComponentQuantity <-
                        cmp.ComponentQuantity
                        |> Quantity.toDto
                    dto.OrderableQuantity <-
                        cmp.OrderableQuantity
                        |> Quantity.toDto
                    dto.OrderableCount <-
                        cmp.OrderableCount
                        |> Count.toDto
                    dto.OrderQuantity <-
                        cmp.OrderQuantity
                        |> Quantity.toDto
                    dto.OrderCount <-
                        cmp.OrderCount
                        |> Count.toDto
                    dto.OrderableConcentration <-
                        cmp.OrderableConcentration
                        |> Concentration.toDto
                    dto.Dose <-
                        cmp.Dose
                        |> Dose.Dto.toDto
                    dto.Items <-
                        cmp.Items
                        |> List.map Item.Dto.toDto

                    dto


                /// <summary>
                /// Create a Component Dto
                /// </summary>
                /// <param name="id">The Id of the Component</param>
                /// <param name="orbN">The name of the Orderable</param>
                /// <param name="cmpN">The name of the Component</param>
                /// <param name="shape">The shape of the Component</param>
                let dto id orbN cmpN shape =
                    createNew id orbN cmpN shape
                    |> toDto




        module Quantity = OrderVariable.Quantity
        module Concentration = OrderVariable.Concentration
        module Count = OrderVariable.Count


        /// <summary>
        /// Create an `Orderable` with
        /// </summary>
        /// <param name="n">The name of the Orderable</param>
        /// <param name="orb_qty">The quantity of the Orderable</param>
        /// <param name="ord_qty">The quantity of the Orderable in the Order</param>
        /// <param name="ord_cnt">The count of the Orderable in the Order</param>
        /// <param name="dos_cnt">The count of the Orderable dose in the Order</param>
        /// <param name="dos">The dose of the Orderable</param>
        /// <param name="cc">The list of Components in the Orderable</param>
        let create
            n
            orb_qty
            ord_qty
            ord_cnt
            dos_cnt
            dos
            cc =
            {
                Name = n
                OrderableQuantity = orb_qty
                OrderQuantity = ord_qty
                OrderCount = ord_cnt
                DoseCount = dos_cnt
                Dose = dos
                Components = cc
            }


        /// <summary>
        /// Create a new `Orderable` with
        /// </summary>
        /// <param name="id">The Id of the Orderable</param>
        /// <param name="orbN">The name of the Orderable</param>
        let createNew id orbN =
            let un = Unit.NoUnit
            let n = [id; orbN] |> Name.create

            let orb_qty = let n = n |> Name.add Literals.orderable in Quantity.create n un
            let ord_qty = let n = n |> Name.add Literals.order in Quantity.create n un
            let ord_cnt = let n = n |> Name.add Literals.order in Count.create n
            let dos_cnt = let n = n |> Name.add Literals.dose in Count.create n
            let dos     = Dose.createNew n

            create (orbN |> Name.fromString) orb_qty ord_qty ord_cnt dos_cnt dos []


        /// Apply **f** to `Orderable` `ord`
        let apply f (orb: Orderable) = orb |> f


        /// Utility function to facilitate type inference
        let get = apply id


        /// Get the name of the `Orderable`
        let getName orb = (orb |> get).Name


        /// Get the Components in an `Orderable`
        let getComponents orb = (orb |> get).Components


        /// Get the `Orderable` dose
        let getDose orb = (orb |> get).Dose



        /// <summary>
        /// Return an Orderable as a list of OrderVariables
        /// </summary>
        /// <param name="orb">The Orderable</param>
        let toOrdVars orb =
            let ord_qty = (orb |> get).OrderQuantity |> Quantity.toOrdVar
            let orb_qty = orb.OrderableQuantity      |> Quantity.toOrdVar
            let ord_cnt = orb.OrderCount             |> Count.toOrdVar
            let dos_cnt = orb.DoseCount              |> Count.toOrdVar

            [
                ord_qty
                orb_qty
                ord_cnt
                dos_cnt
                yield! orb.Dose |> Dose.toOrdVars
                yield! orb.Components |> List.collect Component.toOrdVars
            ]



        /// <summary>
        /// Create a new Orderable from a list of OrderVariables using
        /// an old Orderable.
        /// </summary>
        /// <param name="ovars">The list of OrderVariables</param>
        /// <param name="orb">The old Orderable</param>
        /// <returns>The new Orderable</returns>
        let fromOrdVars ovars orb =
            let ord_qty = (orb |> get).OrderQuantity |> Quantity.fromOrdVar ovars
            let orb_qty = orb.OrderableQuantity      |> Quantity.fromOrdVar ovars
            let ord_cnt = orb.OrderCount             |> Count.fromOrdVar ovars
            let dos_cnt = orb.DoseCount              |> Count.fromOrdVar ovars
            let dos = orb.Dose |> Dose.fromOrdVars ovars

            orb.Components
            |> List.map (Component.fromOrdVars ovars)
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        /// <summary>
        /// Apply constraints to an Orderable
        /// </summary>
        /// <param name="orb">The Orderable</param>
        let applyConstraints orb =
            let ord_qty = (orb |> get).OrderQuantity |> Quantity.applyConstraints
            let orb_qty = orb.OrderableQuantity      |> Quantity.applyConstraints
            let ord_cnt = orb.OrderCount             |> Count.applyConstraints
            let dos_cnt = orb.DoseCount              |> Count.applyConstraints
            let dos = orb.Dose |> Dose.applyConstraints

            orb.Components
            |> List.map Component.applyConstraints
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        /// <summary>
        /// Return a list of strings from an Orderable where each string is
        /// a variable name with the value range and the Unit
        /// </summary>
        let toString = toOrdVars >> List.map (OrderVariable.toString false)


        /// <summary>
        /// Increase the Quantity increment of an Orderable to a maximum
        /// count using a list of increments.
        /// </summary>
        /// <param name="maxCount">The maximum count</param>
        /// <param name="incrs">The list of increments</param>
        /// <param name="orb">The Orderable</param>
        let increaseQuantityIncrement maxCount incrs orb =
            // check if all relevant OrderVariables have an increment
            if
                [
                    orb.OrderableQuantity
                    yield!
                        orb.Components
                        |> List.map _.OrderableQuantity
                ]
                |> List.forall Quantity.hasIncrement
                |> not
                then orb
            else
                // first calculate the minimum increment increase for the orderable and components
                let ord_qty = (orb |> get).OrderQuantity
                let orb_qty = orb.OrderableQuantity |> Quantity.increaseIncrement maxCount incrs
                let ord_cnt = orb.OrderCount
                let dos_cnt = orb.DoseCount
                let dos = orb.Dose //|> Dose.increaseIncrement incr

                orb.Components
                |> List.map (Component.increaseIncrement maxCount incrs)
                |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos

                |> fun newOrb ->
                    [
                        (newOrb.OrderableQuantity |> Quantity.toOrdVar |> OrderVariable.getVar).Values
                        yield! newOrb.Components
                        |> List.map (fun c ->
                            (c.OrderableQuantity |> Quantity.toOrdVar |> OrderVariable.getVar).Values
                        )
                    ]
                    |> List.choose Variable.ValueRange.getIncr
                    |> function
                        | [] -> orb
                        | incrs ->
                            if incrs |> List.length <> ((orb.Components |> List.length) + 1) then orb
                            else
                                let incr =
                                    incrs
                                    |> List.minBy (fun i ->
                                        i
                                        |> Variable.ValueRange.Increment.toValueUnit
                                        |> ValueUnit.getBaseValue
                                    )

                                // apply the minimum increment increase to the orderable and components
                                let ord_qty = (orb |> get).OrderQuantity
                                let orb_qty = orb.OrderableQuantity |> Quantity.increaseIncrement maxCount [incr]
                                let ord_cnt = orb.OrderCount
                                let dos_cnt = orb.DoseCount
                                let dos = orb.Dose //|> Dose.increaseIncrement incr

                                orb.Components
                                |> List.map (Component.increaseIncrement maxCount [incr])
                                |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        /// <summary>
        /// Increase the Rate increment of an Orderable to a maximum
        /// count using a list of increments.
        /// </summary>
        /// <param name="maxCount">The maximum count</param>
        /// <param name="incrs">The list of increments</param>
        /// <param name="orb">The Orderable</param>
        let increaseRateIncrement maxCount incrs orb =
            let ord_qty = (orb |> get).OrderQuantity
            let orb_qty = orb.OrderableQuantity //|> Quantity.increaseIncrement incr
            let ord_cnt = orb.OrderCount
            let dos_cnt = orb.DoseCount
            let dos = orb.Dose |> Dose.increaseIncrement maxCount incrs

            orb.Components
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        let setDoseUnit sn du orb =
            { orb with
                Components = orb.Components |> List.map (Component.setDoseUnit sn du)
            }


        module Print =


            let quantityTo get toStr (o : Orderable) =
                o
                |> get
                |> toStr


            let orderableQuantityToString =
                quantityTo
                    (_.OrderableQuantity)
                    (Quantity.toValueUnitString -1)


            let orderableQuantityToMd =
                quantityTo
                    (_.OrderableQuantity)
                    (Quantity.toValueUnitMarkdown -1)


            let orderQuantityToString prec =
                quantityTo
                    (_.OrderQuantity)
                    (Quantity.toValueUnitString prec)


            let orderQuantityToMd prec =
                quantityTo
                    (_.OrderQuantity)
                    (Quantity.toValueUnitMarkdown prec)


            let countTo get toStr (o : Orderable) =
                o
                |> get
                |> toStr


            let orderCountToString prec =
                countTo
                    (_.OrderCount)
                    (Count.toValueUnitString prec)


            let orderCountToMd prec =
                countTo
                    (_.OrderCount)
                    (Count.toValueUnitMarkdown prec)


            let doseTo toStr (o : Orderable) =
                o.Dose
                |> toStr


            let doseQuantityToString =
                doseTo (Dose.Print.doseQuantityToString -1)


            let doseQuantityToMd =
                doseTo (Dose.Print.doseQuantityToMd -1)


            let doseQuantityAdjustToString prec =
                doseTo (Dose.Print.doseQuantityAdjustToString prec)


            let doseQuantityAdjustToMd prec =
                doseTo (Dose.Print.doseQuantityAdjustToMd prec)


            let dosePerTimeToString prec =
                doseTo (Dose.Print.dosePerTimeToString prec)


            let dosePerTimeToMd prec =
                doseTo (Dose.Print.dosePerTimeToMd prec)


            let dosePerTimeAdjustToString prec =
                doseTo (Dose.Print.dosePerTimeAdjustToString prec)


            let dosePerTimeAdjustToMd prec =
                doseTo (Dose.Print.dosePerTimeAdjustToMd prec)


            let doseRateToString =
                doseTo (Dose.Print.doseRateToString -1)


            let doseRateToMd =
                doseTo (Dose.Print.doseRateToMd -1)


            let doseRateAdjustToString prec =
                doseTo (Dose.Print.doseRateAdjustToString prec)


            let doseRateAdjustToMd prec =
                doseTo (Dose.Print.doseRateAdjustToMd prec)



        /// Helper functions for the Orderable Dto
        module Dto =

            module Units = ValueUnit.Units
            module Id = WrappedString.Id
            module Name = WrappedString.Name
            module Quantity = OrderVariable.Quantity
            module Concentration = OrderVariable.Concentration
            module CT = OrderVariable.Count


            type Dto () =
                member val Name = "" with get, set
                member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
                member val OrderQuantity = OrderVariable.Dto.dto () with get, set
                member val OrderCount = OrderVariable.Dto.dto () with get, set
                member val DoseCount = OrderVariable.Dto.dto () with get, set
                member val Dose = Dose.Dto.dto () with get, set
                member val Components : Component.Dto.Dto list = [] with get, set


            let fromDto (dto: Dto) =
                let n = dto.Name |> Name.fromString

                let orb_qty = dto.OrderableQuantity |> Quantity.fromDto
                let ord_qty = dto.OrderQuantity     |> Quantity.fromDto
                let ord_cnt = dto.OrderCount        |> Count.fromDto
                let dos_cnt = dto.DoseCount         |> Count.fromDto

                let cc =
                    dto.Components
                    |> List.map Component.Dto.fromDto

                let dos = dto.Dose |> Dose.Dto.fromDto

                create n orb_qty ord_qty ord_cnt dos_cnt dos cc


            let toDto (orb : Orderable) =
                let dto = Dto ()

                dto.Name <- orb.Name |> Name.toString
                dto.OrderableQuantity <-
                    orb.OrderableQuantity
                    |> Quantity.toDto
                dto.OrderQuantity <-
                    orb.OrderQuantity
                    |> Quantity.toDto
                dto.OrderCount <-
                    orb.OrderCount
                    |> Count.toDto
                dto.DoseCount <-
                    orb.DoseCount
                    |> Count.toDto
                dto.Dose <-
                    orb.Dose
                    |> Dose.Dto.toDto
                dto.Components <-
                    orb.Components
                    |> List.map Component.Dto.toDto

                dto


            /// <summary>
            /// Create a new Orderable Dto
            /// </summary>
            /// <param name="id">The Id of the Orderable</param>
            /// <param name="orbN">The name of the Orderable</param>
            let dto id orbN =
                createNew id orbN
                |> toDto


    [<RequireQualifiedAccess>]
    module Prescription =


        module Frequency = OrderVariable.Frequency
        module Time = OrderVariable.Time


        /// <summary>
        /// Create a Frequency and Time
        /// </summary>
        /// <param name="tu1">The frequency time unit</param>
        /// <param name="tu2">The time unit</param>
        /// <param name="n">The name of the Frequency and Time</param>
        let freqTime tu1 tu2 n =  (Frequency.create n tu1, Time.create n tu2)


        /// Create a Once `Prescription`
        let once tu1 tu2 n =
            let _, _ = n |> freqTime tu1 tu2 in Once


        /// Create a OnceTimed `Prescription`
        let onceTimed tu1 tu2 n =
            let _, tme = n |> freqTime tu1 tu2 in tme |> OnceTimed


        /// Create a Continuous `Prescription`
        let continuous tu1 tu2 n =
            let _, _ = n |> freqTime tu1 tu2 in Continuous


        /// Create a Discontinuous `Prescription`
        let discontinuous tu1 tu2 n =
            let frq, _ = n |> freqTime tu1 tu2 in frq |> Discontinuous


        /// Create a Timed `Prescription`
        let timed tu1 tu2 n =
            let frq, tme = n |> freqTime tu1 tu2 in (frq, tme) |> Timed


        /// Check whether a `Prescription` is Continuous
        let isContinuous = function | Continuous -> true | _ -> false


        /// Check whether a `Prescription` is Timed
        let isTimed = function | Timed _ -> true | _ -> false


        /// <summary>
        /// Return a Prescription as a Frequency OrderVariable option
        /// and a Time OrderVariable option
        /// </summary>
        /// <param name="prs">The Prescription</param>
        let toOrdVars prs =
            match prs with
            | Once
            | Continuous -> None, None
            | OnceTimed tme ->
                None, tme |> Time.toOrdVar |> Some
            | Discontinuous frq ->
                frq |> Frequency.toOrdVar |> Some, None
            | Timed(frq, tme)     ->
                frq |> Frequency.toOrdVar |> Some, tme |> Time.toOrdVar |> Some


        /// <summary>
        /// Create a new Prescription from a list of OrderVariables using
        /// an old Prescription.
        /// </summary>
        /// <param name="ovars">The list of OrderVariables</param>
        /// <param name="prs">The old Prescription</param>
        let fromOrdVars ovars prs =
            match prs with
            | Once
            | Continuous -> prs
            | OnceTimed tme ->
                tme |> Time.fromOrdVar ovars |> OnceTimed
            | Discontinuous frq ->
                frq |> Frequency.fromOrdVar ovars |> Discontinuous
            | Timed(frq, tme)     ->
                (frq |> Frequency.fromOrdVar ovars,
                tme |> Time.fromOrdVar ovars)
                |> Timed


        /// <summary>
        /// Apply constraints to a Prescription
        /// </summary>
        let applyConstraints prs =
            match prs with
            | Once
            | Continuous -> prs
            | OnceTimed tme ->
                tme |> Time.applyConstraints |> OnceTimed
            | Discontinuous frq ->
                frq |> Frequency.applyConstraints |> Discontinuous
            | Timed(frq, tme)     ->
                (frq |> Frequency.applyConstraints,
                tme |> Time.applyConstraints)
                |> Timed


        let setDoseUnit sn du ord =
            { ord with
                Orderable = ord.Orderable |> Orderable.setDoseUnit sn du
            }


        /// <summary>
        /// Return a list of strings from a Prescription where each string is
        /// a variable name with the value range and the Unit
        /// </summary>
        let toString (prs: Prescription) =
                match prs with
                | Once -> ["eenmalig"]
                | Continuous -> ["continu"]
                | OnceTimed tme -> [tme |> Time.toString]
                | Discontinuous frq -> [frq |> Frequency.toString]
                | Timed(frq, tme)     -> [frq |> Frequency.toString; tme |> Time.toString]



        module Print =


                let frequencyTo toStr (p : Prescription) =
                    match p with
                    | Discontinuous frq -> frq |> toStr
                    | _ -> ""


                let frequencyToString =
                    frequencyTo (Frequency.toValueUnitString -1)


                let frequencyToMd =
                    frequencyTo (Frequency.toValueUnitMarkdown -1)


                let timeTo toStr (p : Prescription) =
                    match p with
                    | OnceTimed tme -> tme |> toStr
                    | Timed (_, tme) -> tme |> toStr
                    | _ -> ""


                let timeToString prec =
                    timeTo (Time.toValueUnitString prec)


                let timeToMd prec =
                    timeTo (Time.toValueUnitMarkdown prec)


                let prescriptionToString (p : Prescription) =
                    match p with
                    | Once -> "eenmalig"
                    | Continuous -> "continu"
                    | OnceTimed _ -> p |> timeToMd -1
                    | Discontinuous _ -> p |> frequencyToString
                    | Timed _     -> $"{p |> frequencyToString} {p |> timeToString -1}"


                let prescriptionToMd (p : Prescription) =
                    match p with
                    | Once -> "eenmalig"
                    | Continuous -> "continu"
                    | OnceTimed _ -> p |> timeToMd -1
                    | Discontinuous _ -> p |> frequencyToMd
                    | Timed _     -> $"{p |> frequencyToMd} {p |> timeToMd -1}"



        /// Helper functions for the Prescription Dto
        module Dto =


            module Units = ValueUnit.Units
            module Id = WrappedString.Id
            module NM = Name


            type Dto () =
                member val IsOnce = false with get, set
                member val IsOnceTimed = false with get, set
                member val IsContinuous = false with get, set
                member val IsDiscontinuous = false with get, set
                member val IsTimed = false with get, set
                member val Frequency = OrderVariable.Dto.dto () with get, set
                member val Time = OrderVariable.Dto.dto () with get, set


            let fromDto (dto : Dto) =
                match dto.IsOnce,
                      dto.IsOnceTimed,
                      dto.IsContinuous,
                      dto.IsDiscontinuous,
                      dto.IsTimed with
                | false, false, true,  false, false -> Continuous
                | false, false, false, true,  false ->
                    dto.Frequency
                    |> Frequency.fromDto
                    |> Discontinuous
                | false, false, false, false, true  ->
                    (dto.Frequency |> Frequency.fromDto, dto.Time |> Time.fromDto)
                    |> Timed
                | true,  false, false, false, false -> Once
                | false, true,  false, false, false ->
                    dto.Time
                    |> Time.fromDto
                    |> OnceTimed
                | _ -> exn "dto is neither or both process, continuous, discontinuous or timed"
                       |> raise


            let toDto pres =
                let dto = Dto ()

                match pres with
                | Once -> dto.IsOnce <- true
                | Continuous -> dto.IsContinuous <- true
                | OnceTimed time ->
                    dto.IsOnceTimed <- true
                    dto.Time <- time |> Time.toDto
                | Discontinuous freq ->
                    dto.IsDiscontinuous <- true
                    dto.Frequency <- freq |> Frequency.toDto
                | Timed (freq, time) ->
                    dto.IsTimed   <- true
                    dto.Frequency <- freq |> Frequency.toDto
                    dto.Time      <- time |> Time.toDto

                dto


            /// <summary>
            /// Create a Prescription Dto
            /// </summary>
            /// <param name="n">The name of the Prescription</param>
            /// <remarks>
            /// Defaults to a Discontinuous Prescription
            /// </remarks>
            let dto n =
                let dto  = Dto ()
                let f, t =
                    n
                    |> Name.fromString
                    |> freqTime Unit.NoUnit Unit.NoUnit

                dto.Frequency <- f |> Frequency.toDto
                dto.Time <- t |> Time.toDto
                dto.IsDiscontinuous <- true

                dto


            /// Make the Prescription Dto Once
            let setToOnce (dto : Dto) =
                dto.IsOnce <- true
                dto.IsOnceTimed <- false
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- false
                dto.IsTimed <- false

                dto


            /// Make the Prescription Dto OnceTimed
            let setToOnceTimed (dto : Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- true
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- false
                dto.IsTimed <- false

                dto


            /// Make the Prescription Dto Continuous
            let setToContinuous (dto : Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- false
                dto.IsContinuous <- true
                dto.IsDiscontinuous <- false
                dto.IsTimed <- false

                dto


            /// Make the Prescription Dto Discontinuous
            let setToDiscontinuous (dto : Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- false
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- true
                dto.IsTimed <- false

                dto


            /// Make the Prescription Dto Timed
            let setToTimed (dto : Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- false
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- false
                dto.IsTimed <- true

                dto



    /// Types and functions that
    /// model a start and stop date time
    /// of an `Order`
    [<RequireQualifiedAccess>]
    module StartStop =


        /// Get the string representation of a `StartStop`
        let toString startStop =
            match startStop with
            | Start dt ->
                dt
                |> DateTime.formattedString "dd-MM-yy"
                |> sprintf "%s"
            | StartStop (start, stop) ->
                stop
                |> DateTime.formattedString "dd-MM-yy"
                |> sprintf "%s - %s" (start |> DateTime.formattedString "dd-MM-yy")


    [<RequireQualifiedAccess>]
    module OrderType =

        let toString = function
            | AnyOrder -> $"{AnyOrder}"
            | OnceOrder -> $"{OnceOrder}"
            | OnceTimedOrder -> $"{OnceTimedOrder}"
            | ProcessOrder -> $"{ProcessOrder}"
            | ContinuousOrder -> $"{ContinuousOrder}"
            | DiscontinuousOrder -> $"{DiscontinuousOrder}"
            | TimedOrder -> $"{TimedOrder}"

        let map s =
            match s with
            | _ when s = "eenmalig" -> OnceOrder
            | _ when s = "eenmalig inlooptijd" -> OnceTimedOrder
            | _ when s = "discontinu" -> DiscontinuousOrder
            | _ when s = "continu" -> ContinuousOrder
            | _ when s = "inlooptijd" -> TimedOrder
            | _ -> DiscontinuousOrder


    open MathNet.Numerics

    module Variable = Informedica.GenSolver.Lib.Variable
    module ValueRange = Variable.ValueRange
    module Equation = Informedica.GenSolver.Lib.Equation
    module Property = ValueRange.Property
    module Quantity = OrderVariable.Quantity
    module QuantityAdjust = OrderVariable.QuantityAdjust
    module Frequency = OrderVariable.Frequency
    module PerTime = OrderVariable.PerTime
    module PerTimeAdjust = OrderVariable.PerTimeAdjust
    module Concentration = OrderVariable.Concentration
    module Rate = OrderVariable.Rate
    module RateAdjust = OrderVariable.RateAdjust
    module Time = OrderVariable.Time
    module Units = ValueUnit.Units
    module Dose = Orderable.Dose


    type Equation = Informedica.GenSolver.Lib.Types.Equation


    /// Apply `f` to `Order` `ord`
    let apply f (ord: Order) = ord |> f


    /// Utility function to facilitate type inference
    let get = apply id


    /// Get the order id
    let getId ord = (ord |> get).Id


    /// <summary>
    /// Create an `Order` with
    /// </summary>
    /// <param name="id">The id of the Order</param>
    /// <param name="adj_qty">The adjust quantity of the Order</param>
    /// <param name="orb">The Orderable of the Order</param>
    /// <param name="prs">The Prescription of the Order</param>
    /// <param name="rte">The Route of the Order</param>
    /// <param name="tme">The Time of the Order</param>
    /// <param name="sts">The StartStop of the Order</param>
    let create id adj_qty orb prs rte tme sts =
        {
            Id = id
            Adjust = adj_qty
            Orderable = orb
            Prescription = prs
            Route = rte
            Duration = tme
            StartStop = sts
        }


    /// <summary>
    /// Create a new `Order` with
    /// </summary>
    /// <param name="id">The id of the Order</param>
    /// <param name="orbN">The name of the Orderable</param>
    /// <param name="str_prs">A function to create a Prescription with a Name</param>
    /// <param name="route">The Route of the Order</param>
    let createNew id orbN str_prs route =
        let orb = Orderable.createNew id orbN
        let n = [id] |> Name.create

        let adj =
            Quantity.create (n |> Name.add Mapping.adj) Unit.NoUnit

        let tme =
            Time.create (n |> Name.add Mapping.ord) Unit.NoUnit

        let prs =
            n
            |> Name.add Mapping.prs
            |> str_prs

        let sts = DateTime.Now  |> StartStop.Start

        create (id |> Id.create) adj orb prs route tme sts


    /// Get the Adjust quantity of an `Order`
    let getAdjust ord = (ord |> get).Adjust


    /// Get the Orderable of an `Order`
    let getOrderable ord = (ord |> get).Orderable


    /// <summary>
    /// Return an Order as a list of strings where each string is
    /// a variable name with the value range and the Unit
    /// </summary>
    let toString (ord: Order) =
        [ ord.Adjust |> Quantity.toString ]
        |> List.append (Orderable.Literals.orderable::(ord.Orderable |> Orderable.toString))
        |> List.append ("Prescription"::(ord.Prescription |> Prescription.toString))
        |> List.append ("Route"::[ord.Route])
        |> List.filter (String.isNullOrWhiteSpace >> not)


    /// <summary>
    /// Return an Order as a list of OrderVariables
    /// </summary>
    let toOrdVars (ord : Order) =
        let adj_qty = ord.Adjust |> Quantity.toOrdVar
        let ord_tme = ord.Duration |> Time.toOrdVar

        let prs_vars =
            ord.Prescription
            |> Prescription.toOrdVars
            |> fun  (f, t) ->
                [f; t]
                |> List.choose id
        [
            adj_qty
            ord_tme
            yield! prs_vars
            yield! ord.Orderable |> Orderable.toOrdVars
        ]


    /// <summary>
    /// Create a new Order from a list of OrderVariables using
    /// an old Order.
    /// </summary>
    /// <param name="ovars">The list of OrderVariables</param>
    /// <param name="ord">The old Order</param>
    let fromOrdVars ovars (ord : Order) =
        { ord with
            Adjust = ord.Adjust |> Quantity.fromOrdVar ovars
            Duration = ord.Duration |> Time.fromOrdVar ovars
            Prescription = ord.Prescription |> Prescription.fromOrdVars ovars
            Orderable = ord.Orderable |> Orderable.fromOrdVars ovars
        }


    /// <summary>
    /// Apply constraints to an Order
    /// </summary>
    /// <param name="ord">The Order</param>
    let applyConstraints (ord : Order) =
        try
            { ord with
                Adjust = ord.Adjust |> Quantity.applyConstraints
                Duration = ord.Duration |> Time.applyConstraints
                Prescription = ord.Prescription |> Prescription.applyConstraints
                Orderable = ord.Orderable |> Orderable.applyConstraints
            }
        with
        | _ ->
            let s = ord |> toString |> String.concat "\n"
            ConsoleWriter.writeErrorMessage
                $"couldn't apply constraints:\n{s}"
                true false
            reraise()


    let isSolved (ord: Order) =
        let qty =
              ord.Orderable.Dose.Quantity
              |> Quantity.toOrdVar
              |> OrderVariable.isSolved
        let rte =
              ord.Orderable.Dose.Rate
              |> Rate.toOrdVar
              |> OrderVariable.isSolved
        qty || rte


    /// <summary>
    /// Increase the Quantity increment of an Order to a maximum
    /// count using a list of increments.
    /// </summary>
    /// <param name="maxCount">The maximum count</param>
    /// <param name="incrs">The list of increments</param>
    /// <param name="ord">The Order</param>
    let increaseQuantityIncrement maxCount incrs (ord : Order) =
        { ord with
            Orderable =
                ord.Orderable
                |> Orderable.increaseQuantityIncrement
                       maxCount
                       incrs
        }


    /// <summary>
    /// Increase the Rate increment of an Order to a maximum
    /// count using a list of increments.
    /// </summary>
    /// <param name="maxCount">The maximum count</param>
    /// <param name="incrs">The list of increments</param>
    /// <param name="ord">The Order</param>
    let increaseRateIncrement maxCount incrs (ord : Order) =
        { ord with
            Orderable =
                ord.Orderable
                |> Orderable.increaseRateIncrement maxCount incrs
        }


    /// <summary>
    /// Map an Order to a list of Equations using a Product Equation
    /// mapping and a Sum Equation mapping
    /// </summary>
    /// <param name="eqMapping">The Product Equation mapping and the Sum Equation mapping</param>
    /// <param name="ord">The Order</param>
    /// <returns>A list of OrderEquations</returns>
    let mapToOrderEquations eqMapping (ord: Order)  =
        let ovars = ord |> toOrdVars

        let map repl eqMapping =
            let eqs, c =
                match eqMapping with
                | SumMapping eqs -> eqs, OrderSumEquation
                | ProductMapping eqs -> eqs, OrderProductEquation
            eqs
            |> List.map (String.replace "=" repl)
            |> List.map (String.split repl >> List.map String.trim)
            |> List.map (fun xs ->
                match xs with
                | h::rest ->
                    let h =
                        try
                            ovars |> List.find (fun v -> v.Variable.Name |> Name.toString = h)
                        with
                        | _ -> failwith $"cannot find {h} in {ovars}"
                    let rest =
                        rest
                        |> List.map (fun s ->
                            try
                                ovars |> List.find (fun v -> v.Variable.Name |> Name.toString = s)
                            with
                            | _ -> failwith $"cannot find {s} in {ovars}"
                        )
                    (h, rest) |> c
                | _ -> failwith $"cannot map {eqs}"
            )

        let sumEqs, prodEqs = eqMapping

        sumEqs
        |> map "+"
        |> List.append (prodEqs |> map "*")


    /// <summary>
    /// Map a list of OrderEquations to an Order
    /// </summary>
    /// <param name="ord">The Order</param>
    /// <param name="eqs">The list of OrderEquations</param>
    let mapFromOrderEquations (ord: Order) eqs =
        let ovars =
            eqs
            |> List.collect (fun e ->
                match e with
                | OrderProductEquation (y, xs)
                | OrderSumEquation (y, xs) -> y::xs
            )
            |> List.distinct
            |> List.map OrderVariable.setUnit

        ord
        |> fromOrdVars ovars


    /// <summary>
    /// Solve an Order
    /// </summary>
    /// <param name="minMax">Whether to solve only for the minimum or maximum</param>
    /// <param name="printErr">Whether to print the error</param>
    /// <param name="logger">The logger</param>
    /// <param name="ord">The Order</param>
    /// <returns>A Result with the Order or a list error messages</returns>
    /// <raises>Any exception raised by the solver</raises>
    let solve minMax printErr logger (ord: Order) =
        let ord =
            if minMax then ord |> applyConstraints
            else ord

        let mapping =
            match ord.Prescription with
            | Once -> Mapping.once
            | Continuous -> Mapping.continuous
            | OnceTimed _ -> Mapping.onceTimed
            | Discontinuous _ -> Mapping.discontinuous
            | Timed _ -> Mapping.timed
            |> Mapping.getEquations
            |> Mapping.getEqsMapping ord

        let oEqs =
            ord
            |> mapToOrderEquations mapping

        try
            oEqs
            |> Solver.mapToSolverEqs
            |> fun eqs ->
                if minMax then eqs |> Solver.solveMinMax logger
                else eqs |> Solver.solve logger
            |> function
            | Ok eqs ->
                eqs
                |> Solver.mapToOrderEqs oEqs
                |> mapFromOrderEquations ord
                |> Ok
            | Error (eqs, m) ->
                eqs
                |> Solver.mapToOrderEqs oEqs
                |> mapFromOrderEquations ord
                |> fun eqs -> Error (eqs, m)

        with
        | e ->
            if printErr then
                oEqs
                |> mapFromOrderEquations ord
                |> toString
                |> List.iteri (printfn "%i. %s")

            raise e


    /// <summary>
    /// Solve an Order for only the minimum and maximum values
    /// </summary>
    /// <param name="printErr">Whether to print the error</param>
    /// <param name="logger">The logger</param>
    let solveMinMax printErr logger = solve true printErr logger


    /// <summary>
    /// Solve an Order for all values
    /// </summary>
    /// <param name="printErr">Whether to print the error</param>
    /// <param name="logger">The logger</param>
    let solveOrder printErr logger = solve false printErr logger


    /// <summary>
    /// Loop through all the OrderVariables in an Order to
    /// turn min incr max to values and subsequently solve the Order.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="ord">The Order</param>
    let minIncrMaxToValues logger (ord: Order) =
        let rec loop runAgain ord =
            if not runAgain then ord
            else
                let mutable flag = false
                let ovars =
                    ord
                    |> toOrdVars
                    |> List.map (fun ovar ->
                        if flag ||
                           ovar.Constraints.Incr |> Option.isNone ||
                           ovar.Variable.Values |> ValueRange.isMinIncrMax |> not then ovar
                        else
                            flag <- true
                            let n =
                                match ord.Prescription with
                                | OnceTimed _ -> 5
                                | Once -> 50
                                | Continuous -> 100
                                | Discontinuous _ -> 50
                                | Timed _ -> 5

                            ovar
                            |> OrderVariable.minIncrMaxToValues n
                    )
                if not flag then ord
                else
                    ord
                    |> fromOrdVars ovars
                    |> solveOrder false logger // could possible restrict to solve variable
                    |> function
                        | Ok ord -> loop flag ord
                        | Error _ -> ord

        loop true ord


    /// <summary>
    /// Increase the Orderable Quantity Increment of an Order.
    /// This allows speedy calculation by avoiding large amount
    /// of possible values.
    /// </summary>
    /// <param name="logger">The OrderLogger to use</param>
    /// <param name="maxQtyCount">The maximum count of the Orderable Quantity</param>
    /// <param name="maxRateCount">The maximum count of the Rate</param>
    /// <param name="ord">The Order to increase the increment of</param>
    let increaseIncrements logger maxQtyCount maxRateCount (ord : Order) =
        if ord.Prescription |> Prescription.isContinuous then ord
        else
            let orbQty = ord.Orderable.OrderableQuantity |> Quantity.toOrdVar
            // the increments used to increase
            let incrs u =
                [ 1N/20N; 1N/10N; 1N/2N; 1N; 5N; 10N; 20N ]
                |> List.map (ValueUnit.singleWithUnit u)
                |> List.map ValueRange.Increment.create
            // only increase incr for volume units
            if orbQty.Variable
               |> Variable.getUnit
               |> Option.map (ValueUnit.Group.unitToGroup >> ((=) Group.VolumeGroup) >> not)
               |> Option.defaultValue false then ord
            else
                ord
                |> increaseQuantityIncrement maxQtyCount (incrs Units.Volume.milliLiter)

            |> increaseRateIncrement
                maxRateCount
                (incrs (Units.Volume.milliLiter |> Units.per Units.Time.hour))
            |> solveMinMax false logger
            |> function
            | Error (_, errs) ->
                errs
                |> List.iter (fun e ->
                    ConsoleWriter.writeErrorMessage
                        $"{e}"
                        true
                        false
                )
                ord // original order
            | Ok ord ->
                ConsoleWriter.writeInfoMessage
                    $"""=== solved order with increased increment === {ord |> toString |> String.concat "\n"}"""
                    true
                    false

                ord // increased increment order
                |> solveOrder false logger

                |> function
                | Error (_, errs) ->
                    errs
                    |> List.iter (fun e ->
                        ConsoleWriter.writeErrorMessage
                            $"{e}"
                            true
                            false
                    )
                    ord // increased increment order
                | Ok ord ->
                    let s = ord |> toString |> String.concat "\n"
                    ConsoleWriter.writeInfoMessage
                        $"solved order with increased increment and values:\n {s}"
                        true
                        false

                    ord // calculated order
        |> Ok


    let setDoseUnit sn du ord =
        { ord with Orderable = ord.Orderable |> Orderable.setDoseUnit sn du }


    module Print =

        open Informedica.GenOrder.Lib


        let itemConcentrationTo toStr (c : Component) =
            c.Items
            |> Seq.map (fun i ->
                i.ComponentConcentration
                |> toStr
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace then ""
                    else
                        $"%s{s} {i.Name |> Name.toString}"
            )
            |> String.concat " + "


        let itemConcentrationToString =
            itemConcentrationTo (Concentration.toValueUnitString -1)


        let itemConcentrationToMd =
            itemConcentrationTo (Concentration.toValueUnitMarkdown -1)


        let componentQuantityTo toStr itemConcTo (o : Order) =
            o.Orderable.Components
            |> Seq.map (fun c ->
                c.OrderableQuantity
                |> toStr
                |> fun q ->
                    let s =
                        c
                        |> itemConcTo
                        |> String.trim
                        |> fun s ->
                            if s |> String.isNullOrWhiteSpace then ""
                            else
                                $" ({s})"
                    match q, s with
                    | _ when q |> String.notEmpty && s |> String.notEmpty
                        -> $"{q} {c.Shape} {s}"
                    | _ when s |> String.notEmpty -> $"{c.Shape} {s}"
                    | _ when q |> String.notEmpty -> $"{q} {c.Shape}"
                    | _ -> ""
            )
            |> Seq.filter String.notEmpty
            |> String.concat " + "


        let componentQuantityToString =
            componentQuantityTo
                (Quantity.toValueUnitString -1)
                itemConcentrationToString


        let componentQuantityToMd =
            componentQuantityTo
                (Quantity.toValueUnitMarkdown -1)
                itemConcentrationToMd


        let orderableDoseQuantityTo toStr (o: Order) =
            o.Orderable.Dose.Quantity
            |> toStr


        let orderableDoseQuantityToString =
            orderableDoseQuantityTo (Quantity.toValueUnitString -1)


        let orderableDoseQuantityToMd =
            orderableDoseQuantityTo (Quantity.toValueUnitMarkdown -1)


        let inline printItem sn get vuToStr o =
            let on = o.Orderable.Name |> Name.toString

            o.Orderable.Components
            |> Seq.collect (fun c ->
                c.Items
                |> Seq.collect (fun i ->
                    let n = i.Name |> Name.toString
                    if sn |> Seq.exists ((=) n) then
                        i
                        |> get
                        |> vuToStr
                        |> fun s ->
                            if on |> String.startsWith n &&
                               sn |> Seq.length = 1 then seq [ s ]
                            else
                                seq [ $"{s} {n}" ]

                    else Seq.empty
                )
            )
            |> String.concat " + "


        let printOrderTo
            useAdj
            printMd
            sn (ord : Order) =

            let sn = sn |> Array.filter String.notEmpty

            let printFr fr =
                    fr
                    |> (if printMd then (Frequency.toValueUnitMarkdown -1) else (Frequency.toValueUnitString -1))
                    |> String.replace "/" " per "

            let printDq () =
                    let vuToStr =
                        if printMd then Quantity.toValueUnitMarkdown 3
                        else Quantity.toValueUnitString 3
                    ord
                    |> printItem
                           sn
                           (_.Dose.Quantity)
                           vuToStr

            let printDqAdjust isOnce =
                if isOnce then
                    let vuToStr =
                        if printMd then QuantityAdjust.toValueUnitMarkdown 3
                        else QuantityAdjust.toValueUnitString 3
                    ord
                    |> printItem
                           sn
                           (_.Dose.QuantityAdjust)
                           vuToStr
                else
                    let vuToStr =
                        if printMd then QuantityAdjust.toValueUnitMarkdown 3
                        else QuantityAdjust.toValueUnitString 3
                    ord
                    |> printItem
                           sn
                           (_.Dose.QuantityAdjust)
                           vuToStr
                // add dose limits to string
                |> fun s ->
                    if ord.Orderable.Components[0].Dose.Quantity |> Quantity.isSolved then
                        let nv =
                            ord.Orderable.Components[0].Items
                            |> List.map (fun i ->
                                if not useAdj then ""
                                else
                                    i.Dose.QuantityAdjust
                                    |> QuantityAdjust.toOrdVar
                                    |> fun ovar ->
                                        ovar.Constraints
                                        |> OrderVariable.Constraints.toMinMaxString 3
                            )
                            |> List.filter (String.isNullOrWhiteSpace >> not)
                            |> String.concat " + "
                            |> fun s ->
                                if s |> String.isNullOrWhiteSpace then s
                                else
                                    $" ({s})"
                        $"= {s |> String.trim}{nv}"
                    else
                        $"({s |> String.trim})"

            let printDt ord =
                if useAdj then
                    let vuToStr =
                        if printMd then PerTimeAdjust.toValueUnitMarkdown 3
                        else PerTimeAdjust.toValueUnitString 3

                    printItem
                        sn
                        (_.Dose.PerTimeAdjust)
                        vuToStr
                        ord
                else
                    let vuToStr =
                        (if printMd then (PerTime.toValueUnitMarkdown 3) else (PerTime.toValueUnitString 3))

                    printItem
                        sn
                        (_.Dose.PerTime)
                        vuToStr
                        ord
                // add dose limits to string
                |> fun s ->
                    if ord.Orderable.Components[0].Dose.Quantity |> Quantity.isSolved then
                        let nv =
                            ord.Orderable.Components[0].Items
                            |> List.map (fun i ->
                                if not useAdj then ""
                                else
                                    i.Dose.PerTimeAdjust
                                    |> PerTimeAdjust.toOrdVar
                                    |> fun ovar ->
                                        ovar.Constraints
                                        |> OrderVariable.Constraints.toMinMaxString 3
                            )
                            |> List.filter (String.isNullOrWhiteSpace >> not)
                            |> String.concat " + "
                            |> fun s ->
                                if s |> String.isNullOrWhiteSpace then s
                                else
                                    $" ({s})"
                        if s |> String.isNullOrWhiteSpace then s
                        else
                            $"= {s |> String.trim}{nv}"
                    else
                        $"({s |> String.trim})"

            let compQtyToStr =
                if printMd then componentQuantityToMd
                else componentQuantityToString
            let orbDoseQtyToStr =
                if printMd then orderableDoseQuantityToMd
                else orderableDoseQuantityToString
            let doseRateToStr =
                if printMd then Rate.toValueUnitMarkdown -1
                else Rate.toValueUnitString -1
            let orbQtyToStr =
                if printMd then Quantity.toValueUnitMarkdown -1
                else Quantity.toValueUnitString -1

            match ord.Prescription with
            | Once ->
                let dq = printDq ()
                let dqa = printDqAdjust true

                let pres = $"{dq} {dqa}"
                let prep = ord |> compQtyToStr
                let adm = ord |> orbDoseQtyToStr

                pres |> String.replace "()" "",
                prep,
                adm

            | Discontinuous fr ->
                // frequencies
                let fr = fr |> printFr
                let dq = printDq ()
                let dt = printDt ord

                let pres = $"{fr} {dq} {dt}"
                let prep = ord |> compQtyToStr
                let adm = $"{fr} {ord |> orbDoseQtyToStr}"

                pres |> String.replace "()" "",
                prep,
                adm

            | Continuous ->
                // infusion rate
                let rt =
                    ord.Orderable.Dose.Rate
                    |> doseRateToStr

                let oq =
                    ord.Orderable.OrderableQuantity
                    |> orbQtyToStr

                let it =
                    ord
                    |> printItem
                        sn
                        (_.OrderableQuantity)
                        orbQtyToStr

                let dr =
                    let printItem =
                        if useAdj then
                            printItem
                                sn
                                (_.Dose.RateAdjust)
                                (
                                    if printMd then RateAdjust.toValueUnitMarkdown 3
                                    else RateAdjust.toValueUnitString 3
                                )
                        else
                            printItem
                                sn
                                (_.Dose.Rate)
                                (
                                    if printMd then Rate.toValueUnitMarkdown 3
                                    else Rate.toValueUnitString 3
                                )

                    ord
                    |> printItem
                    |> fun s ->
                        if ord.Orderable.Dose.Rate |> Rate.isSolved |> not then s
                        else
                            let nv =
                                ord.Orderable.Components[0].Items
                                |> List.map (fun i ->
                                    if useAdj then
                                        i.Dose.RateAdjust
                                        |> RateAdjust.toOrdVar
                                        |> fun ovar ->
                                            ovar.Constraints
                                            |> OrderVariable.Constraints.toMinMaxString 3
                                    else
                                        i.Dose.Rate
                                        |> Rate.toOrdVar
                                        |> fun ovar ->
                                            ovar.Constraints
                                            |> OrderVariable.Constraints.toMinMaxString 3
                                )
                                |> List.filter (String.isNullOrWhiteSpace >> not)
                                |> String.concat " + "
                            $"{s} ({nv})"

                let pres = $"""{sn |> String.concat " + "} {dr}"""
                let prep = ord |> compQtyToStr
                let adm = $"""{sn |> String.concat " + "} {it} in {oq} stand {rt}"""

                pres, prep, adm

            | Timed (fr, tme) ->
                // frequencies
                let fr = fr |> printFr
                let dq = printDq ()
                let dt = printDt ord

                let tme =
                    let vuToStr =
                        if printMd then Time.toValueUnitMarkdown 2
                        else Time.toValueUnitString 2

                    tme
                    |> vuToStr

                // infusion rate
                let rt =
                    ord.Orderable.Dose.Rate
                    |> doseRateToStr

                let pres = $"{fr} {dq} {dt}"
                let prep = ord |> compQtyToStr
                let adm = $"{fr} {ord |> orbDoseQtyToStr} in {tme} stand {rt}"

                pres |> String.replace "()" "",
                prep,
                adm


            | OnceTimed tme ->
                let dq = printDq ()
                let dqa = printDqAdjust true

                let tme =
                    let vuToStr =
                        if printMd then Time.toValueUnitMarkdown 2
                        else Time.toValueUnitString 2

                    tme
                    |> vuToStr

                // infusion rate
                let rt =
                    ord.Orderable.Dose.Rate
                    |> doseRateToStr

                let pres = $"{dq} {dqa}"
                let prep = ord |> compQtyToStr
                let adm = $"{ord |> orbDoseQtyToStr} in {tme} stand {rt}"

                pres |> String.replace "()" "",
                prep,
                adm


        let printOrderTo2
            useAdj
            printMd
            sns (ord : Order) =

            let findItem sn =
                ord.Orderable.Components
                |> List.collect (_.Items)
                |> List.exists (fun i -> i.Name |> Name.toString |> String.equalsCapInsens sn)

            let pres =
                match ord.Prescription with
                | Once -> ""
                | Discontinuous _ -> ord.Prescription |> Prescription.Print.frequencyToMd
                | _ -> ""

            sns
            |> Array.filter String.notEmpty
            |> Array.filter findItem
            |> function
            | [||] ->
                [|
                    pres
                    ord.Orderable |> Orderable.Print.doseQuantityToMd
                    if useAdj then
                        ord.Orderable
                        |> Orderable.Print.doseQuantityAdjustToMd -1
                |]
                , [||]
                , [||]
            | itms ->
                [|
                |]
                , [||]
                , [||]


        /// <summary>
        /// Print an Order to a string using an array of strings
        /// to pick the Orderable Items to print.
        /// </summary>
        let printOrderToString useAdj =
            printOrderTo
                useAdj
                false


        /// <summary>
        /// Print an Order to a markdown string using an array of strings
        /// to pick the Orderable Items to print.
        /// </summary>
        let printOrderToMd printAdj =
            printOrderTo
                printAdj
                true



    module Dto =

        type Dto (id , n) =
            member val Id = id with get, set
            member val Adjust = OrderVariable.Dto.dto () with get, set
            member val Orderable = Orderable.Dto.dto id n with get, set
            member val Prescription = Prescription.Dto.dto n with get, set
            member val Route = "" with get, set
            member val Duration = OrderVariable.Dto.dto () with get, set
            member val Start = DateTime.now () with get, set
            member val Stop : DateTime option = None with get, set


        let fromDto (dto : Dto) =
            let id = dto.Id |> Id.create
            let adj_qty = dto.Adjust |> Quantity.fromDto
            let ord_tme = dto.Duration |> Time.fromDto
            let orb = dto.Orderable |> Orderable.Dto.fromDto
            let prs = dto.Prescription |> Prescription.Dto.fromDto
            let sts =
                match dto.Stop with
                | Some dt -> (dto.Start, dt) |> StartStop.StartStop
                | None -> dto.Start |> StartStop.Start

            create id adj_qty orb prs dto.Route ord_tme sts


        let toDto (ord : Order) =
            let id = ord.Id |> Id.toString
            let n = ord.Orderable.Name |> Name.toString
            let dto = Dto (id, n)

            dto.Adjust <- ord.Adjust |> Quantity.toDto
            dto.Duration <- ord.Duration |> Time.toDto
            dto.Orderable <- ord.Orderable |> Orderable.Dto.toDto
            dto.Prescription <- ord.Prescription |> Prescription.Dto.toDto
            dto.Route <- ord.Route
            let start, stop =
                match ord.StartStop with
                | StartStop.Start dt -> (dt, None)
                | StartStop.StartStop(start, stop) -> (start, stop |> Some)
            dto.Start <- start
            dto.Stop <- stop

            dto


        /// <summary>
        /// Create a new Order Dto
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        /// <param name="str_prs">A function to create a Prescription with a Name</param>
        let dto id orbN rte cmps str_prs =
            let dto =
                createNew id orbN str_prs rte
                |> toDto

            dto.Orderable.Components <-
                [
                    for cmpN, shape, itms in cmps do
                        let c = Orderable.Component.Dto.dto id orbN cmpN shape
                        c.Items <-
                            itms
                            |> List.map (Orderable.Item.Dto.dto id orbN cmpN)
                        c
                ]

            dto


        let cleanDose (dto : Dto) =
            dto.Duration |> OrderVariable.Dto.clean

            if dto.Prescription.IsDiscontinuous || dto.Prescription.IsTimed then
                dto.Prescription.Frequency |> OrderVariable.Dto.clean
            if dto.Prescription.IsTimed then
                dto.Prescription.Time |> OrderVariable.Dto.clean
            if not dto.Prescription.IsContinuous then
                dto.Orderable.OrderableQuantity |> OrderVariable.Dto.clean

            dto.Orderable.Dose |> Dose.Dto.clean

            dto.Orderable.Components
                |> List.iter (fun c ->
                    c.OrderableQuantity |> OrderVariable.Dto.clean
                    c.OrderableConcentration |> OrderVariable.Dto.clean
                    c.OrderableCount |> OrderVariable.Dto.clean
                    c.Dose |> Dose.Dto.clean
                    c.Items
                    |> List.iter (fun i ->
                        i.OrderableQuantity |> OrderVariable.Dto.clean
                        i.OrderableConcentration |> OrderVariable.Dto.clean
                        i.Dose |> Dose.Dto.clean
                    )
                )


        /// <summary>
        /// Create a new Order Dto with a Continuous Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let continuous id orbN rte cmps  =
            Prescription.continuous Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a Once Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let once id orbN rte cmps =
            Prescription.once Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a OnceTimed Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let onceTimed id orbN rte cmps =
            Prescription.onceTimed Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a Discontinuous Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let discontinuous id orbN rte cmps =
            Prescription.discontinuous Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a Timed Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let timed id orbN rte cmps=
            Prescription.timed Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        let setToOnce (dto : Dto) =
            dto.Prescription <-
                dto.Prescription
                |> Prescription.Dto.setToOnce
            dto


        let setToOnceTimed (dto : Dto) =
            dto.Prescription <-
                dto.Prescription
                |> Prescription.Dto.setToOnceTimed
            dto


        let setToContinuous (dto : Dto) =
            dto.Prescription <-
                dto.Prescription
                |> Prescription.Dto.setToContinuous
            dto


        let setToDiscontinuous (dto : Dto) =
            dto.Prescription <-
                dto.Prescription
                |> Prescription.Dto.setToDiscontinuous
            dto


        let setToTimed (dto : Dto) =
            dto.Prescription <-
                dto.Prescription
                |> Prescription.Dto.setToTimed
            dto

