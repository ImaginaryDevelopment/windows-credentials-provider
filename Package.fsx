// package already built release files
#load @".\src\CredentialHelper.Reusable\Reusable.fs"
// expects to run from sln root.
open System
open BReusable

let overwriteZip = false

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

let failIfFileDoesntExist =
    function
    | ValueString path ->
        if System.IO.File.Exists path then
            path
        else formatRelPath path |> failwithf "File path not found %s"
    | _ ->
        invalidArg "path" "Path was invalid"

let failIfDirDoesntExist =
    function
    | ValueString path ->
        if System.IO.Directory.Exists path then
            path
        else formatRelPath path |> failwithf "Dir path not found %s"
    | _ ->
        invalidArg "path" "Path was invalid"

// validate guid locations
(
    let expectedGuid =
        let constantsPath = failIfFileDoesntExist @".\WindowsCredentialProviderTest\Constants.cs"
        System.IO.File.ReadAllLines constantsPath
        |> Seq.find (fun line -> line.Contains "CredentialProviderUID")
        |> String.afterOrSelf "="
        |> String.beforeOrSelf ";"
        |> String.trim
        |> String.trimc '"'
        |> tee (printfn "Found guid: %s")
        |> tryParseGuid
        |> function
            | Some eg -> string eg
            | _ -> failwith "Constant not found"


    let expectedPlaces = 
        [
            failIfFileDoesntExist @".\readme.md"
            failIfFileDoesntExist @".\register-credentials-provider.reg"
        ]

    expectedPlaces
    |> Seq.iter(fun relPath ->
        let found =
            System.IO.File.ReadAllText relPath
            |> String.containsI expectedGuid
        if not found then
            failwithf "Expectation failed: '%A' not found in '%s'(%s)" expectedGuid relPath (System.IO.Path.GetFullPath relPath)
    )
)

// decision point:
// include test console app, yes?
let releasePath = failIfDirDoesntExist @".\TestConsoleApp\bin\Release" |> System.IO.Path.GetFullPath |> failIfDirDoesntExist

// zip output
let atLeastXExtensions = [
    1,"reg"
    5,"dll"
]

let genBuildOutputZipFn i =
    //@"TestConsoleApp20240806_00"
    let dtPart = System.DateTime.Now.ToString "yyyyMMdd"
    let iPart = (string i).PadLeft(2,'0')
    $"TestConsoleApp{dtPart}_{iPart}.zip"

// create or wipe log file
(
    let logFilePath = System.IO.Path.Combine(releasePath,@"CredentialProviderLog.log.txt")
    System.IO.File.CreateText(logFilePath).Dispose()
)

// full path
let targetZipFullPath =
    // get the target output's parent folder
    let outputParent = System.IO.Path.GetDirectoryName releasePath |> failIfDirDoesntExist
    [0..10]
    |> Seq.choose(fun i ->
        let fullPath = System.IO.Path.Combine(outputParent, genBuildOutputZipFn i)
        if overwriteZip then Some fullPath
        elif System.IO.File.Exists fullPath then
            None
        else Some fullPath
    )
    |> Seq.tryHead
    |> function
        | None -> "Could not find a valid output fn"
        | Some fn -> fn

// generate build info, validate file extensions
(
    let inputMap =
        (Map.empty,System.IO.Directory.GetFiles releasePath)
        ||> Seq.fold(fun m fp ->
            if System.IO.Directory.Exists fp then
                m
            elif System.IO.File.Exists fp then
                match System.IO.Path.GetExtension fp with
                | ValueString ext ->
                    m |> Map.addListItem ext fp
                | _ ->
                    fp |> formatRelPath |> failwithf "Found bad extension:%s"
            else
                eprintfn "GetFiles returned invalid item '%s'" fp
                m
        )

    // detect cleaned directory
    atLeastXExtensions
    |> Seq.iter(fun (minCount,extension) ->
        let count = System.IO.Directory.GetFiles(releasePath, $"*.{extension}") |> Array.length
        if count < minCount then
            failwith $"Expected %i{minCount} or more .{extension}"
    )

    let inputMapLengths =
        inputMap
        |> Map.mapListLength

    let fileCount = ((0,inputMapLengths) ||> Map.fold(fun i _  v -> i + v))
    printfn "Zipping %i files" fileCount

    inputMap
    |> Map.iter(fun ext files ->
        printfn "\t%s->%i" ext (List.length files)
        match ext with
        | ValueString _ -> ()
        | _ ->
            files
            |> List.iter( printfn "\t\tExt? - '%s'" )
    )

    if overwriteZip && System.IO.File.Exists targetZipFullPath then
        System.IO.File.Delete targetZipFullPath

    [
        $"{fileCount} files"
        yield! inputMapLengths |> Map.toSeq |> Seq.map(fun (ext,v) ->
            sprintf "\t%s->%i" ext v
        )
    ]
    |> List.toArray
    |> fun lines ->
        // does this automatically overwrite?
        let buildInfoPath = System.IO.Path.Combine(releasePath,"buildInfo.txt")
        System.IO.File.WriteAllLines(buildInfoPath, lines)
)

printfn "zipping '%s' into '%s'" releasePath targetZipFullPath
System.IO.Compression.ZipFile.CreateFromDirectory(releasePath, targetZipFullPath)
