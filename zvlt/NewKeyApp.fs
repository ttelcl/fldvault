module NewKeyApp

open System
open System.IO

open FldVault.Core
open FldVault.Core.Crypto
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools

type KeyKind =
  | Passphrase

  // | NullKey // Fooled! The null key does not have a real *.null.key-info file, so no need to support

type private NewKeyOptions = {
  Folder: string
  KeyKind: KeyKind option
}

let runNewKey args =
  let rec parsemore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parsemore o
    | "-h" :: _ ->
      None
    | "-p" :: rest ->
      rest |> parsemore {o with KeyKind = Some(KeyKind.Passphrase)}
    | "-dv" :: folder :: rest ->
      if folder |> Directory.Exists |> not then
        failwith $"Unknown directory {folder}"
      rest |> parsemore {o with Folder = folder}
    | [] ->
      if o.KeyKind |> Option.isNone then
        failwith $"No key kind specified. Consider '-p' (for a passphrase based key)"
      Some(o)
    | x :: _ ->
      failwith $"Unrecognized argument: {x}"
  let oo = args |> parsemore {
    Folder = Environment.CurrentDirectory
    KeyKind = None
  }
  match oo with
  | Some(o) ->
    match o.KeyKind with
    | Some(KeyKind.Passphrase) ->
      use key1 = KeyEntry.enterNewKey "Enter key"
      // cp $"That's key ID \fc{key1.GetId()}\f0."
      use key2 = KeyEntry.enterKey "Reenter key" key1.Salt
      // cp $"That's key ID \fc{key2.GetId()}\f0."
      if key1.GetId() <> key2.GetId() then
        failwith "The keys are different."
      let pkif = new PassphraseKeyInfoFile(key1)
      cp $"Initializing \fg{pkif.DefaultFileName}\f0 in \fc{o.Folder}\f0."
      let vaultsFolder = new VaultsFolder(o.Folder)
      let fnm = vaultsFolder.PutKeyInfo(pkif)
      0
    | None ->
      failwith "No key kind provided"
  | None ->
    Usage.usage "key-new"
    0




