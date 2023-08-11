module ExtractApp

open System
open System.IO

open Newtonsoft.Json

open FldVault.Core.Crypto
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools

type private FileKey =
  | FileName of string
  | FileId of string

type private ExtractFile = {
  Key: FileKey
  NameOverride: string
}

type private ExistsHandling =
  | Fail
  | Overwrite
  | Skip

type private MetaHandling =
  | Auto
  | No  // "No" to avoid conflict with Option.None
  | All
  | Only
  | View

type private ExtractOptions = {
  VaultName: string
  OutDir: string
  AllowSame: bool
  ExtractAll: bool
  Backdate: bool
  ExistPolicy: ExistsHandling
  Files: ExtractFile list
  MetaPolicy: MetaHandling
}

type private VaultContentFile = {
  Element: FileElement
  Meta: FileMetadata
  Name: string
  Id: string
}

type private ExtractionTask = {
  mutable Selector: ExtractFile option // can be none when '-all' was given
  Target: VaultContentFile
}

type private NamedExtractionTask = {
  OutputName: string
  Reason: ExtractFile option
  TargetVcf: VaultContentFile
}

let runExtract args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      None
    | "-vf" :: vault :: rest ->
      rest |> parseMore {o with VaultName = Path.GetFullPath(vault)}
    | vault :: rest when vault.EndsWith(".zvlt") ->
      rest |> parseMore {o with VaultName = Path.GetFullPath(vault)}
    | "-same" :: rest ->
      rest |> parseMore {o with AllowSame = true}
    | "-all" :: rest ->
      rest |> parseMore {o with ExtractAll = true}
    | "-od" :: dir :: rest ->
      rest |> parseMore {o with OutDir = Path.GetFullPath(dir)}
    | "-f" :: fnm :: rest ->
      let ef = {
        Key = FileKey.FileName(fnm)
        NameOverride = null
      }
      rest |> parseMore {o with Files = ef :: o.Files}
    | "-id" :: fid :: rest ->
      let ef = {
        Key = FileKey.FileId(fid)
        NameOverride = null
      }
      rest |> parseMore {o with Files = ef :: o.Files}
    | "-n" :: name :: rest ->
      match o.Files with
      | ef :: tail ->
        let ef = {ef with NameOverride = name}
        rest |> parseMore {o with Files = ef :: tail}
      | [] ->
        failwith "'-n' requires at least one '-f' or '-id' option before"
    | "-notime" :: rest ->
      rest |> parseMore {o with Backdate = false}
    | "-x-overwrite" :: rest ->
      rest |> parseMore {o with ExistPolicy = ExistsHandling.Overwrite}
    | "-x-skip" :: rest ->
      rest |> parseMore {o with ExistPolicy = ExistsHandling.Skip}
    | "-meta" :: metaOption :: rest ->
      let metaValue =
        match metaOption with
        | "auto" | "default" -> MetaHandling.Auto
        | "no" | "none" -> MetaHandling.No
        | "all" | "yes" -> MetaHandling.All
        | "only" -> MetaHandling.Only
        | "view" | "show" -> MetaHandling.View
        | x ->
          failwith $"Unrecognized metadata handling option: '{x}'"
      rest |> parseMore {o with MetaPolicy = metaValue}
    | [] ->
      if o.VaultName |> String.IsNullOrEmpty then
        failwith "No vault name specified"
      if o.VaultName |> File.Exists |> not then
        failwith $"No such file: {o.VaultName}"
      Some({o with Files = o.Files |> List.rev})
    | x :: _ ->
      failwith $"Unrecognized argument '{x}'"
  let oo = args |> parseMore {
    VaultName = null
    OutDir = Environment.CurrentDirectory
    AllowSame = false
    ExtractAll = false
    Backdate = true
    ExistPolicy = ExistsHandling.Fail
    Files = []
    MetaPolicy = MetaHandling.Auto
  }
  match oo with
  | Some(o) ->
    let vaultFile = VaultFile.Open(o.VaultName)
    let vaultFolder = Path.GetDirectoryName(o.VaultName)
    if o.OutDir |> Directory.Exists |> not then
      cp $"Creating output directory \fg{o.OutDir}\f0."
      o.OutDir |> Directory.CreateDirectory |> ignore
    if o.AllowSame |> not then
      let probeName = Guid.NewGuid().ToString() + ".probe"
      let vaultProbe = Path.Combine(vaultFolder, probeName)
      let outProbe = Path.Combine(o.OutDir, probeName)
      File.Create(outProbe).Dispose()
      if File.Exists(vaultProbe) then
        File.Delete(outProbe)
        cp "\foYou are trying to extract content to the vault file's folder. For security\f0"
        cp "\foreasons this is \frdenied\fo by default. \fyExtract to a different folder or pass the\f0"
        cp "\fg-same\fy option to skip this check\f0."
        failwith $"Folder distinctness check failed"
      else
        File.Delete(outProbe)
    use keyChain = new KeyChain()
    let seedService = KeyUtilities.setupKeySeedService()
    KeyUtilities.hatchKeyIntoChain seedService vaultFile keyChain
    use cryptor = vaultFile.CreateCryptor(keyChain)
    use reader = new VaultFileReader(vaultFile, cryptor)
    let makeVcf ibe =
      let fe = new FileElement(ibe)
      let header = fe.GetHeader(reader)
      let meta = fe.GetMetadata(reader)
      {
        Element = fe
        Meta = meta
        Name = meta.Name
        Id = header.FileId.ToString()
      }
    let vaultContents =
      vaultFile.FileElements()
      |> Seq.map makeVcf
      |> Seq.toArray
    let tasks =
      vaultContents
      |> Array.map (fun vc -> {Selector = None; Target = vc})
    let efMatchesTask extractionTask extractFile =
      let vcf = extractionTask.Target
      match extractFile.Key with
      | FileName(fn) ->
        fn.Equals(vcf.Name, StringComparison.InvariantCultureIgnoreCase)
      | FileId(fid) ->
        vcf.Id.StartsWith(fid, StringComparison.InvariantCultureIgnoreCase)
    for ef in o.Files do
      let efLabel =
        match ef.Key with
        | FileName(fn) -> $"'-f {fn}'"
        | FileId(fid) -> $"'-id {fid}'"
      let matchingTasks = tasks |> Array.where (fun extractionTask -> efMatchesTask extractionTask ef)
      let matchingTask =
        match matchingTasks.Length with
        | 0 -> failwith $"No content files match ${efLabel}."
        | 1 -> matchingTasks[0]
        | _ -> failwith $"Ambiguous key. Multiple content files match ${efLabel}"
      if matchingTask.Selector.IsSome then
        let taskLabel =
          if matchingTask.Target.Name |> String.IsNullOrEmpty then
            $"File ID {matchingTask.Target.Id}"
          else
            $"File name {matchingTask.Target.Name}"
        failwith $"Ambiguous content file. Multiple '-f' / 'id' options match content:  {taskLabel}"
      matchingTask.Selector <- Some(ef)
    let tasks = // implement '-all'
      if o.ExtractAll then
        tasks
      else
        tasks |> Array.where (fun task -> task.Selector.IsSome)
    let resolveName extractionTask =
      let nameOverride =
        match extractionTask.Selector with
        | Some(et) -> et.NameOverride
        | None -> null
      let name =
        if nameOverride |> String.IsNullOrEmpty then
          extractionTask.Target.Name
        else
          nameOverride
      if name |> String.IsNullOrEmpty then
        null
      else
        Path.Combine(o.OutDir, name)
    let namedTasks =
      tasks
      |> Array.map (fun et -> {
        OutputName = et |> resolveName
        Reason = et.Selector
        TargetVcf = et.Target
      })
    let namedTasks =
      namedTasks
      |> Array.where (fun nt ->
        if nt.OutputName |> String.IsNullOrEmpty then
          cp $"\foSkipping\f0 Entry ID \fy{nt.TargetVcf.Id}\f0 has no name assigned"
          false
        else
          true
      )
    
    if namedTasks.Length = 0 then
      failwith "No matching entries to extract"
    
    // Before extracting anything, make sure none of the targets exist if fail-if-exists is set
    if o.ExistPolicy = ExistsHandling.Fail && (o.MetaPolicy <> MetaHandling.Only) && (o.MetaPolicy <> MetaHandling.View) then
      for nt in namedTasks do
        if nt.OutputName |> File.Exists then
          failwith $"Output {nt.OutputName} already exists"
    
    for nt in namedTasks do
      let fe = nt.TargetVcf.Element
      let stampText =
        let stamp = nt.TargetVcf.Meta.UtcStamp
        if stamp.HasValue && o.Backdate then
          let txt = stamp.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz")
          $"(\fc{txt}\f0)"
        else
          ""
      let shouldSaveFile, shouldSaveMeta, shouldShowMeta =
        match o.MetaPolicy with
        | MetaHandling.Auto ->
          true, nt.TargetVcf.Meta.OtherFields.Count>0, false
        | MetaHandling.No ->
          true, false, false
        | MetaHandling.All ->
          true, true, false
        | MetaHandling.Only ->
          false, true, false
        | MetaHandling.View ->
          false, false, true
      if shouldSaveFile then
        if nt.OutputName |> File.Exists then
          match o.ExistPolicy with
          | ExistsHandling.Fail ->
            // should never happen, because this was checked above
            failwith $"Output {nt.OutputName} already exists"
          | ExistsHandling.Skip ->
            cp $"\foSkipping existing \fy{nt.OutputName}\f0 {stampText}"
          | ExistsHandling.Overwrite ->
            cp $"Extracting (\foOverwriting!\f0) \fg{nt.OutputName}\f0 {stampText}"
            fe.SaveContentToFile(reader, o.OutDir, nt.OutputName, o.Backdate, false)
        else
          cp $"Extracting \fg{nt.OutputName}\f0 {stampText}"
          fe.SaveContentToFile(reader, o.OutDir, nt.OutputName, o.Backdate, false)
      if shouldSaveMeta || shouldShowMeta then
        let json = JsonConvert.SerializeObject(nt.TargetVcf.Meta, Formatting.Indented)
        if shouldSaveMeta then
          let fullfile = nt.OutputName + ".meta.json"
          let folder = fullfile |> Path.GetDirectoryName
          let file = fullfile |> Path.GetFileName
          cp $"Saving metadata to \fc{folder}\f0{Path.DirectorySeparatorChar}\fg{file}\f0."
          File.WriteAllText(fullfile, json)
        if shouldShowMeta then
          cp $"\fG// file ID \fy{nt.TargetVcf.Id}\f0:"
          cp $"{json}"
        ()

    0
  | None ->
    Usage.usage "extract"
    0
