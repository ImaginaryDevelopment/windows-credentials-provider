namespace CredentialHelper.ProcessAdapters

open Reusable

module Helpers =
    let printOut =
        function
        | StdError, txt -> eprintfn "%s" txt
        | StdOut, txt -> printfn "%s" txt
    let printOuts outs = outs |> Seq.iter printOut

module Where =
    let where cmd =
        Process.executeProcessHarnessed "where" cmd
module DsRegCmd =

    type private DsRegState = | GatheringHeader | Headed of string

    let getStatus () =
        let cmd = "dsregcmd.exe"
        let ec, text =
            try
            Process.executeProcessHarnessed cmd "/status"
            with ex ->
                eprintfn "Needed where I guess?: %s-%s" (tryGetTypeName ex) ex.Message
                let ec, text = Where.where cmd
                //Helpers.printOuts text
                let cmd = text[0] |> snd
                //printfn "found at '%s'" cmd
                Process.executeProcessHarnessed cmd "/status"
        if ec <> 0 then
            Error (ec,text)
        else
            let errors, outs =
                ((List.empty,List.empty),text)
                ||> List.fold(fun (errors,outs) (t,line) ->
                    match t with
                    | StdError -> line::errors, outs
                    | StdOut -> errors, line::outs
                )
            errors
            |> Seq.iter (eprintfn "%s")

            ((Map.empty,GatheringHeader),outs |> List.rev)
            ||> Seq.fold(fun (m,state) line ->
                match state,line with
                // categories start and end with non-value lines
                | state, NonValueString
                | state, StartsWith "+--" ->
                    m, state
                | _, Trim(StartsWith "For more") -> m,state
                // header found
                | _, StartsWith "|" & txt ->
                    match txt with
                    | After "|" (Before "|" (Trim txt)) ->
                        m |> Map.add txt Map.empty,Headed txt
                    | _ -> failwith "Unexpected line state"
                | Headed category, Trim (ValueString' (Before ":" (Trim key) & After ":" (Trim value))) ->
                    let m =
                        m
                        |> Map.tryFind category
                        |> function
                            | Some sm ->
                                sm |> Map.tryFind key
                                |> function
                                    | Some value2 -> failwithf "Duplicate key found '%s.%s' ('%s','%s')" category key value2 value
                                    | None ->
                                        let smNext = sm |> Map.add key value
                                        m |> Map.add category smNext
                            | None -> 
                                failwithf "Invalid category to add sm: '%s'" category
                    m, Headed category
                | Headed category, _ ->
                    eprintfn "Unexpected state under '%s'" category
                    eprintfn "Line:'%s' -> '%s'" category line
                    printfn "Found:'%i' category(ies) %i total" m.Count (m |> Map.map(fun _ -> Map.count) |> Map.values |> Seq.sum)
                    failwithf "bad state"
                | GatheringHeader, _ ->
                    eprintfn "Line:'%s'" line
                    printfn "Found:'%i' category(ies) %i total" m.Count (m |> Map.map(fun _ -> Map.count) |> Map.values |> Seq.sum)
                    failwithf "bad state"

            )
            |> fst
            |> Ok


