module CredentialHelper.AppSettings

open BReusable

type AppConfig = {
    DevApi: string
    Expect100Continue: bool option
    SecurityProtocolType: System.Net.SecurityProtocolType option
    UseDefaultCredentials: bool option
}

let configDefault = {
    DevApi = null
    Expect100Continue = Some false
    SecurityProtocolType = Some System.Net.SecurityProtocolType.Tls12
    UseDefaultCredentials = Some true
}

module Env =
    let tryGetEnv value =
        match value with
        | ValueString _ ->
            try
                System.Environment.GetEnvironmentVariable value
                |> Option.ofValueString
                |> Option.orElseWith(fun () ->
                    value |> System.String.toLower
                    |> System.Environment.GetEnvironmentVariable
                    |> Option.ofValueString
                )
                |> Option.orElseWith(fun () ->
                    value |> System.String.toUpper
                    |> System.Environment.GetEnvironmentVariable
                    |> Option.ofValueString
                )
            with ex ->
                let t = tryGetTypeName ex
                eprintfn $"Env var: %s{t}: %s{ex.Message}"
                None
        | _ ->
            eprintfn "tryGetEnv fed empty string"
            None

//let sources = [
//    "devapi", fun v -> v.DevApi
//    "expect100", fun v -> v.Expect100Continue
//]

module IO =
    type ExistingFile = ExistingFile of string

    let (|FileExists|DirectoryExists|NotFound|) (path:string)=
        if System.IO.File.Exists path then
            FileExists (ExistingFile path)
        elif System.IO.Directory.Exists path then
            DirectoryExists path
        else NotFound

    let toFileExistsOption =
        function
        | FileExists fp -> Some fp
        | _ -> None

open IO

let getConfig (reporter:Reporter) (deserializer: IDeserializerR) pathOverride : AppConfig option =
    let defaultFp = "CredentialHelper.json"

    let tryPath fpo =
        let fpo =
            if System.IO.Path.IsPathRooted fpo then fpo
            else System.IO.Path.GetFullPath fpo
        // check for directory or file
        match fpo with
        | FileExists fpo -> Some fpo
        // check we've been passed a path to a directory where our default filename exists
        | DirectoryExists dirPath ->
            System.IO.Path.Combine(dirPath, defaultFp)
            |> toFileExistsOption
        | _ -> None

    // account for a directory, a relative path, a nonvalue string
    match pathOverride with
    | Some (ValueString fpo) -> fpo
    | _ -> defaultFp
    |> tee(fun v -> reporter.Log $"Searching in '{v}' for a config")
    |> tryPath
    |> Option.bind(fun (ExistingFile efp) ->

        reporter.Log $"Found file:'{efp}'"

        let text = System.IO.File.ReadAllText efp

        reporter.Log $"Read config file text"

        let result =
            text
            |> deserializer.Deserialize

        reporter.Log<_>("Deserialized", result)

        result
    )

// priority env > file > compiled defaults
let getConfiguration deserializer reporter pathOverride =
    let deserializer = PackageAdapters.Cereal.makeDeserializerR reporter deserializer

    let inline tryGetEnvOrDefault deserializer defaultValue name =
        name |> Env.tryGetEnv |> Option.map deserializer |> Option.defaultValue defaultValue

    let appConfig =
        getConfig reporter deserializer pathOverride |> Option.defaultValue configDefault

    let devApi =
        nameof configDefault.DevApi
        |> tryGetEnvOrDefault id appConfig.DevApi

    let expect100 =
        nameof configDefault.Expect100Continue
        |> tryGetEnvOrDefault deserializer.Deserialize appConfig.Expect100Continue

    let spt =
        nameof configDefault.SecurityProtocolType
        |> tryGetEnvOrDefault deserializer.Deserialize appConfig.SecurityProtocolType

    let udc =
        nameof configDefault.UseDefaultCredentials
        |> tryGetEnvOrDefault deserializer.Deserialize appConfig.UseDefaultCredentials

    {
        DevApi = devApi
        Expect100Continue = expect100
        SecurityProtocolType = spt
        UseDefaultCredentials = udc
    }
