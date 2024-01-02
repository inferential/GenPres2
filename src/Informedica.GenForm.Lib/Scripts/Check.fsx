

#load "load.fsx"
#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../VenousAccess.fs"
#load "../Patient.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../Check.fs"
#load "../SolutionRule.fs"
#load "../PrescriptionRule.fs"


open System
open Informedica.GenForm.Lib


module GStand = Informedica.ZForm.Lib.GStand
module RuleFinder = Informedica.ZIndex.Lib.RuleFinder


Environment.SetEnvironmentVariable("GENPRES_PROD", "1")


//|> Array.length

(*
|> Array.filter (fun dr ->
    dr.Route = "iv" &&
    dr.Generic = "amoxicilline"
)
*)


let didNotPass =
    DoseRule.get ()
    |> Array.filter (fun dr ->
        true //dr.Generic = "ondansetron"
    )
    |> Check.checkAll


didNotPass |> Array.length
|> Array.iter (printfn "%s")

