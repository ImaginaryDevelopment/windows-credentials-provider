module Reusable

open System

type Property<'t> = {
    Getter: unit -> 't
    Setter: 't -> unit
}

let failNullOrEmpty paramName x = if String.IsNullOrEmpty x then raise <| ArgumentOutOfRangeException paramName

let (|ValueString|WhiteSpace|NonValueString|) =
    function
    | null -> NonValueString
    | x when not <| System.String.IsNullOrWhiteSpace x -> ValueString x
    | x when System.String.IsNullOrEmpty x -> NonValueString
    | x -> WhiteSpace x

type System.String with
    static member contains delimiter text=
        failNullOrEmpty "delimiter" delimiter
        if String.IsNullOrEmpty text then
            false
        else text.Contains delimiter

let tryAfter (delimiter:string) (x:string) =
    failNullOrEmpty (nameof(delimiter)) delimiter
    match x.IndexOf delimiter with
    | i when i < 0 -> None
    | i -> x[i + delimiter.Length ..] |> Some

let replace delimiter replacement text =
    failNullOrEmpty (nameof delimiter) delimiter
    match text with
    | NonValueString -> text
    | _ -> text.Replace(delimiter,replacement)

let afterOrSelf delimiter x = x |> tryAfter delimiter |> Option.defaultValue x

let uncurry f (x,y) = f x y

let inline defer f x =
    fun () -> f x

let (|After|_|) delimiter text =
    text |> tryAfter delimiter

module Tuple2 =
    let mapSnd f (x,y) =
        x, f y
    let withSnd y x = x,y

let toDisposable (x: #System.IDisposable) =
    x :> System.IDisposable

let dispose (x:System.IDisposable) =
    x.Dispose()

let tryDispose title x =
    try
        dispose x
    with _ ->
        eprintfn "Failed to dispose: '%s'" title
