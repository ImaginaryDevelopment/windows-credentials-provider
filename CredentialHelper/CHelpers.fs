[<System.Runtime.CompilerServices.Extension>]
module CredentialHelper.CHelpers
open System.Runtime.CompilerServices

[<Extension>]
let toList (this: 't seq) =
    List.ofSeq this

