// really clean and package just the cred provider source for building remotely

// assumes we've already built the other dlls in release mode

#load @".\CredentialHelper\Reusable.fs"
// expects to run from sln root.
open System
open Reusable

let inline formatRelPath path =
    match path with
    | ValueString path ->
        try
            sprintf "'%s'('%s')" path (System.IO.Path.GetFullPath path)
        with _ ->
            eprintfn "Failed to format path '%s'" path
            $"'%s{path}'"
    | WhiteSpace _ -> "<wspace>"
    | NullString -> "<null>"
    | EmptyString -> "<empty>"

let failIfDirDoesntExist =
    function
    | ValueString path ->
        if System.IO.Directory.Exists path then
            path
        else formatRelPath path |> failwithf "Dir path not found %s"
    | _ ->
        invalidArg "path" "Path was invalid"

let targetPath = System.IO.Path.Combine(@".", "WindowsCredentialProviderTest") |> System.IO.Path.GetFullPath |> failIfDirDoesntExist
// only clean directories we don't want on target machine
// will have to adjust project paths on remote machine manually
[
    "output"
    "obj"
    "bin\\DEBUG"
]
|> Seq.choose(fun v ->
    let pathToClean = System.IO.Path.Combine(targetPath,v)
    if System.IO.Directory.Exists pathToClean then
        Some pathToClean
    else None
)
|> Seq.iter(fun fp ->
    failIfDirDoesntExist fp |> ignore<string>
    System.IO.Directory.Delete(fp, true)
    try
        System.IO.Directory.CreateDirectory(fp) |> ignore
    with _ -> ()
)
let releasePath = @".\WindowsCredentialProviderTest"
let targetZipFullPath = @".\WCPT src.zip"

printfn "Zipping %s -> %s" (formatRelPath releasePath) (formatRelPath targetZipFullPath)
System.IO.Compression.ZipFile.CreateFromDirectory(releasePath, targetZipFullPath)

