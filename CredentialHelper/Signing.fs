module CredentialHelper.Signing

open BReusable

type SigningError =
    | StatusError of (int*(OutputType*string)list)
    // keys of ds reg sections found
    | WorkplaceNotFound of string list


let getSigningFunc () =
    // TODO: assuming the only 1 workplace is the right one for now
    let m = CredentialHelper.Reusable.ProcessAdapters.DsRegCmd.getStatus() |> Result.mapError StatusError

    m
    |> Result.map CredentialHelper.Reusable.ProcessAdapters.DsRegCmd.getWorkplaces
    |> Result.bind (fun m ->
        let workplaceNotFoundError = lazy(m.Keys |> List.ofSeq |>  WorkplaceNotFound |> Error)
        m
        |> Map.tryHead
        |> Option.map Ok
        |> Option.defaultValue workplaceNotFoundError.Value
    )
