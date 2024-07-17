module Reusable

open System

type OnAction = delegate of unit -> unit
type OnAction<'t> = delegate of 't -> unit

type Property<'t> = {
    Getter: unit -> 't
    Setter: 't -> unit
}

let failNullOrEmpty paramName x = if String.IsNullOrEmpty x then raise <| ArgumentOutOfRangeException paramName

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


let (|ValueString|WhiteSpace|NonValueString|) =
    function
    | null -> NonValueString
    | x when not <| System.String.IsNullOrWhiteSpace x -> ValueString x
    | x when System.String.IsNullOrEmpty x -> NonValueString
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

    static member trim text =
        Option.ofValueString text
        |> Option.map (fun text -> text.Trim())
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
        | NonValueString -> text
        | _ -> text.Replace(delimiter, replacement)

    static member inline afterOrSelf delimiter text =
        String.tryAfter delimiter text |> Option.defaultValue text

    static member makeEndWith delimiter text =
        if String.endsWith delimiter text then text
        else text + delimiter

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

let inline tryParseInt x = fromParser System.Int32.TryParse x

let createDisposable (onDispose: unit -> unit) =
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

module Cereal =
    let deserialize<'t>(x:string) =
        System.Text.Json.JsonSerializer.Deserialize<'t>(x)
    let tryDeserialize<'t>(x: string) =
        try
            deserialize<'t>(x) |> Ok
        with ex ->
            Error(x,ex)
