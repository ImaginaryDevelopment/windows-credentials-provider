module CredentialHelper.QRCode

open Reusable
open ZXing
open ZXing.Common

open System.Drawing

type QrManager () =
    //let reader = ZXing.BarcodeReader()

    let reader = QrCode.QRCodeReader()

    member _.TryDecode(image:Bitmap) =
        try
            let source = BitmapLuminanceSource(image)
            let bm = BinaryBitmap(HybridBinarizer(source))
            reader.decode bm
            |> Option.ofObj
            |> Option.bind(fun r -> r.Text |> Option.ofValueString)
        finally
            reader.reset()

