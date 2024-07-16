[<System.Runtime.CompilerServices.Extension>]
module CredentialHelper.CHelpers
open System.Runtime.CompilerServices

[<Extension>]
let toList (this: 't seq) =
    List.ofSeq this

[<Extension>]
let toFSharpFunc(this: System.Func<bool>): FSharpFunc<unit,bool> =
    fun () -> this.Invoke()

let createLatchedFunction (action: System.Action) =
    Reusable.createLatchedFunction action.Invoke

let createLatchedFunctionA action : System.Action =
    let f = createLatchedFunction action
    new System.Action(fun () -> f())
