module AppCreate

open System
open System.IO

open FldVault.Core.Crypto
open FldVault.Core.Vaults
open FldVault.KeyServer

open FileUtilities

open ColorPrint
open CommonTools
open FldVault.Core.Mvlt

type private Options = {
  InputFile: string
  OutputFolder: string
  KeyFile: string option
}

let getKeyInfoFromFile fileName =
  let pkif = PassphraseKeyInfoFile.TryFromFile(fileName)
  if pkif = null then
    cp $"\frError\fo: Could not get key from \fy{fileName}\f0."
    None
  else
    // let zkey = Zkey.FromPassphraseKeyInfoFile(pkif)
    // cp $"\fcDBG\fo Key Info: \n\fg{zkey.ToZkeyTransferString(false)}\f0."
    Some pkif

let private runCreate o =
  let pkif = o.KeyFile |> Option.bind getKeyInfoFromFile    
  let kss = new KeyServerService()
  use keyChain = new KeyChain()
  let status =
    if kss.ServerAvailable |> not then
      cp "\frError\fo: Key server is not available\f0."
      1
    elif pkif.IsNone then
      cp "\frError\fo: Key not specified\f0."
      1
    else
      let pkif = pkif.Value
      let keyId = pkif.KeyId
      let presence = kss.LookupKeySync(keyId, keyChain)
      //cp $"\fcDBG\f0 key presence: \fy{presence}\f0."
      match presence with
      | KeyPresence.Unavailable ->
        cp $"\frError\fo: Key \fy{keyId}\f0 not yet found on server."
        kss.RegisterFileSync(o.KeyFile.Value, keyChain) |> ignore
        1
      | KeyPresence.Cloaked ->
        cp $"\frError\fo: Key \fy{keyId}\f0 is available, but hidden."
        1
      | KeyPresence.Present ->
        0
      | _ ->
        cp $"\frError\fo: Key \fy{keyId}\f0 has an unrecognized status."
        1
  if status <> 0 then
    status
  else
    let pkif = pkif.Value
    let sinkName =
      MvltWriter.DeriveMvltFileName(o.InputFile, pkif.KeyId)
      |> Path.GetFileName
    let sinkName =
      if o.OutputFolder |> String.IsNullOrEmpty then
        Path.Combine(Environment.CurrentDirectory, sinkName)
      else
        Path.Combine(o.OutputFolder, sinkName)
    let sinkShort = Path.GetFileName(sinkName)
    cp $"Saving \fg{sinkShort}\f0."
    let metafile = o.InputFile + ".meta.json"
    let metaShort = Path.GetFileName(metafile)
    if File.Exists(metafile) then
      cp $"Metadata file \fc{metaShort}\f0 found. Including it in the vault."
    else
      cp $"\fkNo metadata file ({metaShort}) found: not including extra metadata\f0."
    let saveTask =
      task {
        let! name = MvltWriter.CompressAndEncrypt(
          o.InputFile, sinkName, keyChain, pkif)
        return name
      }
    let name =       
      saveTask
      |> Async.AwaitTask
      |> Async.RunSynchronously
    cp $"Saved to \fg{name}\f0."
    0

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
      elif o.KeyFile.IsNone then
        cp "\frError\fo: Key not specified\f0."
        None
      elif o.OutputFolder |> String.IsNullOrEmpty then
        let inputFolder =
          o.InputFile |> Path.GetFullPath |> Path.GetDirectoryName
        if FileIdentifier.AreSame(inputFolder, Environment.CurrentDirectory) then
          cp "\frError\fo: Output folder not specified\fy (\fg-of\fy is required if input folder is current directory)\f0."
          None
        else
          // If input folder is not the current directory, then default
          // to current directory as output folder (else error)
          Some {o with OutputFolder = Environment.CurrentDirectory}
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
      rest |> parseMore { o with KeyFile = Some keysource }
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
    KeyFile = None
  }
  match oo with
  | None ->
    cp ""
    Usage.usage "create"
    1
  | Some o ->
    o |> runCreate
