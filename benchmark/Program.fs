﻿// For more information see https://aka.ms/fsharp-console-apps

open System


module TestSolver =

    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib

    module Api = Api
    module Solver = Solver
    module Name = Variable.Name
    module ValueRange = Variable.ValueRange
    module Minimum = ValueRange.Minimum
    module Maximum = ValueRange.Maximum
    module Increment = ValueRange.Increment
    module ValueSet = ValueRange.ValueSet


    let (|>>) r f =
        match r with
        | Ok x -> x |> f
        | Error _ -> r


    let procss s = printfn $"%s{s} "


    let printEqs = function
        | Ok eqs -> eqs |> Solver.printEqs true procss
        | Error _ -> failwith "errors"


    let printEqsWithUnits = function
        | Ok eqs -> eqs |> Solver.printEqs false procss
        | Error _ -> failwith "errors"


    let setProp n p eqs =
        let n = n |> Name.createExc
        match eqs |> Api.setVariableValues true n p with
        | Some var ->
            eqs
            |> List.map (fun e ->
                e |> Equation.replace var
            )
        | None -> eqs

    let create c u v =
        [|v|]
        |> ValueUnit.create u
        |> c

    let createMinIncl = create (Minimum.create true)
    let createMinExcl = create (Minimum.create false)
    let createMaxIncl = create (Maximum.create true)
    let createMaxExcl = create (Maximum.create false)
    let createIncr = create Increment.create
    let createValSet u v =
        v
        |> Array.ofSeq
        |> ValueUnit.create u
        |> ValueSet.create

    let setIncr u n vals = vals |> createIncr u |> IncrProp |> setProp n
    let setMinIncl u n min = min |> createMinIncl u |> MinProp|> setProp n
    let setMinExcl u n min = min |> createMinExcl u |> MinProp |> setProp n
    let setMaxIncl u n max = max |> createMaxIncl u |> MaxProp |> setProp n
    let setMaxExcl u n max = max |> createMaxExcl u |> MaxProp |> setProp n
    let setValues u n vals = vals |> createValSet u |> ValsProp |> setProp n

    let logger =
        fun (_ : string) ->
            () //File.AppendAllLines("examples.log", [s])
        |> SolverLogging.logger

    let solve n p eqs =
        let n = n |> Name.createExc
        Api.solve true id logger n p eqs

    let solveAll = Api.solveAll false logger

    let solveMinMax = Api.solveAll true logger

    let solveMinIncl u n min = solve n (min |> createMinIncl u |> MinProp)
    let solveMinExcl u n min = solve n (min |> createMinExcl u  |> MinProp)
    let solveMaxIncl u n max = solve n (max |> createMaxIncl u |> MaxProp)
    let solveMaxExcl u n max = solve n (max |> createMaxExcl u |> MaxProp)
    let solveIncr u n incr = solve n (incr |> createIncr u |> IncrProp)
    let solveValues u n vals = solve n (vals |> createValSet u |> ValsProp)

    let init = Api.init
    let nonZeroNegative = Api.nonZeroNegative


    let solveCountMinIncl = solveMinIncl Units.Count.times
    let solveCountMaxExcl = solveMaxExcl Units.Count.times
    let solveCountValues u n vals = solveValues Units.Count.times u n vals


module Utils =

    open MathNet.Numerics

    let inline allPairs min incr max = 
        let x1 = [|min..incr..max|]
        let x2 = [|min..incr..max|]

        x1, x2


    let randomNums seed n max =
        let rnd = Random(seed)

        Array.init n (fun _ -> rnd.Next(1, max))


    let randomBigRationals seed n max =
        let nums = randomNums seed n max
        let denums = randomNums (seed + 1) n max
        Array.zip nums denums
        |> Array.map (fun (n, d) -> 
            let n = BigRational.FromInt(n)
            let d = BigRational.FromInt(d)
            n / d
        )


open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open MathNet.Numerics

open TestSolver
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib


type Benchmarks () =

    let eqs_n n a b c d e f =
        let eqs =
            [
                "a = b + c"
                "d = f * a"
                "d = e * b"
            ] 
            |> List.take n
            |> init 
            |> nonZeroNegative
        
        let set n xsOpt eqs = xsOpt |> Option.map (fun xs -> eqs |> setValues Units.Count.times n xs) |> Option.defaultValue eqs 

        eqs
        |> set "a" a
        |> set "b" b
        |> set "c" c
        |> set "d" d
        |> set "e" e
        |> set "f" f

    let eqs_1 = eqs_n 1 None None None None None None

    let eqs_1_max max =
        let xs = [|1N..1N..max|]
        eqs_n 1 None (Some xs) (Some xs) None None None

    let eqs_3_max max =
        let xs = [|1N..1N..max|]
        eqs_n 3 None (Some xs) (Some xs) None None None

    let eqs_1_max_100 = eqs_1_max 100N

    let eqs_1_max_200 = eqs_1_max 200N

    let eqs_3_max_100 = eqs_3_max 100N

    let eqs_3_max_200 = eqs_3_max 200N

    let allPairsInt_100 = Utils.allPairs 1 1 100
    let allPairsInt_1_000 = Utils.allPairs 1 1 200

    let allPairs_100 = Utils.allPairs 1N 1N 100N
    let allPairs_200 = Utils.allPairs 1N 1N 200N

    let getTwoRandomLists n max =
        let xs1 = Utils.randomBigRationals 1 n max
        let xs2 = Utils.randomBigRationals 2 n max
        xs1, xs2

    let eqs_1_Rand n =
        let xs1, xs2 = getTwoRandomLists n 1_000
        eqs_n 1 None (Some xs1) (Some xs2) None None None

    let eqs_3_Rand n =
        let xs1, xs2 = getTwoRandomLists n 1_000
        eqs_n 3 None (Some xs1) (Some xs2) None None None


    let rand_100_a, rand_100_b = getTwoRandomLists 100 1_000

    let rand_200_a, rand_200_b = getTwoRandomLists 200 1_000

    let eqs_1_rand_10 = eqs_1_Rand 10

    let eqs_1_rand_20 = eqs_1_Rand 20

    let eqs_3_rand_10 = eqs_3_Rand 10

    let eqs_3_rand_20 = eqs_3_Rand 20


    member this.Print () =
        eqs_1_rand_10 
        |> List.map (Equation.toString true)
        |> String.concat "\n"
        |> printfn "%s"



    [<Benchmark>]
    member this.AllPairesInt_100 () =
        let x1, x2 = allPairsInt_100

        Array.allPairs x1 x2
        |> Array.map (fun (x1, x2) -> x1 + x2)
        |> Array.distinct


    [<Benchmark>]
    member this.AllPairsInt_200 () =
        let x1, x2 = allPairsInt_1_000

        Array.allPairs x1 x2
        |> Array.map (fun (x1, x2) -> x1 + x2)
        |> Array.distinct


    [<Benchmark>]
    member this.AllPairsBR_100 () =
        let x1, x2 = allPairs_100

        Array.allPairs x1 x2
        |> Array.map (fun (x1, x2) -> x1 + x2)
        |> Array.distinct


    [<Benchmark>]
    member this.AllPairsBR_200 () =
        let x1, x2 = allPairs_200

        Array.allPairs x1 x2
        |> Array.map (fun (x1, x2) -> x1 + x2)
        |> Array.distinct


    [<Benchmark>]
    member this.AllPairsBR_Rand_100 () =
        let x1, x2 = rand_100_a, rand_100_b

        Array.allPairs x1 x2
        |> Array.map (fun (x1, x2) -> x1 + x2)
        |> Array.distinct


    [<Benchmark>]
    member this.AllPairsBR_Rand_200 () =
        let x1, x2 = rand_200_a, rand_200_b

        Array.allPairs x1 x2
        |> Array.map (fun (x1, x2) -> x1 + x2)
        |> Array.distinct


    [<Benchmark>]
    member this.SolveCountMinIncl () =
        solveCountMinIncl "a" 10N eqs_1

    [<Benchmark>]
    member this.Solve_1_Eqs_100 () =
        solveAll eqs_1_max_100


    [<Benchmark>]
    member this.Solve_1_Eqs_200 () =
        solveAll eqs_1_max_200


    [<Benchmark>]
    member this.Solve_3_Eqs_100 () =
        solveAll eqs_3_max_100


    [<Benchmark>]
    member this.Solve_3_Eqs_200 () =
        solveAll eqs_3_max_200


    [<Benchmark>]
    member this.Solve_1_Eqs_Rand_10 () =
        solveAll eqs_1_rand_10


    [<Benchmark>]
    member this.Solve_1_Eqs_Rand_20 () =
        solveAll eqs_1_rand_20


    [<Benchmark>]
    member this.Solve_3_Eqs_Rand_10 () =
        solveAll eqs_3_rand_10


    [<Benchmark>]
    member this.Solve_3_Eqs_Rand_20 () =
        solveAll eqs_3_rand_20



// For more information see https://aka.ms/fsharp-console-apps
[<EntryPoint>]
let main (args: string[]) =

    Benchmarks().Print()
    
    printfn "Starting to run benchmarks"
    let _ = BenchmarkRunner.Run<Benchmarks>()
    printfn "Finished running benchmarks"

    0

