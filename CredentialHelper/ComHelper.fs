module CredentialHelper.ComHelper

open Reusable

type TypeNameReference = {
    AssemblyName:string
    TypeName: string
}

type CreateArgs =
    | CreateByGuid of System.Guid
    | CreateByStringRef of TypeNameReference
    | CreateByType of System.Type
    | CreateByProgId of string

//System.Activator.GetObject()
let createCom =
    function
    | CreateByGuid guid ->
        let t = System.Type.GetTypeFromCLSID guid
        System.Activator.CreateInstance t
    | CreateByStringRef tnr ->
        System.Activator.CreateInstance(tnr.AssemblyName,tnr.TypeName)
    | CreateByType t ->
        System.Activator.CreateInstance t
    | CreateByProgId progId ->
        System.Type.GetTypeFromProgID progId
        |> Option.ofObj
        |> Option.map System.Activator.CreateInstance
        |> Option.defaultValue null

let tryComCreateByGuid guid =
    try
        createCom (CreateByGuid guid)
        |> Ok
    with ex -> Error ($"{tryGetTypeName ex}:%s{ex.Message}")

let tryAllCom (guid, tnrOpt, t, progIdOpt) (f:System.Func<obj,_>) =
    [
        "CreateByGuid", CreateByGuid guid
        match tnrOpt with
        | Some tnr ->
            "CreateByStringRef", CreateByStringRef tnr
        | None -> ()

        "CreateByType", CreateByType t
        match progIdOpt with
        | None -> ()
        | Some (ValueString progId) ->
            "CreateByProgId", CreateByProgId progId
        | _ -> ()
    ]
    |> Seq.map (Tuple2.mapSnd (fun x ->
        try
            let v = createCom x
            match v with
            | null -> Error "null value returned"
            | _ -> f.Invoke v |> Ok
        with ex ->
            eprintfn "Exception: %s" (ex.GetType().Name)
            Error ex.Message
    ))

let tryGetCredProvFilter() =
    let root = Microsoft.Win32.Registry.LocalMachine
    let path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Provider Filters"
    let tryGetAllCom guid =
        let value =
            tryAllCom(guid,None,typeof<CredentialProvider.Interop.ICredentialProviderFilter>,None) (System.Func<_,_>(function | :? CredentialProvider.Interop.ICredentialProviderFilter as icpf -> Some icpf | _ -> eprintfn "Hello casting fail"; None))
        value |> Some

    RegistryAdapters.Registry.withKey root path (fun sk ->
        sk.GetSubKeyNames()
        |> Seq.choose tryParseGuid
        |> List.ofSeq
        |> Ok
    )
    |> fun x -> x
    |> function
        | Error e -> eprintfn "%s" e; None
        | Ok [] -> eprintfn "No credential provider filters found in %s\\%s" root.Name path; None
        | Ok (h::[]) ->
            tryGetAllCom h
        | Ok (h:: rst) ->
            printfn "Found multiple credential filters(%i)" (rst.Length + 1)
            tryGetAllCom h
