module CredentialHelper.ApiClient

open Reusable

open type System.String

let private encode x = System.Net.WebUtility.UrlEncode(x)

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

    let createWReq(verifiedBase, relPath) wrType =
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

        wr.UseDefaultCredentials <- true
        wr.UserAgent <- System.Environment.MachineName
        wr.PreAuthenticate <- true
        wr.ServicePoint.Expect100Continue <- false
        wr.ConnectionGroupName <- $"Thread-{System.Threading.Thread.CurrentThread.ManagedThreadId}"
        wr

    let tryGetResultString (wReq:System.Net.HttpWebRequest) =
        task {
            try
                use! wResp = wReq.GetResponseAsync()
                let wResp = wResp :?> System.Net.HttpWebResponse
                use rs = wResp.GetResponseStream()
                use sr = new System.IO.StreamReader(rs)
                let! rj = sr.ReadToEndAsync()
                return Ok rj
            with ex -> return Error ex
        }

open HttpWReq

let tryPingServer verifiedBase =
    task {
        try
            let wReq = HttpWReq.createWReq(verifiedBase, "/api/qr/ping") (WReqType.Get Map.empty)
            let! rj = HttpWReq.tryGetResultString wReq
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

let tryValidate verifiedBase (value: AuthPost) =
    // api/qr/auth
    task {
        try
            let wReq = HttpWReq.createWReq (verifiedBase,"/api/qr/auth") WReqType.Post
            use tw = new System.IO.StreamWriter(wReq.GetRequestStream()) :> System.IO.TextWriter
            let value = System.Text.Json.JsonSerializer.Serialize value
            printfn "Post value is '%s'" value

            do! tw.WriteAsync value
            do! tw.FlushAsync()

            return!  HttpWReq.tryGetResultString wReq

        with ex -> return Error ex
    }



