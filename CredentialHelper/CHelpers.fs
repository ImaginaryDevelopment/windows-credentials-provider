[<System.Runtime.CompilerServices.Extension>]
module CredentialHelper.CHelpers
open System.Runtime.CompilerServices

open BReusable

[<Extension>]
let toList (this: 't seq) =
    List.ofSeq this

[<Extension>]
let toFSharpFunc(this: System.Func<bool>): FSharpFunc<unit,bool> =
    fun () -> this.Invoke()

[<Extension>]
let tryGetError(this: Result<_,'t>): 't option =
    match this with
    | Error e -> Some e
    | Ok _ -> None

[<Extension>]
let tryGetValue(this:Result<'t,_>): 't option =
    match this with
    | Ok v -> Some v
    | _ -> None

[<Extension>]
let mapOk(this:Result<'t,_>, f: System.Func<'t,_>) : Result<_,_> =
    this
    |> Result.map f.Invoke

let createLatchedFunction (action: System.Action) =
    BReusable.createLatchedFunction action.Invoke

let createLatchedFunctionA action : System.Action =
    let f = createLatchedFunction action
    new System.Action(fun () -> f())

[<Extension>]
let before(this: string, delimiter:string): string =
    match this with
    | Before delimiter value -> value
    | _ -> invalidArg "this" "Delimiter not found"

[<Extension>]
let after(this: string, delimiter:string) : string = 
    match this with
    | After delimiter value -> value
    | _ -> invalidArg "this" "Delimiter not found"
