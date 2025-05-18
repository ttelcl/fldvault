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
  let zkey = header.KeyInfoFile.ToZkey()
  let transferstring = zkey.ToZkeyTransferString(false)
  cp $"Key descriptor: \n\fG{transferstring}\f0\n"
  let keyAvailable =
    if kss.ServerAvailable |> not then
      cp "\foKey server is not available\f0."
      false
    else
      let presence = kss.LookupKeySync(keyId, keyChain)
      match presence with
      | KeyPresence.Unavailable ->
        cp $"\foKey \fy{keyId}\fo not found on server. \fyRegistering a request for it\f0."
        kss.RegisterFileSync(o.InputVault, keyChain) |> ignore
        false
      | KeyPresence.Cloaked ->
        cp $"\foKey \fy{keyId}\fo is available, but hidden\f0."
        false
      | KeyPresence.Present ->
        if o.Check |> not then
          cp $"\fyKey \fg{keyId}\fy would be available in \fgcheck\fy mode\f0."
        else
          cp $"Key \fg{keyId}\f0 is available\f0."
        true
      | _ ->
        cp $"\frKey \fy{keyId}\fr has an unrecognized status\f0."
        false
  let status = if keyAvailable then 0 else 1
  let checkmode =
    if o.Check && keyAvailable then
      true
    elif o.Check && not keyAvailable then
      cp "\foKey not available. Running in \fyinfo\fo mode, not \fycheck\fo mode\f0."
      false
    else
      false
  use reader = header.CreateReader(
    (if checkmode then keyChain else null),
    inputStream,
    false)
  let mutable contentSize = 0L
  let mutable decompressedSize = 0L
  let mutable datablockCount = 0
  let infoTask =
    if checkmode then
      task {
        let! blockOffset = reader.LoadNextBlock()
        if reader.BlockType <> MvltFormat.Preamble4CC then
          cp $"\frError\fo: Expected Preamble4CC block, but got \fy{reader.BlockType:X08}\f0."
          failwith "Invalid block type"
        cp $"\fk{blockOffset:X08}\f0 Preamble: \fb{reader.BlockContentSize}\f0 bytes"
        let phase = reader.DecryptBlock()
        let preambleText = reader.GetMetadataText()
        let metadata = JsonConvert.DeserializeObject<JObject>(preambleText)
        cp $"         \fg{preambleText}\f0."
        reader.CyclePhase(phase) |> ignore // temporary hack
        while reader.Phase < MvltPhase.End do
          let! blockOffset = reader.LoadNextBlock()
          let phase = reader.DecryptBlock()
          reader.CyclePhase(phase) |> ignore // temporary hack
          if phase = MvltPhase.Data then
            cpx $"\fk{blockOffset:X08}\f0 Data:     \fb{reader.BlockContentSize}\f0 bytes"
            if reader.BlockOriginalSize <> reader.BlockContentSize then
              let percent = 100.0 * float reader.BlockContentSize / float reader.BlockOriginalSize
              cp $" (\fg{reader.BlockOriginalSize}\f0, \fy{percent:F2}%%\f0)"
            else
              cp " (\fknot compressed\f0)"
            contentSize <- contentSize + int64(reader.BlockContentSize)
            decompressedSize <- decompressedSize + int64(reader.BlockOriginalSize)
            datablockCount <- datablockCount + 1
          elif phase = MvltPhase.End then
            cp $"\fk{blockOffset:X08}\f0 Footer:   \fb{reader.BlockContentSize}\f0 bytes"
            let terminatorJson = reader.GetMetadataText()
            cp $"         \fg{terminatorJson}\f0."
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
        let! blockOffset = reader.LoadNextBlock()
        if reader.BlockType <> MvltFormat.Preamble4CC then
          cp $"\frError\fo: Expected Preamble4CC block, but got \fy{reader.BlockType:X08}\f0."
          failwith "Invalid block type"
        cp $"\fk{blockOffset:X08}\f0 Preamble: \fb{reader.BlockContentSize}\f0 bytes"
        while reader.IgnoreBlock() do
          let! blockOffset = reader.LoadNextBlock()
          let phase = reader.ValidatePhase()
          if phase = MvltPhase.Data then
            cpx $"\fk{blockOffset:X08}\f0 Data:     \fb{reader.BlockContentSize}\f0 bytes"
            if reader.BlockOriginalSize <> reader.BlockContentSize then
              let percent = 100.0 * float reader.BlockContentSize / float reader.BlockOriginalSize
              cp $" (\fg{reader.BlockOriginalSize}\f0, \fy{percent:F2}%%\f0)"
            else
              cp " (\fknot compressed\f0)"
            contentSize <- contentSize + int64(reader.BlockContentSize)
            decompressedSize <- decompressedSize + int64(reader.BlockOriginalSize)
            datablockCount <- datablockCount + 1
          elif phase = MvltPhase.End then
            cp $"\fk{blockOffset:X08}\f0 Footer:   \fb{reader.BlockContentSize}\f0 bytes"
          else
            cp $"\frError\fo: Unknown block phase \fy{reader.Phase}\f0."
            failwith "Unknown block phase"
        return None
      }
  let metadataOption = infoTask |> Async.AwaitTask |> Async.RunSynchronously
  let percent = 100.0 * float contentSize / float decompressedSize
  cpx $"Total:   \fb{contentSize}\f0 mvlt bytes (\fg{decompressedSize}\f0 uncompressed,"
  cp $" \fy{percent:F2}%%\f0) in \fc{datablockCount}\f0 blocks"
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
    cp ""
    Usage.usage "info"
    1
  | Some o ->
    o |> runInfo

