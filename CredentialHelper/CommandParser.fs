module CredentialHelper.CommandParser

open BReusable

type CommandType =
    | ComInvoke
    | ShowUI
    | ApiCall of qrCodeValue: string option
    | AttemptLogin
    | OutputDiagnostics
    | ShowArgs
    with
        static member TryGetApiQrCode(ct) =
            match ct with
            | ApiCall (Some (ValueString iOpt)) -> Some iOpt
            | _ -> None

let (|HasArg|_|) (arg:string) args =
    args |> List.tryFind ((=) arg) |> Option.map ignore

// assumes the arg pair beging at index 0 or 1
let (|HasArgPair|_|) (arg:string) =
    function
    | arg' :: arg2' :: _
    | _ :: arg' :: arg2' :: [] when arg' = arg -> Some arg2'
    | _ -> None

let commands = [
    "-com", ComInvoke
    "-ui", ShowUI
    "-api", ApiCall None
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

    // this might catch other args
    | HasArgPair "-api" (ValueString portOpt) when not <| portOpt.StartsWith "-" -> ApiCall (Some portOpt)

    | HasArg "-api" -> ApiCall None
    | HasArg "-ui" -> ShowUI
    | HasArg "-diag" -> OutputDiagnostics
    | HasArg "-help" -> ShowArgs
    | _ -> AttemptLogin

