module ShowKeyApp

open System
open System.IO

open FldVault.Core.Vaults
open FldVault.KeyServer

open ColorPrint
open CommonTools

type private KeySource =
  | KeyFile of string
  | KeyId of Guid

type private Options = {
  Source: KeySource option
  IncludePassphrase: bool
}

let run args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      None
    | "-p" :: rest ->
      rest |> parseMore { o with IncludePassphrase = true }
    | "-f" :: fileName :: rest ->
      let fileName = fileName |> Path.GetFullPath
      if fileName |> File.Exists |> not then
        cp $"\frError\fo: Input file \fy{fileName}\fo does not exist\f0."
        None
      else
        rest |> parseMore { o with Source = fileName |> KeyFile |> Some }
    | "-k" :: id :: rest ->
      let ok, guid = id |> Guid.TryParse
      if ok |> not then
        cp $"\frError\fo: Key id \fy{id}\fo is not a valid GUID\f0."
        None
      else
        rest |> parseMore { o with Source = guid |> KeyId |> Some }
    | [] ->
      if o.Source.IsNone then
        cp "\frError\fo: No input file or key id specified\f0."
        None
      else
        Some o
    | x :: _ ->
      cp $"\frError\fo Unknown option: \fc{x}\f0."
      None
  let oo = args |> parseMore {
    Source = None
    IncludePassphrase = false
  }
  match oo with
  | None ->
    Usage.usage "showkey"
    1
  | Some o ->
    let pkif =
      match o.Source with
      | Some (KeyFile fileName) ->
        fileName |> PassphraseKeyInfoFile.TryFromFile
      | Some (KeyId id) ->
        let kss = new KeyServerService()
        if kss.ServerAvailable then
          kss.LookupKeyInfoAsync(id) |> Async.AwaitTask |> Async.RunSynchronously
        else
          null
      | None ->
        failwith "Key source is not specified"
    if pkif = null then
      cp $"\fo: Could not get key\f0 (unsupported format)."
      1
    else
      let zkey = Zkey.FromPassphraseKeyInfoFile(pkif)
      let info = zkey.ToZkeyTransferString(o.IncludePassphrase)
      cp $"Key\f0:"
      let color = if o.IncludePassphrase then "\fy" else "\fg"
      cp $"{color}{info}\f0"
      0
