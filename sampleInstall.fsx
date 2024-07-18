open System.IO
open System.Diagnostics

let srcDriveLetter = 'z'
let locations =
    [ @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
      @"C:\Windows\Microsoft.NET\Framework64\v2.0.50727\RegAsm.exe"
      @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"
      @"C:\Windows\Microsoft.NET\Framework\v2.0.50727\RegAsm.exe" ]

let quoted x = $"\"{x}\""
let startsWithI delimiter (path:string) =
    if System.String.IsNullOrEmpty delimiter then failwith "Bad Delimiter"
    path.StartsWith(delimiter, System.StringComparison.InvariantCultureIgnoreCase)

let createDirIfNotExists path =
    if System.String.IsNullOrWhiteSpace path then failwith "Bad Path"
    if Directory.Exists path |> not then
        Directory.CreateDirectory path |> ignore<DirectoryInfo>
    path

let regAsmPath =
    locations
    |> Seq.tryFind (System.IO.File.Exists)
    |> function
        | None -> failwith "Unable to find RegAsm.exe"
        | Some regAsmPath ->
            printfn "Found regasm at '%s'" regAsmPath
            regAsmPath

let runProcess cmd args =
    use p = new Process()
    p.StartInfo.FileName <- cmd
    p.StartInfo.Arguments <- args
    p.Start() |> ignore<bool>
    p.WaitForExit()

let register dllFullPath =
    if System.IO.File.Exists dllFullPath |> not then
        failwithf "Could not find dll '%s'" dllFullPath

    let toRegister =
        //let fn = Path.GetFileNameWithoutExtension dllFullPath
        //let p = Path.GetDirectoryName dllFullPath
        //Path.Combine(p,fn) |> quoted
        dllFullPath |> quoted

    printfn "Registering '%s' with '%s'" toRegister dllFullPath
    runProcess regAsmPath toRegister

let localPath = @"c:\net481" |> createDirIfNotExists

let dllFullPath = 
    let dllDirPath = string srcDriveLetter + @":\qrprov\qrlogon\net481\"
    let dllName = "WindowsCredentialProviderTest.dll" // "qrlogonclient.dll"
    Path.Combine(dllDirPath, dllName)

// copy newest files in
let makeLocalPath (fullPath: string) =
    if fullPath |> startsWithI localPath |> not then
        if fullPath |> startsWithI "c:" then failwith "Unimplemented scenario, making local a local path that is not in the expected folder"
        else Path.Combine(localPath, Path.GetFileName fullPath)
    else fullPath

if
    dllFullPath |> startsWithI "C:" |> not
then
    let dir = Path.GetDirectoryName dllFullPath
    printfn "Checking '%s' for files to copy" dir
    Directory.EnumerateFiles dir
    |> Seq.iter (fun fn ->
        try
            let l = makeLocalPath fn
            printfn "Copying '%s' to '%s'" fn l
            File.Copy(fn, l, overwrite = true)
        with ex ->
            eprintfn "Failed to copy: '%s' - '%s'" fn ex.Message)

let localDllFullPath = makeLocalPath dllFullPath

printfn "Registering '%s'" <| Path.GetFileName dllFullPath
register localDllFullPath
