﻿module Reusable

open System

type Property<'t> = {
    Getter: unit -> 't
    Setter: 't -> unit
}

let failNullOrEmpty paramName x = if String.IsNullOrEmpty x then raise <| ArgumentOutOfRangeException paramName

let tee f x =
    f x
    x

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
