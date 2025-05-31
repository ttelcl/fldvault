module AppBundlesFetch

open System
open System.IO
open System.Threading

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open FileUtilities

open FldVault.KeyServer
open FldVault.Core.Crypto
open FldVault.Core.Mvlt
open FldVault.Core.Vaults

open GitVaultLib.Configuration
open GitVaultLib.GitThings
open GitVaultLib.VaultThings

open ColorPrint
open CommonTools
open KeyTools

type private RepoSource =
  | GitRepo of WitnessFolder: string
  | AnchorRepo of AnchorName: string * RepoName: string
  | AnchorAll of AnchorName: string

type private Options = {
  Source: RepoSource
}

let decryptVault (keyChain: KeyChain) keyId vaultFile bundleFile =
  if keyId |> keyChain.ContainsKey |> not then
    failwith "Expecting a key chain preloaded with the vault key"
  use inputStream = File.OpenRead(vaultFile)
  let headerTask =
    task {
      let! header = MvltFileHeader.ReadAsync(inputStream)
      return header
    }
  let header =
    headerTask
    |> Async.AwaitTask
    |> Async.RunSynchronously
  if header.KeyInfoFile.KeyId <> keyId then
    cp $"\frFatal:\fo vault '\fy{vaultFile}\fo' is encryped with the wrong key.\f0 It does not belong here."
    failwith "Vault file is encrypted with the wrong key."
  use reader = header.CreateReader(
    keyChain,
    inputStream,
    false)
  use cts = new CancellationTokenSource()
  let ct = cts.Token
  let extractTask =
    task {
      cpx "               Bundle:   "
      use outStream = bundleFile |> startFileBinary
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
  bundleFile |> finishFile
  // In this case we do not want to restore the original bundle time, we can skip that
  let metaName = bundleFile + ".meta.json"
  do
    cpx "               Metadata: "
    use metaWriter = metaName |> startFile
    let json = metadata.ToString(Formatting.Indented)
    metaWriter.WriteLine(json)
  metaName |> finishFile
  ()

let private ingestRepoAnchor kinf (repoDb: LogicalRepository) =
  let zkey = repoDb.VaultFolder.GetVaultKey()
  cp $"\fg{repoDb.AnchorName}\fy::\fm{repoDb.RepoName}\f0 (\fy{zkey.KeyGuid}\f0):"
  let keyError = repoDb.VaultFolder.CanGetKey()
  if keyError |> String.IsNullOrEmpty |> not then
    cp $"    \foNo vault key available:\fy {keyError}\f0."
  else
    let keyLoadResult = repoDb.VaultFolder |> lookupVaultFolderKey kinf // this already prints a message
    match keyLoadResult with
    | Failure(failure) ->
      // A message was printed already
      ()
    | Success {KeyId = keyId; Chain = keyChain} ->
      repoDb.RegisterVaults()
      repoDb.RegisterBundles()
      let byhost =
        repoDb.RecordCache.Records.Values
        |> Seq.sortBy (fun br -> br.HostName)
        |> Seq.toArray
      if byhost.Length = 0 then
        let filler = "-------"
        cp $"\fr{repoDb.AnchorName,14}\f0.\fy{filler,-15}\f0 \fono vaults or bundles found\f0."
      // no need for an explicit 'else': the collection is empty, so the 'for' doesn't do anything.
      let incoming =
        byhost
        |> Array.filter (fun br -> br.HasSourceFile |> not)
      if incoming.Length = 0 && byhost.Length > 0 then
        let filler = "-------"
        cp $"\fy{repoDb.AnchorName,14}\f0.\fy{filler,-15}\f0 All bundles are outgoing, nothing to ingest\f0."
      for record in incoming do
        cpx $"\fg{repoDb.AnchorName,14}\f0.\fc{record.HostName,-15}\f0 "
        let tSource = record.VaultTime
        let tDest = record.BundleTime
        let needsUpdate = tSource.HasValue && (not(tDest.HasValue) || tSource.Value >= tDest.Value)
        if needsUpdate |> not then
          cp "\fk(Already up to date)\f0."
        else
          let vaultTime =
            if record.VaultTime.HasValue then
              "\fb" + record.VaultTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "\f0"
            else
              "\fo-missing-\f0"
          let bundleTime =
            if record.BundleTime.HasValue then
              "\fo" + record.BundleTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "\f0"
            else
              "\fo-missing-\f0"
          cpx $"Vault: {vaultTime,-23} \fy-->\f0 Bundle: {bundleTime,-23}"
          cp ""
          let bundleName = record.BundleFileName
          let vaultName = record.GetVaultFileNameOrFail()
          decryptVault kinf.Chain keyId vaultName bundleName

let private runIngestGit kinf witnessFolder =
  let repo = witnessFolder |> GitRepoFolder.LocateRepoRootFrom
  if repo = null then
    cp "\foNo git repository found here\f0."
    1
  else
    let repoSettings = repo.TryLoadGitVaultSettings()
    if repoSettings = null then
      cp $"\foNo gitvault settings found in repository \fy{repo.Folder}\f0."
      1
    else
      cp $"Processing incoming bundles related to GIT repository \fo{repo.Folder}\f0:"
      let settings = CentralSettings.Load()
      for anchorSettings in repoSettings.ByAnchor.Values do
        let repoDb = new LogicalRepository(settings, anchorSettings.VaultAnchor, anchorSettings.RepoName)
        let anchorSettings = anchorSettings |> Some
        let repo = repo |> Some
        repoDb |> ingestRepoAnchor kinf
      0

let private runIngestAnchorRepo kinf anchorName repoName =
  let settings = CentralSettings.Load()
  if anchorName |> settings.Anchors.ContainsKey |> not then
    cp $"\foanchor name '{anchorName}\fo' is unknown\f0."
    let anchorNames = settings.Anchors.Keys |> Seq.sort |> Seq.toArray
    cp $"The \fb{anchorNames.Length}\f0 known anchor names are:"
    for knownAnchorName in anchorNames do
      cp $"  \fy{knownAnchorName}\f0 "
    1
  else
    let repoDb = new LogicalRepository(settings, anchorName, repoName)
    repoDb |> ingestRepoAnchor kinf
    0

let private runIngestAnchorAll kinf anchorName =
  let settings = CentralSettings.Load()
  if anchorName |> settings.Anchors.ContainsKey |> not then
    cp $"\foanchor name '{anchorName}\fo' is unknown\f0."
    let anchorNames = settings.Anchors.Keys |> Seq.sort |> Seq.toArray
    cp $"The \fb{anchorNames.Length}\f0 known anchor names are:"
    for knownAnchorName in anchorNames do
      cp $"  \fy{knownAnchorName}\f0 "
    1
  else
    let vaultFolders =
      settings.EnumerateRepoVaultFolders(anchorName)
      |> Seq.toArray
    if vaultFolders.Length = 0 then
      cp $"\foThe vault anchor folder \fy{anchorName}\fo is empty\f0."
      1
    else
      for vaultRepoFolder in vaultFolders do
        let repoDb = new LogicalRepository(settings, anchorName, vaultRepoFolder.RepoName)
        repoDb |> ingestRepoAnchor kinf
      0

let private runIngest o =
  use keyChain = new KeyChain()
  let kinf = keyChain |> createInfrastructure
  match o.Source with
  | GitRepo(witnessFolder) ->
    witnessFolder |> runIngestGit kinf
  | AnchorRepo(anchorName, repoName) ->
    runIngestAnchorRepo kinf anchorName repoName
  | AnchorAll(anchorName) ->
    anchorName |> runIngestAnchorAll kinf

let run args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      parseMore o rest
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-f" :: witnessFolder :: rest ->
      let witnessFolder = (witnessFolder |> Path.GetFullPath).TrimEnd('/', '\\')
      if Directory.Exists(witnessFolder) |> not then
        cp $"\fFolder \fy{witnessFolder}\f0 does not exist."
        None
      else
        rest |> parseMore { o with Source = GitRepo(witnessFolder) }
    | "-a" :: anchorAndRepo :: rest ->
      let parts = anchorAndRepo.Split("::")
      if parts.Length = 1 then
        rest |> parseMore { o with Source = AnchorAll(parts[0]) }
      elif parts.Length = 2 then
        rest |> parseMore { o with Source = AnchorRepo(parts[0], parts[1]) }
      else
        cp $"Unrecognized format in \fg-a\f0 argument '\fc{anchorAndRepo}\f0'"
        None
    | [] ->
      Some o
    | x :: _ ->
      cp $"\frUnknown option: \fy{x}\f0."
      None
  let oo = args |> parseMore { 
    Source = GitRepo(Environment.CurrentDirectory)
  }
  match oo with
  | None ->
    Usage.usage "bundles-fetch"
    1
  | Some o ->
    o |> runIngest
