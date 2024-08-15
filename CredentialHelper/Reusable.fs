module Reusable

open System

type OnAction = delegate of unit -> unit
type OnAction<'t> = delegate of 't -> unit

type Property<'t> = {
    Getter: unit -> 't
    Setter: 't -> unit
}

let failNullOrEmpty paramName x = if String.IsNullOrEmpty x then raise <| ArgumentOutOfRangeException paramName

let inline tryGetTypeName(value:obj) =
    match value with
    | null -> "<null>"
    | _ ->
        try
            value.GetType().Name
        with ex ->
            eprintfn "Failed to read type name"
            "<typeUnk>"

let tee f x =
    f x
    x

// prevent re-entrancy
let createLatchedFunction f =
    let mutable latch = false
    fun () ->
        if not latch then
            latch <- true
            f()
            latch <- false

module Option =
    let ofValueString =
        function
        | null -> None
        | x when String.IsNullOrWhiteSpace x -> None
        | x -> Some x
    let inline ofZeroOrPositive value =
        if value >= LanguagePrimitives.GenericZero then Some value else None

    let inline ofTrue value =
        match value with
        | true -> Some ()
        | false -> None
    let inline ofFalse value =
        match value with
        | true -> None
        | false -> Some ()

    let inline teeNone f =
        function
        | None -> f(); None
        | Some x -> Some x

    let inline ofSnd (x,y) =
        match y with
        | None -> None
        | Some y -> Some(x,y)
    //let inline ofSndMap f (x,y) =
    //    match f y with
    //    |

    let ofTryF f args =
        try
            f args |> Some
        with _ -> None


let (|ValueString|WhiteSpace|NullString|EmptyString|) =
    function
    | null -> NullString
    | x when not <| System.String.IsNullOrWhiteSpace x -> ValueString x
    | x when System.String.IsNullOrEmpty x -> EmptyString
    | x -> WhiteSpace x

type String with
    static member inline makeIndexFunction f delimiter text =
        failNullOrEmpty (nameof delimiter) delimiter
        Option.ofValueString text
        |> Option.bind(f delimiter >> Option.ofZeroOrPositive)

    static member inline indexOf delimiter text =
        String.makeIndexFunction (fun delimiter value -> value.IndexOf delimiter) delimiter text

    static member inline indexOfI delimiter text =
        String.makeIndexFunction (fun delimiter value -> value.IndexOf(delimiter, StringComparison.InvariantCultureIgnoreCase)) delimiter text

    static member inline trim text =
        Option.ofValueString text
        |> Option.map (fun text -> text.Trim())
        |> Option.defaultValue text

    static member inline trimc (char:Char) text =
        Option.ofValueString text
        |> Option.map(fun text -> text.Trim char)
        |> Option.defaultValue text

    static member endsWith delimiter text =
        failNullOrEmpty (nameof delimiter) delimiter
        Option.ofValueString text
        |> Option.map(fun text -> text.EndsWith delimiter)
        |> Option.defaultValue false

    static member contains delimiter text =
        failNullOrEmpty (nameof delimiter) delimiter
        if String.IsNullOrEmpty text then
            false
        else text.Contains delimiter

    static member containsI delimiter text =
        String.indexOfI delimiter text
        |> Option.isSome

    static member tryAfter delimiter text =
        String.indexOf delimiter text
        |> Option.map(fun i ->
            text[i + delimiter.Length ..]
        )

    static member inline substring startIndex (nonNullValue: string) = nonNullValue.Substring(startIndex)
    static member inline substring2 startIndex length (nonNullValue:string) = nonNullValue.Substring(startIndex, length)

    static member tryBefore delimiter text =
        String.indexOf delimiter text
        |> Option.map(fun i -> text |> String.substring2 0 i)

    static member replace delimiter replacement text =
        failNullOrEmpty (nameof delimiter) delimiter
        match text with
        | ValueString _ -> text.Replace(delimiter, replacement)
        | _ -> text

    static member inline afterOrSelf delimiter text =
        String.tryAfter delimiter text |> Option.defaultValue text
    static member inline beforeOrSelf delimiter text =
        String.tryBefore delimiter text |> Option.defaultValue text

    static member makeEndWith delimiter text =
        if String.endsWith delimiter text then text
        else text + delimiter

    static member toLower text =
        match text with
        | ValueString text -> text.ToLower()
        | _ -> text
    static member toUpper text =
        match text with
        | ValueString text -> text.ToUpper()
        | _ -> text

let (|After|_|) delimiter text =
    text |> String.tryAfter delimiter

let (|Before|_|) delimiter text =
    text |> String.tryBefore delimiter

// String Contains Case Sensitive
let (|ContainsS|_|) delimiter =
    String.contains delimiter
    >> Option.ofTrue

// String Contains Case Insensitive
let (|ContainsI|_|) delimiter =
    String.containsI delimiter
    >> Option.ofTrue

let inline uncurry f (x,y) = f x y

let inline defer f x =
    fun () -> f x

module Tuple2 =
    let mapSnd f (x,y) =
        x, f y
    let withSnd y x = x,y

let inline fromParser f x =
    match f x with
    | true, v -> Some v
    | _, _ -> None

let inline tryParseInt (x:string) : int option = x |> fromParser System.Int32.TryParse
let inline tryParseGuid (x:string) : Guid option = x |> fromParser System.Guid.TryParse


let inline createDisposable (onDispose: unit -> unit) =
    {new System.IDisposable with member x.Dispose() = onDispose()}

let createObserver (onNext,onCompleted, onError): IObserver<'t> =
    {
        new System.IObserver<'t> with
            member _.OnNext(value) =
                match onNext with
                | None -> ()
                | Some f -> f value
            member _.OnCompleted() =
                match onCompleted with
                | None -> ()
                | Some f -> f ()
            member _.OnError ex =
                match onError with
                | None -> ()
                | Some f -> f ex
    }

let toDisposable (x: #System.IDisposable) =
    x :> System.IDisposable

let dispose (x:System.IDisposable) =
    x.Dispose()

let tryDispose title x =
    if Object.ReferenceEquals(x,null) then
        eprintfn "Failed to dispose(null): '%s'" title
    else
        try
            dispose x
        with _ ->
            eprintfn "Failed to dispose: '%s'" title

type DisposalTracker<'t when 't :> IDisposable>(msg, value, throwOnDisposedAccess) =
    let gId = Guid.NewGuid()
    let mutable isDisposed = false
    let mutable disposedTitle = None
    let hasValue = not <| Object.ReferenceEquals(null,value)
    let l = if hasValue then new obj() else null
    let getText() = $"{msg}(%A{gId})"

    member _.GetText() = getText()
    member _.IsDisposed = isDisposed
    member _.Value:'t =
        if isDisposed then
            let text = 
                match disposedTitle with
                | None -> $"Disposed access to {getText()}"
                | Some disposedTitle ->
                    $"Disposed access to {getText()} after '%s{disposedTitle}'" 
            if throwOnDisposedAccess then
                invalidOp text
            else
                eprintfn "%s" text
        value

    member _.TryGet( ?title :string) =
        let title = title |> Option.bind Option.ofValueString |> Option.map (sprintf "- %s") |> Option.defaultValue ""
        if isDisposed then
            eprintfn $"Attempt to read from disposed({getText()}){title}"
            None
        elif not hasValue then
            eprintfn $"Attempt to read from null({getText()}){title}"
            None
        else Some value

    member _.Dispose(title: string) =
        if not hasValue then
            eprintfn "Disposal(%s) called on null value: %s" title <| getText() 
        else
            lock l (fun () ->
                if not isDisposed then
                    //printfn "Disposing(%s) %s: " title <| getText()
                    isDisposed <- true
                    disposedTitle <- Some title
                    try
                        value.Dispose()
                    with ex ->
                        eprintfn "Disposal(%s) failed %s: %s'%s'" title (getText()) (tryGetTypeName ex) ex.Message
                else
                    eprintfn "Disposal(%s) called on disposed value: %s" title <| getText()
            )

    member x.Dispose() = x.Dispose("?")


    interface IDisposable with
        member x.Dispose() = x.Dispose()

module DisposalTracker =
    let inline tryGet (dt:DisposalTracker<_>) = dt.TryGet()

    //    if dt.IsDisposed then None
    //    else dt |> Option.map toDisposable
    // assume we're option wrapped
    let tryGetNamedDisposable title (dt:DisposalTracker<_> option) =
        dt |> Option.bind (fun dt ->
            if dt.IsDisposed then 
                createDisposable(fun () -> dt.Dispose title) |> Some
            else None
        )

type IObservableStore<'t> =
    inherit IObservable<'t>
    abstract member Value: 't

// https://github.com/davedawkins/Sutil/blob/main/src/Sutil/ObservableStore.fs
type ObservableStore<'t>(value: 't) =
    let mutable value = value
    let mutable uid = 0
    let subscribers = System.Collections.Generic.Dictionary<_, IObserver<'t>>()

    // should we check equality so we don't update for being sent the same value?
    member _.Value
        with get() = value
        and set v =
            value <- v
            if subscribers.Count > 0 then
                subscribers.Values
                |> Seq.iter(fun s -> s.OnNext value)

    member _.Subscribe observer : IDisposable =
        let id = uid
        uid <- uid + 1
        subscribers.Add(id, observer)

        createDisposable (fun () ->
            subscribers.Remove id |> ignore<bool>
        )

    interface IObservableStore<'t> with
        member x.Value = x.Value
        member x.Subscribe observer = x.Subscribe observer

module Controls =

    let inline ensureInvoke (f: OnAction) x =
        if (^t: (member InvokeRequired: bool)(x)) then
            (^t: (member Invoke: OnAction -> obj)(x,f))
        else f.Invoke()

    let inline setInvokedIfNot control oldValue f x =
        if oldValue <> x then
            control |> ensureInvoke f |> ignore<obj>


    let inline setTextIfNot control text =
        let oldValue = (^t: (member Text: string)(control))
        let inline f () = (^t: (member set_Text: string -> unit)(control,text))
        setInvokedIfNot control oldValue f text

    let inline setEnabledIfNot control enabled =
        let oldValue = (^t: (member Enabled: bool)(control))
        let inline f () = (^t: (member set_Enabled: bool -> unit)(control,enabled))
        setInvokedIfNot control oldValue f enabled

module Map =
    let addListItem k v m =
        match m |> Map.tryFind k with
        | None -> m |> Map.add k [v]
        | Some items -> m |> Map.add k (v::items)

    let mapListLength m =
        m
        |> Map.map(fun _ -> List.length)

module Async =
    open System.Runtime.CompilerServices
    open System.Threading
    open System.Threading.Tasks

    let inline map f x =
        async {
            let! value = x
            return f value
        }

    // https://stackoverflow.com/questions/28350329/how-to-await-taskawaiter-or-configuredtaskawaitable-in-f
    let fromTaskAwaiter (awaiter: TaskAwaiter<'a>) =
        async {
            use handle = new SemaphoreSlim(0)
            awaiter.OnCompleted(fun () -> ignore (handle.Release()))
            let! _ = handle.AvailableWaitHandle |> Async.AwaitWaitHandle
            return awaiter.GetResult()
        }
    let inline catchB t =
        t
        |> Async.Catch
        |> map (function | Choice1Of2 v -> Ok v | Choice2Of2 e -> Error e)

    let inline catchBind (t:Async<Result<'t,exn>>) =
        t
        |> catchB
        |> map(
            function
            | Error e -> Error e
            | Ok (Error e) -> Error e
            | Ok (Ok v) -> Ok v
        )
type Reporter =
    abstract member LogError: string*exn -> unit
    abstract member LogError: string -> unit
    abstract member Log: string -> unit
    abstract member Log<'t>: string * 't -> unit

type IDeserializer =
    abstract member Deserialize<'t>: string -> Result<'t,string*exn>

type IDeserializerR =
    abstract member Deserialize<'t>: string -> 't option


let tryInvokes functions =
    functions
    |> List.choose(fun f ->
        try
            f() |> Some
        with _ -> None
    )
    |> List.tryHead

type ReflectionInfo = {
    AssemblyName: string
    Location: string
    Version: string
}

let tryMungeCallerFilePath (cfp: string) =
    match cfp with
    | ValueString cfp ->
        try
            System.IO.Path.GetFileName cfp
        with _ ->
            cfp
    | _ -> cfp

type AsmFileLocationType =
    | CodeBase
    | CodeBaseUri
    | Location

type LocationDescription = {
    LocationType: AsmFileLocationType
    Path:string
}

module Reflection =

    let fixLocationInfo location =
        location
        |> Option.ofValueString
        |> Option.map(function
            | After "file:///" v ->
                // fix, assuming windows
                if System.IO.Path.DirectorySeparatorChar = '\\' && v.Contains "/" then
                    v |> System.String.replace "/" "\\"
                else v
            | v -> v
        )
        |> Option.defaultValue location

    // https://stackoverflow.com/questions/52797/how-do-i-get-the-path-of-the-assembly-the-code-is-in
    let tryGetLocation(asm:System.Reflection.Assembly) =
        [
        // framework only
            CodeBaseUri,fun () -> Uri(asm.CodeBase).LocalPath
            CodeBase,fun () -> asm.CodeBase
            Location,fun () -> asm.Location
        ]
        |> List.choose(Tuple2.mapSnd (fun f -> Option.ofTryF f ()) >> Option.ofSnd)
        |> List.tryHead
        |> Option.map(fun (prefix,path) -> {LocationType=prefix; Path=fixLocationInfo path})

    let getReflectInfo(asm:System.Reflection.Assembly) =
        asm.GetCustomAttributes(typeof<System.Reflection.AssemblyVersionAttribute>, false)
        |> Option.ofObj
        |> Option.defaultValue Array.empty
        |> List.ofArray
        |> function
            | [] -> None
            | (:? System.Reflection.AssemblyVersionAttribute as ava)::_ -> 
                Some {
                    AssemblyName=asm.FullName
                    Version=ava.Version
                    Location= tryGetLocation asm |> Option.map (_.Path) |> Option.defaultValue null
                    }
            | _ -> None
