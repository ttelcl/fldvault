module KeyApp

open System
open System.IO

open FldVault.Core
open FldVault.Core.Crypto
open FldVault.Core.Vaults

open ColorPrint
open CommonTools

type KeyKind =
  | Passphrase

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
    | "-p" :: rest ->
      rest |> parsemore {o with KeyKind = Some(KeyKind.Passphrase)}
    | "-dv" :: folder :: rest ->
      if folder |> Directory.Exists |> not then
        failwith $"Unknown directory {folder}"
      rest |> parsemore {o with Folder = folder}
    | [] ->
      if o.KeyKind |> Option.isNone then
        failwith $"No key kind specified. Consider '-p' (for a passphrase based key)"
      o
    | x :: _ ->
      failwith $"Unrecognized argument: {x}"
  let o = args |> parsemore {
    Folder = Environment.CurrentDirectory
    KeyKind = None
  }
  let pkif =
    match o.KeyKind with
    | Some(KeyKind.Passphrase) ->
      use key1 = KeyEntry.enterNewKey "Enter key"
      // cp $"That's key ID \fc{key1.GetId()}\f0."
      use key2 = KeyEntry.enterKey "Reenter key" key1.Salt
      // cp $"That's key ID \fc{key2.GetId()}\f0."
      if key1.GetId() <> key2.GetId() then
        failwith "The keys are different."
      new PassphraseKeyInfoFile(key1)
    | None ->
      failwith "No key kind provided"
  cp $"Initializing \fg{pkif.DefaultFileName}\f0 in \fc{o.Folder}\f0."
  let vaultsFolder = new VaultsFolder(o.Folder)
  let fnm = vaultsFolder.PutKeyInfo(pkif)
  0

type private KeyOpOptions = {
  KeyTag: string option
  KeyFile: string option
  Folder: string
}

let private parseKeyOptions args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-key" :: keytag :: rest ->
      rest |> parseMore {o with KeyTag = Some(keytag)}
    | "-kf" :: file :: rest ->
      rest |> parseMore {o with KeyFile = Some(file)}
    | "-dv" :: folder :: rest ->
      if folder |> Directory.Exists |> not then
        failwith $"Unknown directory {folder}"
      rest |> parseMore {o with Folder = folder}
    | [] ->
      match o.KeyTag, o.KeyFile with
      | Some(_), None -> ()
      | None, Some(_) -> ()
      | None, None ->
        failwith "Expecting one of -key or -kf"
      | Some(_), Some(_) ->
        failwith "Expecting either -key or -kf, but not both"
      o
    | x :: _ ->
      failwith $"Unrecognized argument {x}"
  args |> parseMore {
    KeyTag = None
    KeyFile = None
    Folder = "."
  }

let runCheckKey args =
  let o = args |> parseKeyOptions
  0

let runUnlockKey args =
  let o = args |> parseKeyOptions
  0

let runLockKey args =
  let o = args |> parseKeyOptions
  0
