

#load "load.fsx"

open System

let dataUrlId = "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", dataUrlId)



#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../VenousAccess.fs"
#load "../Mapping.fs"
#load "../Patient.fs"
#load "../LimitTarget.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../SolutionRule.fs"

#time


open Informedica.GenForm.Lib


open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Utils



open Informedica.GenUnits.Lib

let filter =
    { Filter.filter with
        Patient =
            { Patient.patient with
                VenousAccess = []
                Department = Some "ICK"
                Age =
                    Units.Time.day
                    |> ValueUnit.singleWithValue 2N
                    |> Some
                Weight =
                  Units.Weight.kiloGram
                  |> ValueUnit.singleWithValue (3N)
                  |> Some
            }
    }


let dr =
    DoseRule.get ()
    |> Array.take 1
    |> DoseRule.filter filter
    |> Array.item 0


dr
|> fun dr ->
    SolutionRule.get ()
    |> SolutionRule.filter
        { filter with
            Generic = dr.Generic |> Some
            //Shape = dr.Shape |> Some
            //Route = dr.Route |> Some
            DoseType = dr.DoseType |> DoseType.toString |> Some
        }


SolutionRule.get ()
|> Array.filter(fun sr -> sr.Generic = "alprostadil")
