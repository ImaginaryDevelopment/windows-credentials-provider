module CredentialHelper.CommandParser

open BReusable

type CommandType =
    | ComInvoke
    | ShowUI
    | ApiCall
    | AttemptLogin
    | OutputDiagnostics
    | ShowArgs

let (|HasArg|_|) (arg:string) =
    function
    | v :: _
    | _ :: v :: [] when v = arg  -> Some ()
    | _ -> None

let commands = [
    "-com", ComInvoke
    "-ui", ShowUI
    "-api", ApiCall
    "-login", AttemptLogin
    "-diag", OutputDiagnostics
    "-help", ShowArgs
]

let showHelp () =
    printfn "Commands available:"
    commands
    |> Seq.iter(fun (arg,v) ->
        printfn $"\t{arg}:{v}"
    )

// this doesn't automatically stay in sync with commands above
let getCommandType (args: string[]) =
    let args = args |> Option.ofObj |> Option.defaultValue Array.empty |> List.ofArray 
    printfn "Args: %A" args
    match args with
    | HasArg "-com" -> ComInvoke
    | HasArg "-api" -> ApiCall
    | HasArg "-ui" -> ShowUI
    | HasArg "-diag" -> OutputDiagnostics
    | HasArg "-help" -> ShowArgs
    | _ -> AttemptLogin

