module AppBundlesConnect

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

type private ConnectTarget =
  | AnchorHost of AnchorName: string * HostName: string
  | AnchorAll of AnchorName: string
  | All

type private Options = {
  Target: ConnectTarget option
  Fetch: bool
}

type private AnchorRepoInfo = {
  Logical: LogicalRepository
  Settings: AnchorRepoSettings
  Repo: GitRepoFolder
}

let private matchAnchor o anchor =
  match o.Target with
  | Some(AnchorHost(anchorName, _)) ->
    anchor = anchorName
  | Some(AnchorAll(anchorName)) ->
    anchor = anchorName
  | Some(All) ->
    true
  | None ->
    false

let private matchAnchorHost o (br: BundleRecord) =
  match o.Target with
  | Some(AnchorHost(anchorName, hostName)) ->
    br.AnchorName = anchorName && br.HostName.Equals(hostName, StringComparison.OrdinalIgnoreCase)
  | Some(AnchorAll(anchorName)) ->
    br.AnchorName = anchorName
  | Some(All) ->
    true
  | None ->
    false

let private tryAnchorRepoInfo repo (cs: CentralSettings) (ars: AnchorRepoSettings) =
  let anchorName = ars.VaultAnchor
  if anchorName |> cs.Anchors.ContainsKey |> not then
    cp $"\foIgnoring unknown anchor \f0'\fy{anchorName}\f0'"
    None
  else
    let logical = new LogicalRepository(cs, anchorName, ars.RepoName)
    logical.RegisterVaults()
    logical.RegisterBundles()
    let error = logical.RecordCache.CheckSourceRepository(repo, ars)
    if error |> String.IsNullOrEmpty then
      Some {
        Logical = logical
        Settings = ars
        Repo = repo
      }
    else
      cp $"\foIgnoring broken registration: \fy{error}\f0."
      None

let private createRemote (repo:GitRepoFolder) remoteName (bundle: BundleRecord) =
  let result = GitRunner.AddRemote(repo.Folder, remoteName, bundle.BundleFileName)
  if result.StatusCode <> 0 then
    let errorText = String.Join("\n", result.ErrorLines)
    cp $"  \frError creating remote\f0: \n[\fo{errorText}\f0]"

let private fetchRemote (repo:GitRepoFolder) remoteName =
  let result = GitRunner.FetchRemote(repo.Folder, remoteName)
  if result.StatusCode <> 0 then
    let errorText = String.Join("\n", result.ErrorLines)
    cp $"  \frError fetching remote\f0: \n[\fo{errorText}\f0]"
  else
    cp $"    Fetching \fg{remoteName}\f0."
    for line in result.OutputLines do
      cp $"    {line}"

let private runConnect o =
  let centralSettings = CentralSettings.Load()
  let repo, repoSettings =
    let repo = GitRepoFolder.LocateRepoRootFrom(Environment.CurrentDirectory)
    if repo = null then
      cp "\foExpecting this to be called from within a git repository\f0."
      null, null
    else
      let settings = repo |> RepoSettings.TryLoad
      if settings = null then
        cp "\foThis git repository has not been initialized for use with gitvault yet\f0."
        cp "Call \fogitvault repo init\f0 to initialize it first."
        null, null
      else
        repo, settings
  if repo = null || repoSettings = null then
    1
  else
    let anchorRepos =
      repoSettings.ByAnchor.Values
      |> Seq.filter (fun ars -> ars.VaultAnchor |> matchAnchor o)
      |> Seq.choose (tryAnchorRepoInfo repo centralSettings)
      |> Seq.toArray
    if anchorRepos.Length = 0 then
      cp "\foNo matching anchors found\f0."
      1
    else
      let remotes, status = repo.Folder |> GitRunner.GetRemotes
      if status.StatusCode <> 0 then
        cp "\f0Error retrieving the existing remotes in this repository\f0 GIT said:"
        let errorText = String.Join('\n', status.ErrorLines)
        cp $"[\fr{errorText}\f0]"
        1
      else
        for anchorRepo in anchorRepos do
          let logicalRepo = anchorRepo.Logical
          let hostName = anchorRepo.Settings.HostName
          cp $"Anchor '\fg{logicalRepo.AnchorName}\f0':"
          let otherBundles =
            hostName
            |> logicalRepo.GetOtherRecords
            |> Seq.filter (fun br -> br |> matchAnchorHost o)
            |> Seq.toArray
          if otherBundles.Length = 0 then
            cp $"  \fcNothing to connect\f0: there are no other matching bundles to connect to"
          else
            for otherBundle in otherBundles do
              let remoteName = otherBundle.RemoteName
              let incoming = otherBundle.HasSourceFile |> not
              let bundleStamp = otherBundle.BundleTime
              if bundleStamp.HasValue |> not then
                if incoming then
                  cp $"  \foError: Bundle missing\f0: [{remoteName}]. \fyRun \fogitvault ingest\f0."
                else
                  cp $"  \foError: Bundle missing\f0: [{remoteName}]. \fyRun '\fogitvault send\fy' from its source repo\f0"
              else
                if incoming then
                  let vaultStamp = otherBundle.VaultTime
                  if vaultStamp.HasValue && vaultStamp.Value > bundleStamp.Value then
                    cp $"  \foWarning: Bundle out of date\f0: [{remoteName}]. \fyRun \fogitvault ingest\f0."
                let existingRemote = remotes[remoteName]
                if existingRemote = null then
                  cp $"  \fg{remoteName,-20}\f0 \fycreating remote\f0."
                  createRemote repo remoteName otherBundle
                  if o.Fetch then
                    fetchRemote repo remoteName
                else
                  let fetchTarget = existingRemote.FetchTarget
                  if fetchTarget = null then
                    cp $"  \fo{remoteName,-20}\f0 \fyalready exists, but misconfigured\f0 (no fetch target)."
                  elif fetchTarget.Target.EndsWith(".bundle") |> not then
                    cp $"  \fo{remoteName,-20}\f0 \fyalready exists as a different target\f0 (not a bundle)."
                  elif FileIdentifier.AreSame(fetchTarget.Target, otherBundle.BundleFileName) then
                    cp $"  \fg{remoteName,-20}\f0 (already exists)."
                    if o.Fetch then
                      fetchRemote repo remoteName
                  else
                    cp $"  \fo{remoteName,-20}\f0 \fyalready exists as a different target\f0 (different bundle)."
        cp "\fmNYI\f0."
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
    | "-fetch" :: rest ->
      rest |> parseMore {o with Fetch = true}
    | "-nofetch" :: rest ->
      rest |> parseMore {o with Fetch = false}
    | "-all" :: rest ->
      rest |> parseMore {o with Target = ConnectTarget.All |> Some}
    | "-a" :: spec :: rest ->
      let parts = spec.Split('.')
      if parts.Length = 1 then
        let anchor = parts[0]
        if anchor |> CentralSettings.IsValidAnchor |> not then
          cp $"\fo'\fc{anchor}\fo' is not a valid anchor name\f0."
          None
        else
          rest |> parseMore {o with Target = ConnectTarget.AnchorAll(anchor) |> Some}
      elif parts.Length = 2 then
        let anchor = parts[0]
        let host = parts[1]
        if anchor |> CentralSettings.IsValidAnchor |> not then
          cp $"\fo'\fc{anchor}\fo' is not a valid anchor name\f0."
          None
        elif host |> CentralSettings.IsValidHost |> not then
          cp $"\fo'\fc{host}\fo' is not a valid 'host name'\f0."
          None
        else
          rest |> parseMore {o with Target = ConnectTarget.AnchorHost(anchor, host) |> Some}
      elif parts.Length = 0 then
        cp $"\foEmpty argument to \fg-a\f0."
        None
      else
        cp $"\foToo many '.' characters in \fg-a \fc{spec}\f0 (expecting one or none)"
        None
    | [] ->
      if o.Target.IsNone then
        cp "\foNo target specified\f0."
        None
      else
        o |> Some
    | x :: _ ->
      cp $"\frUnknown option: \fy{x}\f0."
      None
  let oo = args |> parseMore {
    Target = None
    Fetch = false
  }
  match oo with
  | None ->
    Usage.usage "bundles-connect"
    1
  | Some(o) ->
    o |> runConnect
