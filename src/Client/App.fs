module App

open Elmish
open Elmish.React

//-:cnd:noEmit
#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

//+:cnd:noEmit
Program.mkProgram Pages.Main.init Pages.Main.update Pages.Main.render
//-:cnd:noEmit
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactSynchronous "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
//+:cnd:noEmit
|> Program.run
