module CredentialHelper.Reusable.CertAdapters

open System.Security.Cryptography.X509Certificates

let getSystemCerts() =
    printfn "----------------------"
    printfn "Checking store"
    let names =
        [
            nameof StoreName.My,StoreName.My
            nameof StoreName.AuthRoot,StoreName.AuthRoot
            nameof StoreName.Root,StoreName.Root
        ]

    let locs = [
        nameof StoreLocation.LocalMachine,StoreLocation.LocalMachine
        nameof StoreLocation.CurrentUser,StoreLocation.CurrentUser
    ]
    for (nDisp, n) in names do
        let tryFindCerts lOpt =
            let l =
                match lOpt with
                | None ->
                    printfn "\t_-%s" nDisp
                    None
                | Some (ldisp,l:StoreLocation) ->
                    printfn "\t%s-%s" ldisp nDisp
                    Some l
            use store =
                match l with
                | None -> new X509Store(n)
                | Some l ->
                    new X509Store(n, l)
            store.Open(OpenFlags.ReadOnly)
            printfn "Found %i cert(s)" store.Certificates.Count
            (store.Certificates |> Seq.cast<X509Certificate2>)
            |> Seq.iter(fun v -> printfn "%s-%A" v.FriendlyName v.Thumbprint)
        tryFindCerts None
        for (lDisp,l) in locs do
            tryFindCerts(Some (lDisp,l))
    ()


let outputDiagnostics() =
    getSystemCerts()

