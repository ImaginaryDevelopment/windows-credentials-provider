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
let tryCreateCom =
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

let tryAllCom (guid, tnr, t, progId) (f:System.Func<obj,_>) =
    [
        "CreateByGuid", CreateByGuid guid
        "CreateByStringRef", CreateByStringRef tnr
        "CreateByType", CreateByType t
        "CreateByProgId", CreateByProgId progId
    ]
    |> Seq.map (Tuple2.mapSnd (fun x ->
        try
            let v = tryCreateCom x
            match v with
            | null -> Error "null value returned"
            | _ -> f.Invoke v |> Ok
        with ex ->
            eprintfn "Exception: %s" (ex.GetType().Name)
            Error ex.Message
    ))
