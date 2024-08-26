namespace CredentialHelper.Reusable.RegistryAdapters

open BReusable
open CredentialHelper.Reusable

module Registry =
    let withKey (root:Microsoft.Win32.RegistryKey) (path:string) f =
        use sk = root.OpenSubKey(path)
        if isNull sk then
            Error $"Could not open - %s{root.Name}\\%s{path}"
        else
            f sk

open Registry

module Diag =
    type DiagnosticTargetingType =
        | All
        | SpecificKeys of string list

    let outputRegistryInfo (dllComGuid: System.Guid) =
        let indentValue = "\t"
        let guidStr = dllComGuid |> string |> System.String.toUpper |> sprintf "{%s}"
        let getNext indent = $"%s{indent}{indentValue}"

        let rec walkTree (root:Microsoft.Win32.RegistryKey) indent limit (path,tdd) =
            withKey root path (fun sk ->
                // diagnostics
                let filter =
                    match tdd with
                    | DiagnosticTargetingType.All -> id
                    | DiagnosticTargetingType.SpecificKeys items ->
                        Seq.filter(fun vn -> items |> List.exists(fun cvn -> vn = cvn))

                let vns = sk.GetValueNames()
                //printfn "%s\\%s - %i values" root.Name path <| Seq.length vns
                vns
                |> filter
                |> Seq.iter(fun vn ->
                    let v =
                        try
                            sk.GetValue vn
                        with _ -> "<null>"
                    printfn "%s%s - %A" indent vn v
                )
                sk.GetSubKeyNames()
                |> Seq.iter(fun skn ->
                    printfn "%s%s" indent skn
                    if limit > 1 then
                        walkTree root (getNext indent) (limit - 1) (skn,tdd)
                )
                Ok ()
            )
            |> function
                | Error e -> printfn "%s%s" indent e
                | Ok () -> ()

        let expected = "{298D9F84-9BC5-435C-9FC2-EB3746625954}"
        if guidStr <> expected then
            eprintfn "Guid issue %s <> %s" guidStr expected

        let printEmptyBanner() = printfn "------"

        printEmptyBanner()
        [
            Microsoft.Win32.Registry.LocalMachine, [
                //https://superuser.com/a/392309/39536
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\CredUI", DiagnosticTargetingType.All
                // has a value called dontdisplaylastusername that may be relevant
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", DiagnosticTargetingType.SpecificKeys ["dontdisplaylastusername"]

                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\" + guidStr, DiagnosticTargetingType.All

                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device", DiagnosticTargetingType.All

                // https://stackoverflow.com/questions/255669/how-to-enable-assembly-bind-failure-logging-fusion-in-net
                //@"SOFTWARE\Microsoft\Fusion"
            ]
            //Microsoft.Win32.Registry.ClassesRoot, @"CLSID\" + guidStr
        ]
        |> List.collect(fun (r,p) -> p |> List.map(fun item -> r,item))
        |> List.iter(fun (r,(p,dtt)) ->
            try
                printfn "Attempting to check %s-%s" r.Name p
                walkTree r indentValue 2 (p,dtt)
            with ex ->
                let t = tryGetTypeName ex
                eprintfn "Failed to walk %s - %s: %s(%s)" r.Name p t ex.Message
            printEmptyBanner()
        )
        printEmptyBanner()

    let outputDsRegInfo() =
        printfn "DsReg:"
        ProcessAdapters.DsRegCmd.getStatus()
        |> function
            | Ok outs ->
                outs
                |> Map.iter(fun category m ->
                    printfn "\t%s" category
                    m
                    |> Map.iter(fun key value ->
                        printfn "\t\t%s:%s" key value
                    )
                )
            | Error (ec,outs) ->
                ProcessAdapters.Helpers.printOuts outs
                eprintfn "Ec: %i" ec

    let outputDiagnostics (dllComGuid: System.Guid) =
        outputRegistryInfo dllComGuid
        //outputDsRegInfo()

