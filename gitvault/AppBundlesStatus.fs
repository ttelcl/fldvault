module AppBundlesStatus

open System
open System.IO

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

type private RepoSource =
  | GitRepo of WitnessFolder: string
  | AnchorRepo of AnchorName: string * RepoName: string
  | AnchorAll of AnchorName: string

type private Options = {
  Source: RepoSource
}

let private processRepoAnchor repo anchorSettings (repoDb: LogicalRepository) =
  let zkey = repoDb.VaultFolder.GetVaultKey()
  cp $"\fg{repoDb.AnchorName}\fy::\fm{repoDb.RepoName}\f0 (\fc{repoDb.VaultFolder.VaultFolder}\f0 / \fy{zkey.KeyGuid}\f0):"
  let keyError = repoDb.VaultFolder.CanGetKey()
  if keyError |> String.IsNullOrEmpty |> not then
    cp $"    \foNo vault key available:\fy {keyError}\f0."
  else
    let repoRecord =
      match repo, anchorSettings with
      | Some(repo), Some(anchorSettings) -> repoDb.RegisterSourceRepository(repo, anchorSettings) |> Some
      | _ -> None
    repoDb.RegisterVaults()
    repoDb.RegisterBundles()
    let byhost =
      repoDb.RecordCache.Records.Values
      |> Seq.sortBy (fun br -> br.HostName)
      |> Seq.toArray
    if byhost.Length = 0 then
      let filler = "-------"
      cp $"\fr{repoDb.AnchorName,14}\f0.\fy{filler,-15}\f0 \fono vaults or bundles found\f0."
    for record in byhost do
      cpx $"\fg{repoDb.AnchorName,14}\f0.\fc{record.HostName,-15}\f0 "
      let kind =
        if repoRecord.IsSome && record.Key.Equals(repoRecord.Value.Key) then 
          "\fgthis\f0    "
        elif record.HasSourceFile then
          "\fcoutgoing\f0"
        else
          "\fyincoming\f0"
      cpx $"{kind} "
      let tSource = if record.HasSourceFile then record.BundleTime else record.VaultTime
      let tDest = if record.HasSourceFile then record.VaultTime else record.BundleTime
      let needsUpdate = tSource.HasValue && (not(tDest.HasValue) || tSource.Value >= tDest.Value)
      let vaultTime =
        if record.VaultTime.HasValue then
          let color =
            if needsUpdate |> not then
              "\fk"
            elif record.HasSourceFile then
              "\fo"
            else
              "\fb"
          color + record.VaultTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "\f0"
        else
          "\fo-missing-\f0"
      let bundleTime =
        if record.BundleTime.HasValue then
          let color =
            if needsUpdate |> not then
              "\fk"
            elif record.HasSourceFile then
              "\fb"
            else
              "\fo"
          color + record.BundleTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "\f0"
        else
          "\fo-missing-\f0"
      let arrow =
        if needsUpdate |> not then
          "\f0   \f0"
        elif record.HasSourceFile then
          "\fc<--\f0"
        else
          "\fy-->\f0"
      cpx $"Vault: {vaultTime,-23} {arrow} Bundle: {bundleTime,-23}"
      cp ""

let private runBundleStatusGit witnessFolder =
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
      cp $"Status of bundles related to GIT repository \fo{repo.Folder}\f0:"
      let settings = CentralSettings.Load()
      for anchorSettings in repoSettings.ByAnchor.Values do
        let repoDb = new LogicalRepository(settings, anchorSettings.VaultAnchor, anchorSettings.RepoName)
        let anchorSettings = anchorSettings |> Some
        let repo = repo |> Some
        repoDb |> processRepoAnchor repo anchorSettings
      0

let private runBundleStatusAnchorRepo anchorName repoName =
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
    repoDb |> processRepoAnchor None None
    0

let private runBundleStatusAnchorAll anchorName =
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
        //cp $"Repo \fm{vaultRepoFolder.RepoName}\f0:"
        let repoDb = new LogicalRepository(settings, anchorName, vaultRepoFolder.RepoName)
        repoDb |> processRepoAnchor None None
      0

let private runBundlesStatus o =
  match o.Source with
  | GitRepo(witnessFolder) ->
    witnessFolder |> runBundleStatusGit
  | AnchorRepo(anchorName, repoName) ->
    runBundleStatusAnchorRepo anchorName repoName
  | AnchorAll(anchorName) ->
    anchorName |> runBundleStatusAnchorAll

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
    Usage.usage "bundles-status"
    1
  | Some o ->
    o |> runBundlesStatus
