module CredentialHelper.CommandParser

open Reusable

type CommandType =
    | ComInvoke
    | ShowUI
    | ApiCall
    | AttemptLogin

let (|HasArg|_|) (arg:string) =
    function
    | v :: _
    | _ :: v :: [] when v = arg  -> Some ()
    | _ -> None

let getCommandType (args: string[]) =
    let args = args |> Option.ofObj |> Option.defaultValue Array.empty |> List.ofArray 
    printfn "Args: %A" args
    match args with
    | HasArg "-com" ->
        ComInvoke
    | HasArg "-api" ->
        ApiCall
    | HasArg "-ui" -> ShowUI
    | _ -> AttemptLogin

