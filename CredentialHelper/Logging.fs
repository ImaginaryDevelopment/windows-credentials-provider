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

[<RequireQualifiedAccess>]
type EventLogType =
    | Error // 1
    | Warning // 2
    | Information // 4
    | SuccessAudit // 8
    | FailureAudit // 16

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

let asmOpt = lazy(
    let t = typeof<Stub>
    ()
    |> tryf (fun () ->
        t.GetType().Assembly)
)

let prefixes =
    [
        "c", fun (asm:System.Reflection.Assembly) -> asm.CodeBase
        "l", fun (asm:System.Reflection.Assembly) -> asm.Location
    ]
    |> List.map(fun (n,f) -> n + "|", tryf f)
    |> Map.ofList

let fixLocationInfo location =
    match location with
    | WhiteSpace _ | NonValueString -> None
    | After "l|" v -> Some v
    | After "c|" v -> Some v
    | ValueString _ -> Some location
    |> Option.map(function
        | After "file:///" v ->
            // fix, assuming windows
            if System.IO.Path.DirectorySeparatorChar = '\\' && v.Contains "/" then
                v |> replace "/" "\\"
            else v
        | v -> v
    )

let tryGetLocation (asm:System.Reflection.Assembly) =
    (None, prefixes)
    ||> Map.fold(fun state prefix attemptF ->
        match state with
        | Some l -> Some l
        | None ->
            attemptF asm |> Option.bind fixLocationInfo |> Option.map (fun l -> prefix + "|" + l)
    )
        //tryf (fun () ->
        //    "c|" + asm.CodeBase)
        //|> Option.orElseWith(fun () ->
        //    tryf (fun () -> "l|" + asm.Location)
        //)

let tryGetFileInfo (location:string) =
    // codebase may produce this: file:///C:/Users/User/AppData/Local/Temp/LINQPad7/_dwololqc/query_kgvigc.dll
    fixLocationInfo location
    |> Option.map System.IO.Path.GetFullPath
    |> Option.filter File.Exists
    |> Option.bind( tryf (fun location -> System.IO.FileInfo location) )

let logStartup fla =
    if not startupLogged then
        startupLogged <- true
        let log (msg,elt) = tryLoggingsWithFallback' fla (msg,elt) |> ignore<Map<_,_>>

        log ("CD:" + System.Environment.CurrentDirectory, EventLogType.Information)
        asmOpt.Value
        |> Option.iter(fun asm ->
            asm
            |> tryGetLocation
            |> Option.iter(fun l ->
                log (l,EventLogType.SuccessAudit)
                tryGetFileInfo l
                |> Option.iter(fun fi ->
                    [
                        "LastWrite:"+ fi.LastWriteTime.ToString("o")
                        "Created:" + fi.CreationTime.ToString("o")
                    ]
                    |> List.iter(fun msg ->
                        log ( msg, EventLogType.SuccessAudit)
                    )
                )
            )
        )

let tryLoggingsWithFallback fla (text,elt) =
    logStartup fla
    tryLoggingsWithFallback' fla (text,elt)
