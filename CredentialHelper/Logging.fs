module CredentialHelper.Logging

open Reusable

open System.IO

let tryFormatEx (ex:exn) =
    try
        ex.GetType().Name + ":" + ex.Message
    with _ -> ex.Message

open System.Diagnostics
open System.Collections.Generic

type private Stub = class end 

let inline private mapEventLogType elt =
    match elt with
    | EventLogType.Information -> EventLogEntryType.Information
    | EventLogType.Error -> EventLogEntryType.Error
    | EventLogType.FailureAudit -> EventLogEntryType.FailureAudit
    | EventLogType.SuccessAudit -> EventLogEntryType.SuccessAudit
    | EventLogType.Warning -> EventLogEntryType.Warning

let tryLogFile fn (text:string,elt) =
    if System.String.IsNullOrWhiteSpace fn then
        Error "Bad logging filename"
    else
        if fn |> Path.GetDirectoryName |> Directory.Exists |> not then
            Error "Log directory does not exist"
        else
            try
                let elet = mapEventLogType elt
                File.AppendAllText(fn, $"{elet}:{text}")
                Ok ()
            with ex ->
                try
                    eprintfn "Error type: %s" (ex.GetType().Name)
                with _ -> ()
                Error ex.Message


// https://stackoverflow.com/questions/25725151/write-to-windows-application-event-log-without-event-source-registration

let mutable registrationAttempted = false
let mutable successNoRegAttempted = false


let tryLogEvent appName eventLogNameOpt (text,elt) =
    // which type of EventLog, not the application name
    let elName = eventLogNameOpt |> Option.defaultValue "Application"

    let writeEntry text =

        use el = new EventLog(elName)
        el.Source <- appName
        let elet = mapEventLogType elt
        el.WriteEntry(text, elet)
    let recordSuccess () =
        if not registrationAttempted && not successNoRegAttempted then
            successNoRegAttempted <- true
            writeEntry "Success without registration"

    let registration () =
        if not registrationAttempted then
            registrationAttempted <- true
            let mutable registrationSucceeded = false
            try
            if EventLog.SourceExists appName |> not then
                EventLog.CreateEventSource(appName,elName)
                registrationSucceeded <- true
            with
                | :? System.Security.SecurityException ->
                    ()
            if registrationSucceeded then
                writeEntry "Registration Success"
        writeEntry text

    (None, [
        (fun () -> writeEntry text) >> recordSuccess
        registration
    ])
    ||> List.fold(fun state f ->
        match state with
        | Some (Error _)
        | None ->
            try
                f() |> Ok |> Some
            with ex -> tryFormatEx ex |> Error |> Some
        | x -> x
    )
    |> Option.defaultValue (Error "Unexpectedly empty")

type LogListAttemptType =
    | TryAll
    | StopOnSuccess

let tryLoggings llat (logFuncs:(string * System.Func<_,_,Result<unit,string>>) seq) (text:string,elt: EventLogType) =
    (List.empty,logFuncs)
    ||> Seq.fold(fun results (title,logFunc) ->
        let attemptLog() =
            try
                logFunc.Invoke(text,elt)
            with ex -> tryFormatEx ex |> Error
        match llat, results |> List.tryFind(snd >> function | Ok _ -> true | _ -> false) with
        | TryAll, _ -> (title, attemptLog ()) :: results
        | StopOnSuccess, None -> (title,attemptLog()) :: results
        | StopOnSuccess, _ -> results
    )
    |> Map.ofList

type LoggerDelegate = System.Func<string,EventLogType,Result<unit,string>>
type FullLoggingArgs = {
    AppName: string
    AttemptType: LogListAttemptType
    FileNames: string list
    //PriorityLoggers: (string * LoggerDelegate) IReadOnlyList
    //FallbackLoggers: (string * LoggerDelegate) IReadOnlyList
}

let private tryLoggingsWithFallback' fla (text,elt) =
    let logFuncs = [
        //yield! fla.PriorityLoggers
        yield! fla.FileNames |> List.map(fun fn ->
            let fPath = System.IO.Path.GetFullPath fn
            let f: LoggerDelegate = System.Func<_,_,_>(fun text elt -> tryLogFile fPath (text,elt))
            fPath,  f
            )
        let fEvent: LoggerDelegate = System.Func<_,_,_>(fun text elt -> tryLogEvent fla.AppName None (text,elt))
        "EventLogger", fEvent
        //yield! fla.FallbackLoggers
    ]

    tryLoggings fla.AttemptType logFuncs (text,elt)

let tryf f args =
    try
        f args |> Some
    with _ -> None

let mutable startupLogged = false

let chAsmOpt = lazy(
    let t = typeof<Stub>
    ()
    |> tryf (fun () ->
        t.Assembly
    )
)
let tryGetAsm f : Lazy<System.Reflection.Assembly option>=
    lazy(
        tryf f ()
    )

let asms =
    lazy (
        [
            "Entry",tryGetAsm System.Reflection.Assembly.GetEntryAssembly
            "ChAsm",chAsmOpt
            //"Exe", tryGetAsm System.Reflection.Assembly.GetCallingAssembly
        ]
        |> List.choose (Tuple2.mapSnd _.Value >> Option.ofSnd)
    )

//let prefixes =
//    [
//        Codebase, fun (asm:System.Reflection.Assembly) -> asm.CodeBase
//        Location, fun (asm:System.Reflection.Assembly) -> asm.Location
//    ]
//    |> List.map(fun (n,f) -> n, tryf f)
//    |> Map.ofList

let tryGetFileInfo (location:string) =
    // codebase may produce this: file:///C:/Users/User/AppData/Local/Temp/LINQPad7/_dwololqc/query_kgvigc.dll
    Reflection.fixLocationInfo location
    |> fun v ->
        try
            System.IO.Path.GetFullPath v
            |> Some
        with ex ->
            eprintfn "Could not get value from location: '%s' - '%s'" ex.Message v
            None
    |> Option.filter File.Exists
    |> Option.bind( tryf (fun location -> System.IO.FileInfo location) )

let logStartup fla =
    if not startupLogged then
        startupLogged <- true
        let log (msg,elt) = tryLoggingsWithFallback' fla (msg,elt) |> ignore<Map<_,_>>

        log ("CD:" + System.Environment.CurrentDirectory, EventLogType.Information)

        let tryLogStartupInfo title asm =
            asm
            |> Reflection.tryGetLocation
            |> Option.iter(fun li ->
                let fi =
                    tryGetFileInfo li.Path
                    |> Option.map(fun fi ->
                        [
                            "LastWrite:"+ fi.LastWriteTime.ToString("o")
                            "Created:" + fi.CreationTime.ToString("o")
                            "SHA:ec821ab"
                        ]
                    )
                    |> Option.defaultValue List.empty
                let msg =
                    [
                        $"{title}|{li.LocationType}|{li.Path}"
                        yield! fi
                    ]
                    |> String.concat System.Environment.NewLine
                log (msg,EventLogType.SuccessAudit)
            )

        // log assemblies
        match asms.Value with
        | [] -> log ("No Asms found", EventLogType.Error)
        | asms ->
            (List.empty,asms)
            ||> List.fold(fun asmNames (title,asm) ->
                if asmNames |> List.contains asm.FullName then
                    asmNames
                else
                    tryLogStartupInfo title asm
                    asm.FullName :: asmNames
            )
            |> ignore

let tryLoggingsWithFallback fla (text,elt) =
    logStartup fla
    tryLoggingsWithFallback' fla (text,elt)
