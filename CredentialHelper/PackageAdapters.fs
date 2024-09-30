namespace CredentialHelper.PackageAdapters

open BReusable

module Cereal =
    let deserialize<'t>(x:string) =
        Newtonsoft.Json.JsonConvert.DeserializeObject<'t>(x)
        //System.Text.Json.JsonSerializer.Deserialize<'t>(x)
    let tryDeserialize<'t>(x: string) =
        try
            deserialize<'t>(x) |> Ok
        with ex ->
            Error(x,ex)

    let serialize<'t>(x:'t) =
        try
            //System.Text.Json.JsonSerializer.Serialize x |> Ok
            Newtonsoft.Json.JsonConvert.SerializeObject x |> Ok
        with
            | :? System.TypeInitializationException as te ->
                match te.InnerException with
                | null -> Error $"{te.Message}:{te.StackTrace}"
                | ie -> Error $"TypeInitializerEx:{ie.Message}:{ie.StackTrace}";

    let deserializer =
        { new IDeserializer with
            member _.Deserialize<'t>(value:string) =
                let result = tryDeserialize<'t>(value)
                result
        }

    // for swallows and things that are not important to watch for failures
    let makeDeserializerR (reporter:Reporter) (deserializer:IDeserializer) =
        { new IDeserializerR with
            member _.Deserialize<'t>(value:string) =
                match deserializer.Deserialize<'t> value with
                | Ok v -> Some v
                | Error (msg,exn) ->
                    reporter.LogError(msg,exn)
                    None
        }

