module CredentialHelper.CommandParser

open Reusable

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

let outputDiagnostics (dllComGuid: System.Guid) =
    let indentValue = "\t"
    let guidStr = dllComGuid |> string |> System.String.toUpper |> sprintf "{%s}"
    let getNext indent = $"%s{indent}{indentValue}"
    let rec walkTree (root:Microsoft.Win32.RegistryKey) indent limit path =
        use sk = root.OpenSubKey(path,false)
        if not <| isNull sk then
            sk.GetValueNames()
            |> Seq.iter(fun vn ->
                let v =
                    try
                        sk.GetValue vn
                    with _ -> "<null>"
                printfn "%s%s - %A" indent vn v
            )
            sk.GetSubKeyNames()
            |> Seq.iter(fun skn ->
                printfn "%s%s" indent skn
                if limit > 1 then
                    walkTree root (getNext indent) (limit - 1) skn
            )
        else printfn "%s%s-%s is null" indent root.Name path
    let expected = "{298D9F84-9BC5-435C-9FC2-EB3746625954}"
    if guidStr <> expected then
        eprintfn "Guid issue %s <> %s" guidStr expected

    let printEmptyBanner() = printfn "------"

    printEmptyBanner()
    [
        Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\" + guidStr
        Microsoft.Win32.Registry.ClassesRoot, @"CLSID\" + guidStr
        Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device"
    ]
    |> List.iter(fun (r,p) ->
        try
            printfn "%s-%s" r.Name p
            walkTree r indentValue 2 p
        with ex ->
            let t = tryGetTypeName ex
            eprintfn "Failed to walk %s - %s: %s(%s)" r.Name p t ex.Message
        printEmptyBanner()
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

