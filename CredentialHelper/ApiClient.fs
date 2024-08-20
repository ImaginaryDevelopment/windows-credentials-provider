﻿module CredentialHelper.ApiClient

open Reusable

open CredentialHelper.PackageAdapters

open type System.String

let private encode x = System.Net.WebUtility.UrlEncode(x)

type VerificationResult = {
    Username: string
    Password: string
    Domain: string
}

type BaseUrl = private | VerifiedBase of string
with
    static member TryCreate value =
        value
        |> Option.ofValueString
        |> function
            | Some(ContainsS "://") -> value |> Ok
            | Some value -> "https://" + value |> Ok
            | None -> Error "Empty value"
        |> Result.map( System.String.makeEndWith "/" >> BaseUrl.VerifiedBase)

    member x.GetValue() =
        match x with
        | VerifiedBase v -> v

        // is http://google.com/?foo=bar legal?
    static member ToFullUrl (x:BaseUrl) relPath (queryParams:Map<string,string>) =
        Option.ofValueString relPath
        |> Option.map(fun v ->
            x.GetValue() + v.TrimStart('/')
        )
        |> Option.defaultWith x.GetValue
        |> fun url ->
            let sep = if url.Contains "?" then "&" else "?"
            let toAppend = queryParams |> Map.toSeq |> Seq.map(fun (k,v) -> $"{k}={encode v}") |> String.concat "&"
            url + sep + toAppend

// based on my old http client usage
[<System.Obsolete("Going to try with HttpWebRequest first")>]
module HttpClient =
    open System.Net.Http
    let httpClient =
        // we may not need cookies here, but example code had it
        let c = System.Net.CookieContainer()
        let handler = new HttpClientHandler(CookieContainer=c)
        // consider setting base origin here?
        new HttpClient(handler,disposeHandler=true)

    let fetch verifiedBase relPath queryParams =
        let uri =
            BaseUrl.ToFullUrl verifiedBase relPath queryParams
        task {
            let! response = httpClient.GetAsync(System.Uri uri)
            let! text = response.Content.ReadAsStringAsync()
            if response.IsSuccessStatusCode then
                return Ok text
            else
                return Error (response.StatusCode, response.ReasonPhrase, text)
        }

module HttpWReq =
    type WReqType =
        | Post
        | Get of queryParams: Map<string,string>

    let createWReq (config:AppSettings.AppConfig) (verifiedBase, relPath) wrType =
        let url =
            match wrType with
            | Post -> Map.empty
            | Get qp -> qp
            |> BaseUrl.ToFullUrl verifiedBase relPath

        printfn "Request will be sent to '%s'" url

        let wr = System.Net.HttpWebRequest.CreateHttp url

        wr.Method <-
            match wrType with
            | Post -> "POST"
            | Get _ -> "GET"
        config.UseDefaultCredentials
        |> Option.iter(fun udc ->
            wr.UseDefaultCredentials <- udc
        )

        wr.UserAgent <- System.Environment.MachineName
        wr.PreAuthenticate <- true
        config.Expect100Continue
        |> Option.iter(fun ec ->
            wr.ServicePoint.Expect100Continue <- ec
        )
        config.SecurityProtocolType
        |> Option.iter(fun spt ->
            System.Net.ServicePointManager.SecurityProtocol <- spt
        )
        wr.ConnectionGroupName <- $"Thread-{System.Threading.Thread.CurrentThread.ManagedThreadId}"
        wr

    let tryGetResultString (wReq:System.Net.HttpWebRequest, ct: System.Threading.CancellationToken) =
        task {
            ct.ThrowIfCancellationRequested()
            try
                printfn "Fetching response"
                use! wResp = wReq.GetResponseAsync().ConfigureAwait(false)
                ct.ThrowIfCancellationRequested()
                printfn "casting response"
                let wResp = wResp :?> System.Net.HttpWebResponse
                ct.ThrowIfCancellationRequested()
                printfn "getting response stream"
                use rs = wResp.GetResponseStream()
                ct.ThrowIfCancellationRequested()
                printfn "Getting stream reader"
                use sr = new System.IO.StreamReader(rs)
                ct.ThrowIfCancellationRequested()
                printfn "reading to end"
                let! rj = sr.ReadToEndAsync()
                ct.ThrowIfCancellationRequested()
                printfn "returning value: '%A'" rj
                return Ok rj
            with ex ->
                printfn "erroring out"
                return Error ex
        }

open HttpWReq

let tryPingServer config verifiedBase ct =
    task {
        try
            let wReq = HttpWReq.createWReq config (verifiedBase, "/api/qr/ping") (WReqType.Get Map.empty)
            let! rj = HttpWReq.tryGetResultString (wReq,ct)
            match rj with
            | Ok rj ->
                return Ok(rj = "PONG!")
            | Error e -> return Error e
        with ex -> return Error ex
    }

type AuthPost = {
    // int 64 
    Code: string
}

let tryValidate config verifiedBase (value: AuthPost, ct: System.Threading.CancellationToken) =
    // api/qr/auth
    let t = 
        backgroundTask {
            if ct.IsCancellationRequested then
                return Error "Cancelled"
            else
                let wReq = HttpWReq.createWReq config (verifiedBase,"/api/qr/auth") WReqType.Post
                ct.ThrowIfCancellationRequested()
                use tw = new System.IO.StreamWriter(wReq.GetRequestStream()) :> System.IO.TextWriter
                printfn "Serializing the value"
                match Cereal.serialize value with
                | Error e ->
                    let err = Error e
                    return Error e
                | Ok value ->
                    printfn "Post value is '%s'" value
                    do! tw.WriteAsync(value)
                    do! tw.FlushAsync()
                    printfn "post flushed"
                    ct.ThrowIfCancellationRequested()
                    let! result = HttpWReq.tryGetResultString (wReq, ct)
                    printfn "Got result string"
                    match result with
                    | Ok value ->
                        printfn "Got result:'%s'" value
                        return Ok value
                    | Error e ->
                        printfn "Result fetch failed"
                        return Error e.Message
        }
    t



