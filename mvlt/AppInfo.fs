module AppInfo

open System
open System.IO

open FldVault.Core.Crypto
open FldVault.Core.Mvlt
open FldVault.KeyServer

open ColorPrint
open CommonTools
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type private Options = {
  InputVault: string
  Check: bool
}

let private runInfo o =
  let vault = o.InputVault
  use inputStream = File.OpenRead(vault)
  let headerTask =
    task {
      let! header = MvltFileHeader.ReadAsync(inputStream)
      return header
    }
  let header =
    headerTask
    |> Async.AwaitTask
    |> Async.RunSynchronously
  use keyChain = new KeyChain()
  let kss = new KeyServerService()
  let keyId = header.KeyInfoFile.KeyId
  cp $"Looking up key \fc{keyId}\f0."
  let status =
    if kss.ServerAvailable |> not then
      cp "\foKey server is not available\f0."
      1
    else
      let presence = kss.LookupKeySync(keyId, keyChain)
      match presence with
      | KeyPresence.Unavailable ->
        cp $"\foKey \fy{keyId}\fo not found on server. \fyRegistering a request for it\f0."
        kss.RegisterFileSync(o.InputVault, keyChain) |> ignore
        1
      | KeyPresence.Cloaked ->
        cp $"\foKey \fy{keyId}\fo is available, but hidden\f0."
        1
      | KeyPresence.Present ->
        cp $"\fgKey \fy{keyId}\fg is available\f0."
        0
      | _ ->
        cp $"\frKey \fy{keyId}\fr has an unrecognized status\f0."
        1
  if status <> 0 then
    status
  else
    use reader = header.CreateReader(
      keyChain,
      inputStream,
      false)
    let mutable contentSize = 0L
    let mutable decompressedSize = 0L
    let infoTask =
      if o.Check then
        task {
          do! reader.LoadNextBlock()
          if reader.BlockType <> MvltFormat.Preamble4CC then
            cp $"\frError\fo: Expected Preamble4CC block, but got \fy{reader.BlockType:X08}\f0."
            failwith "Invalid block type"
          cp $"Preamble: {reader.BlockContentSize} bytes ({reader.BlockOriginalSize})"
          let phase = reader.DecryptBlock()
          let preambleText = reader.GetMetadataText()
          let metadata = JsonConvert.DeserializeObject<JObject>(preambleText)
          cp $"          \fg{preambleText}\f0."
          reader.CyclePhase(phase) |> ignore // temporary hack
          while reader.Phase < MvltPhase.End do
            do! reader.LoadNextBlock()
            let phase = reader.DecryptBlock()
            reader.CyclePhase(phase) |> ignore // temporary hack
            if phase = MvltPhase.Data then
              cp $"Data:     {reader.BlockContentSize} bytes ({reader.BlockOriginalSize})"
              contentSize <- contentSize + int64(reader.BlockContentSize)
              decompressedSize <- decompressedSize + int64(reader.BlockOriginalSize)
            elif phase = MvltPhase.End then
              cp $"End:      {reader.BlockContentSize} bytes ({reader.BlockOriginalSize})"
              let terminatorJson = reader.GetMetadataText()
              cp $"          \fg{terminatorJson}\f0."
              let terminator = JsonConvert.DeserializeObject<JObject>(terminatorJson)
              for prop in terminator.Properties() do
                metadata[prop.Name] <- prop.Value
            else
              cp $"\frError\fo: Unknown block phase \fy{reader.Phase}\f0."
              failwith "Unknown block phase"
          return metadata |> Some
        }
      else
        task {
          do! reader.LoadNextBlock()
          if reader.BlockType <> MvltFormat.Preamble4CC then
            cp $"\frError\fo: Expected Preamble4CC block, but got \fy{reader.BlockType:X08}\f0."
            failwith "Invalid block type"
          cp $"Preamble: {reader.BlockContentSize} bytes ({reader.BlockOriginalSize})"
          while reader.IgnoreBlock() do
            do! reader.LoadNextBlock()
            let phase = reader.ValidatePhase()
            if phase = MvltPhase.Data then
              cp $"Data:     {reader.BlockContentSize} bytes ({reader.BlockOriginalSize})"
              contentSize <- contentSize + int64(reader.BlockContentSize)
              decompressedSize <- decompressedSize + int64(reader.BlockOriginalSize)
            elif phase = MvltPhase.End then
              cp $"End:      {reader.BlockContentSize} bytes ({reader.BlockOriginalSize})"
            else
              cp $"\frError\fo: Unknown block phase \fy{reader.Phase}\f0."
              failwith "Unknown block phase"
          return None
        }
    let metadataOption = infoTask |> Async.AwaitTask |> Async.RunSynchronously
    cp $"Total:    {contentSize} bytes ({decompressedSize})"
    match metadataOption with
    | None ->
      ()
    | Some metadata ->
      let json = JsonConvert.SerializeObject(metadata, Formatting.Indented)
      cp $"Metadata: \fg{json}\f0."
    0

let run args =
  let rec parseMore o args =
    match args with
    | "-v":: rest ->
      verbose <- true
      rest |> parseMore o
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-a" :: vaultFile :: rest ->
      rest |> parseMore { o with InputVault = vaultFile }
    | "-check" :: rest
    | "-c" :: rest ->
      rest |> parseMore { o with Check = true }
    | [] ->
      if o.InputVault |> String.IsNullOrEmpty then
        cp "\foNo Input vault specified\f0."
        None
      elif o.InputVault |> File.Exists |> not then
        cp $"\foFile not found: {o.InputVault}\f0."
        None
      else
        Some o
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  let oo = args |> parseMore {
    InputVault = null
    Check = false
  }
  match oo with
  | None ->
    Usage.usage "info"
    1
  | Some o ->
    o |> runInfo

