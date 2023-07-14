module AppendApp

open System
open System.IO

open FldVault.Core.Crypto
open FldVault.Core.Utilities
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools

type HashSet<'a> = System.Collections.Generic.HashSet<'a>

type private PathFile = {
  // command line parsing focused representation of a file to append
  Path: string
  File: string
  Name: string
  Compression: ZvltCompression
}

type private AppendOptions = {
  VaultFile: string
  PathDefault: string
  CompressionDefault: ZvltCompression
  Files: PathFile list
}

type private FileTarget = {
  // execution focused representation of a file to append
  Label: string
  Source: string
  Compression: ZvltCompression
}

let private pathFileToFileTarget pf = 
  let shortName = if pf.Name |> String.IsNullOrEmpty then Path.GetFileName(pf.File) else Path.GetFileName(pf.Name)
  let prefix = if pf.Path |> String.IsNullOrEmpty then "" else $"{pf.Path}/"
  {
    Label = prefix + shortName
    Source = Path.GetFullPath(pf.File)
    Compression = pf.Compression
  }

let runAppend args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      None
    | [] ->
      if o.VaultFile |> String.IsNullOrEmpty then
        failwith "No target vault specified"
      if o.Files |> List.isEmpty then
        failwith "No files to add specified"
      Some({o with Files = o.Files |> List.rev})
    | "-vf" :: vault :: rest ->
      rest |> parseMore {o with VaultFile = vault}
    | file :: rest when file.EndsWith(".zvlt") ->
      rest |> parseMore {o with VaultFile = file}
    | "-p" :: path :: rest ->
      let path = if path = "." then "" else path
      let path = path.Replace('\\', '/')
      let badIdx = path.IndexOfAny([| ':'; ';'; '|'; '<'; '>'; '*'; '?' |])
      if badIdx >= 0 then
        failwith $"Invalid character in path '{path}': '{path[badIdx]}'"
      let segments = 
        path.Split('/') // silently remove empty segments (including leading or trailing '/')
        |> Array.where (fun segment -> segment |> String.IsNullOrEmpty |> not)
      if segments |> Seq.contains ".." then
        failwith $"'{path}': virtual paths must not contain '..' segments"
      rest |> parseMore {o with PathDefault = String.Join("/", segments)}
    | "-f" :: file :: rest ->
      if file.IndexOfAny([| '*'; '?'|]) >= 0 then
        let file = Path.GetFullPath(file)
        let folder = Path.GetDirectoryName(file)
        let file = Path.GetFileName(file)
        let files = Directory.GetFiles(folder, file)
        if files.Length = 0 then
          cp $"Pattern \fy{folder}\f0{Path.DirectorySeparatorChar}\fo{file}\f0 did not match any files"
          rest |> parseMore o
        else
          let files =
            files
            |> Array.map (fun fnm -> {Path = o.PathDefault; File = fnm; Compression = o.CompressionDefault; Name = null})
          let fileList = o.Files |> Array.foldBack (fun pf l -> pf :: l) files
          rest |> parseMore {o with Files = fileList}
      else
        let pf = {Path = o.PathDefault; File = file; Compression = o.CompressionDefault; Name = null}
        rest |> parseMore {o with Files = pf :: o.Files}
    | "-z" :: compressionText :: rest ->
      let compression =
        match compressionText with
        | "auto" -> ZvltCompression.Auto
        | "off" -> ZvltCompression.Off
        | "on" -> ZvltCompression.On
        | x ->
          failwith $"Unrecognized compression specifier '{x}'"
      rest |> parseMore {o with CompressionDefault = compression}
    | "-n" :: name :: rest ->
      match o.Files with
      | [] ->
        failwith "'-n' expects a '-f'  before it"
      | pf :: tail ->
        if name.IndexOfAny([| '/'; '\\' |]) >= 0 then 
          failwith "'-n' expecting a short file name with no path"
        let pf = {pf with Name = name}
        rest |> parseMore {o with Files = pf :: tail}
    | x :: _ ->
      failwith $"Unrecognized argument '{x}'"
  let oo = args |> parseMore {
    VaultFile = null
    PathDefault = ""
    Files = []
    CompressionDefault = ZvltCompression.Auto
  }
  match oo with
  | Some(o) ->
    let vaultFile = VaultFile.Open(o.VaultFile)
    use keyChain = new KeyChain()
    let seedService = KeyUtilities.setupKeySeedService()
    KeyUtilities.hatchKeyIntoChain seedService vaultFile keyChain
    use cryptor = vaultFile.CreateCryptor(keyChain)
    let targets = o.Files |> List.map pathFileToFileTarget
    let alreadyAdded =
      let existingMetadata =
        use reader = new VaultFileReader(vaultFile, cryptor)
        vaultFile.FileElements()
        |> Seq.map (fun ibe -> new FileElement(ibe))
        |> Seq.map (fun fe -> fe.GetMetadata(reader))
        |> Seq.toArray
      let existingNames =
        existingMetadata
        |> Seq.map (fun meta -> meta.Name)
        |> Seq.where (fun name -> name |> String.IsNullOrEmpty |> not)
        |> Seq.toArray
      let existingNameSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
      existingNames |> Seq.iter (fun name -> existingNameSet.Add(name) |> ignore)
      targets
      |> Seq.where (fun tgt -> existingNameSet.Contains(tgt.Label))
      |> Seq.toArray
    if alreadyAdded.Length > 0 then
      cp "\frError\f0 These files already exist in the vault:"
      for aa in alreadyAdded do
        cp $"  \fo{aa.Label}\f0 (\fy{aa.Source}\f0)"
      failwith $"Adding this file would create an ambiguously named entry in the vault"
    use writer = new VaultFileWriter(vaultFile, cryptor)
    for target in targets do
      cp $"Adding entry '\fg{target.Label}\f0' (\fk{target.Source}\f0)."
      // Use the long form append here to have more control over the metadata (enable paths)
      let fi = new FileInfo(target.Source)
      let meta = new FileMetadata(target.Label, fi.LastWriteTimeUtc |> EpochTicks.FromUtc, fi.Length)
      let compression = target.Compression
      let _ =
        use source = File.OpenRead(target.Source)
        writer.AppendFile(source, meta, compression)
      ()
    0
  | None ->
    Usage.usage "append"
    0




