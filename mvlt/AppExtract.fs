module AppExtract

open System
open System.IO
open System.Threading

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open FldVault.Core.Crypto
open FldVault.Core.Mvlt
open FldVault.KeyServer

open FileUtilities

open ColorPrint
open CommonTools

type private Options = {
  InputVault: string
  OutputFolder: string
  Overwrite: bool
  ExtractMeta: bool option // None decides automatically
}

let private runExtract o =
  let kss = new KeyServerService()
  if kss.ServerAvailable |> not then
    cp "\frError\fo: Zvault Key Server is not running\f0."
    1
  else
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
    let keyId = header.KeyInfoFile.KeyId
    let outputName = MvltReader.DeriveOriginalFileName(vault, o.OutputFolder, keyId)
    let keyAvailable =
      let presence = kss.LookupKeySync(keyId, keyChain)
      //cp $"\fcDBG\f0 key presence: \fy{presence}\f0."
      match presence with
      | KeyPresence.Unavailable ->
        cp $"\frError\fo: Key \fy{keyId}\fo not yet found on server\f0. Registering the vault file."
        kss.RegisterFileSync(o.InputVault, keyChain) |> ignore
        false
      | KeyPresence.Cloaked ->
        cp $"\frError\fo: Key \fy{keyId}\fo is available, but hidden\f0. Please un-hide it."
        false
      | KeyPresence.Present ->
        cp $"Key \fg{keyId}\f0 succesfully retrieved."
        true
      | _ ->
        cp $"\frError\fo: Key \fy{keyId}\fo has an unrecognized status\f0."
        false
    if keyAvailable then
      let canWrite =
        if outputName |> File.Exists then
          if o.Overwrite then
            cp $"\foOverwriting existing output file \fy{outputName}\f0."
            true
          else
            cp "\frThe output file already exists\f0. Use \fg-F\f0 to overwrite it."
            false
        else
          cp $"Writing \fg{outputName}\f0."
          if o.OutputFolder |> Directory.Exists |> not then
            Directory.CreateDirectory(o.OutputFolder) |> ignore
            cp $"\foCreated\f0 output folder \fc{o.OutputFolder}\f0."            
          true
      if canWrite then
        use reader = header.CreateReader(
          keyChain,
          inputStream,
          false)
        use cts = new CancellationTokenSource()
        let ct = cts.Token
        let extractTask =
          task {
            use outStream = outputName |> startFileBinary
            let! _ = reader.LoadNextBlock()
            if reader.BlockType <> MvltFormat.Preamble4CC then
              cp $"\frError\fo: Expected Preamble4CC block, but got \fy{reader.BlockType:X08}\f0."
              failwith "Invalid block type"
            let _ = reader.DecryptBlock() |> reader.DecompressBlock
            let preambleText = reader.GetMetadataText()
            let metadata = JsonConvert.DeserializeObject<JObject>(preambleText)
            while reader.Phase < MvltPhase.End do
              let! _ = reader.LoadNextBlock()
              let phase = reader.DecryptBlock()
              let memory = phase |> reader.DecompressBlock
              if phase = MvltPhase.Data then
                do! outStream.WriteAsync(memory, ct)
              elif phase = MvltPhase.End then
                let terminatorJson = reader.GetMetadataText()
                let terminator = JsonConvert.DeserializeObject<JObject>(terminatorJson)
                for prop in terminator.Properties() do
                  metadata[prop.Name] <- prop.Value
              else
                cp $"\frError\fo: Unknown block phase \fy{reader.Phase}\f0."
                failwith "Unknown block phase"
            return metadata
          }
        let metadata = extractTask |> Async.AwaitTask |> Async.RunSynchronously
        outputName |> finishFile
        let nullableTime = metadata |> MvltReader.GetModifiedTimeFromMetadata
        if nullableTime.HasValue then
          let modifiedTime = nullableTime.Value
          cp $"Restoring last modified time (\fg{modifiedTime}\f0)"
          File.SetLastWriteTimeUtc(outputName, modifiedTime.UtcDateTime)
        else
          cp $"\foNo modification time in metadata\f0 Not restoring timestamp."
        let hasCustomMetadata =
          metadata.Properties()
          |> Seq.exists (fun prop -> prop.Name <> "modified" &&
                                     prop.Name <> "length" &&
                                     prop.Name <> "name")
        cp $"Extraction complete."
        if hasCustomMetadata then
          cp $"Metadata (\foincludes custom fields\f0):"
        else
          cp $"Metadata (no custom fields)\f0:"
        cp $"\fg{metadata}\f0."
        let extractMetadata = 
          match o.ExtractMeta with
          | Some true -> true
          | Some false -> false
          | None -> hasCustomMetadata
        if extractMetadata then
          let metaName = outputName + ".meta.json"
          let metaShortName = Path.GetFileName(metaName)
          cp $"Writing metadata to \fc{metaShortName}\f0."
          do
            use metaWriter = metaName |> startFile
            let json = metadata.ToString(Formatting.Indented)
            metaWriter.WriteLine(json)
          metaName |> finishFile
          if nullableTime.HasValue then
            let modifiedTime = nullableTime.Value
            File.SetLastWriteTimeUtc(metaName, modifiedTime.UtcDateTime)
        else
          cp $"Not writing metadata to file\f0."
        0
      else
        1
    else
      1

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
    | "-of" :: folder :: rest ->
      rest |> parseMore { o with OutputFolder = folder }
    | "-F" :: rest ->
      rest |> parseMore { o with Overwrite = true }
    | "-nometa" :: rest ->
      rest |> parseMore { o with ExtractMeta = Some false }
    | "-meta" :: rest ->
      rest |> parseMore { o with ExtractMeta = Some true }
    | "-meta-auto" :: rest ->
      rest |> parseMore { o with ExtractMeta = None }
    | [] ->
      if o.InputVault |> String.IsNullOrEmpty then
        cp "\foNo Input vault specified\f0."
        None
      elif o.InputVault |> File.Exists |> not then
        cp $"\foFile not found: {o.InputVault}\f0."
        None
      elif o.OutputFolder |> String.IsNullOrEmpty then
        let inputFolder =
          o.InputVault |> Path.GetFullPath |> Path.GetDirectoryName
        if FileIdentifier.AreSame(inputFolder, Environment.CurrentDirectory) then
          cp "\frError\fo: Output folder not specified\fy (\fg-of\fy is required if input folder is current directory)\f0."
          None
        else
          // If input folder is not the current directory, then default
          // to current directory as output folder (else error)
          Some { o with OutputFolder = Environment.CurrentDirectory }
      else
        Some o
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  let oo = args |> parseMore {
    InputVault = null
    OutputFolder = null
    Overwrite = false
    ExtractMeta = None
  }
  match oo with
  | None ->
    cp ""
    Usage.usage "extract"
    1
  | Some o ->
    o |> runExtract
