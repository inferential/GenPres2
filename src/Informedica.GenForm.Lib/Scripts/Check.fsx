

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


Environment.SetEnvironmentVariable("GENPRES_PROD", "1")


let checked =
    DoseRule.get ()
    |> Array.filter (fun dr -> true
    //    dr.Generic = "abatacept" &&
    //    dr.Shape = "" &&
    //    dr.Route = "iv"
    )
    |> Check.checkAll Patient.patient


checked
|> Array.iter (printfn "%s")

