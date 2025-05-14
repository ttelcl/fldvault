module AppCreate

open System
open System.IO

open FldVault.Core.Crypto
open FldVault.KeyServer

open ColorPrint
open CommonTools

type KeySource =
  | KeyId of Guid
  | KeyInfoFile of string
  | ZvaultFile of string
  | MvaultFile of string

let parseKeySource (txt: string) =
  if txt.EndsWith(".pass.key-info", StringComparison.InvariantCultureIgnoreCase) then
    KeyInfoFile txt |> Some
  elif txt.EndsWith(".zvlt", StringComparison.InvariantCultureIgnoreCase) then
    ZvaultFile txt |> Some
  elif txt.EndsWith(".mvlt", StringComparison.InvariantCultureIgnoreCase) then
    MvaultFile txt |> Some
  else
    match Guid.TryParse(txt) with
    | true, g -> KeyId g |> Some
    | _ -> None

type private Options = {
  InputFile: string
  OutputFolder: string
  Key: KeySource option
}

let private runCreate o =
  let keyId =
    match o.Key with
    | Some(KeyId g) -> g
    | Some(KeyInfoFile f) -> 
      failwith "KeyInfoFile as key source not implemented"
    | Some(ZvaultFile f) ->
      failwith "ZvaultFile as key source not implemented"
    | Some(MvaultFile f) ->
      failwith "MvaultFile as key source not implemented"
    | None ->
      failwith "Internal error: Key source not specified"
  let kss = new KeyServerService()
  use keyChain = new KeyChain()
  if kss.ServerAvailable |> not then
    cp "\frError\fo: Key server is not available\f0."
    1
  else
    let presence = kss.LookupKeySync(keyId, keyChain)
    cp $"\fcDBG\f0 key presence: \fy{presence}\f0."
    cp "\fo'create' Not Yet Implemented\f0."
    1

let run args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      parseMore o rest
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | [] ->
      if o.InputFile |> String.IsNullOrEmpty then
        cp "\frError\fo: Input file not specified\f0."
        None
      elif o.OutputFolder |> String.IsNullOrEmpty then
        cp "\frError\fo: Output folder not specified\f0."
        None
      elif o.Key.IsNone then
        cp "\frError\fo: Key not specified\f0."
        None
      else
        Some o
    | "-f" :: file :: rest ->
      if file |> String.IsNullOrEmpty then
        cp "\frError\fo: Input file name is empty\f0."
        None
      else
        let file = file |> Path.GetFullPath
        if file |> File.Exists |> not then
          cp $"\frError\fo: Input file \fy{file}\fo does not exist."
          None
        else
          parseMore { o with InputFile = file } rest
    | "-k" :: keysource :: rest ->
      match keysource |> parseKeySource with
      | None ->
        cp $"\frError\fo: Key source \fy{keysource}\fo is not recognized."
        None
      | Some ks ->
        rest |> parseMore { o with Key = Some ks }
    | "-of" :: outFolder :: rest ->
      let outFolder = outFolder |> Path.GetFullPath
      if outFolder |> Directory.Exists |> not then
        cp $"\frError\fo: Output folder \fy{outFolder}\fo does not exist."
        None
      else
        rest |> parseMore { o with OutputFolder = outFolder }
    | x :: _ ->
      cp $"\frError\fo: Unknown argument \fy{x}\f0."
      None
  let oo = args |> parseMore {
    InputFile = ""
    OutputFolder = ""
    Key = None
  }
  match oo with
  | None ->
    cp ""
    Usage.usage "create"
    1
  | Some o ->
    o |> runCreate
