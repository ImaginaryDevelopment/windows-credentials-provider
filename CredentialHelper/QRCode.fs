module CredentialHelper.QRCode

open Reusable
open ZXing
open ZXing.Common

open System.Drawing


type QrResult =
    | QrNotFound
    | QrCodeFound of string
    with
        member x.TryGetCode() =
            match x with
            | QrNotFound -> None
            | QrCodeFound value -> Some value

type QrManager () =
    //let reader = BarcodeReader()

    let reader = QrCode.QRCodeReader()

    member _.TryDecode(image:Bitmap, ct: System.Threading.CancellationToken) =
        try
            if ct.IsCancellationRequested then
                QrNotFound
            else
                let source = BitmapLuminanceSource image
                if ct.IsCancellationRequested then
                    QrNotFound
                else
                    let bm = BinaryBitmap(HybridBinarizer source)
                    if ct.IsCancellationRequested then
                        QrNotFound
                    else
                        reader.decode bm
                        |> Option.ofObj
                        |> Option.bind(fun r -> r.Text |> Option.ofValueString)
                        |> Option.map QrCodeFound
                        |> Option.defaultValue QrNotFound
        finally
            reader.reset()

