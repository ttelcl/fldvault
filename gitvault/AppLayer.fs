module AppLayer

open System
open System.Globalization
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open LibGit2Sharp

open FldVault.KeyServer
open FldVault.Core.Crypto
open FldVault.Core.Mvlt

open GitVaultLib.Bundles
open GitVaultLib.Configuration
open GitVaultLib.Delta
open GitVaultLib.GitThings
open GitVaultLib.Layers

open ColorPrint
open CommonTools

type private TagSource =
  | Explicit of string
  | Automatic
  | FromScaffold

type private Options = {
  LayerTag: TagSource option
  Dependencies: string list
  Force: bool
  Scaffold: string option
}

let private parseOptions args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      parseMore o rest
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-tag" :: tag :: rest ->
      if o.Scaffold |> Option.isSome then
        cp "\fg-tag\fo and \fg-for\fo are mutually exclusive\f0."
        None
      else
        let tag =
          if tag |> GitUtils.isValidTagName then
            tag |> TagSource.Explicit |> Some
          else
            cp $"\fo'\fy{tag}\fo' is not a valid tag name\f0."
            None
        rest |> parseMore {o with LayerTag = tag }
    | "-autotag" :: rest | "-auto" :: rest ->
      if o.Scaffold |> Option.isSome then
        cp "\fg-autotag\fo and \fg-for\fo are mutually exclusive\f0."
        None
      else
        rest |> parseMore {o with LayerTag = TagSource.Automatic |> Some}
    | "-for" :: groupname :: rest ->
      let hasTag =
        match o.LayerTag with
        | None | Some(FromScaffold) -> false
        | _ -> true
      if hasTag then
        cp "\fg-tag\fo/\fg-autotag\fo and \fg-for\fo are mutually exclusive\f0."
        None
      elif groupname |> GitUtils.isValidTagName |> not then // tags and scaffold groups have same rule
        cp $"\fo'\fy{groupname}\fo' is not a valid scaffold group name\f0."
        None
      else
        rest |> parseMore {o with Scaffold = groupname |> Some; LayerTag = TagSource.FromScaffold |> Some}
    | "-on" :: tag :: rest ->
      if tag |> GitUtils.isValidTagName |> not then
        cp $"\fg-on\fo: '\fy{tag}\fo' is not a valid layer tag\f0."
        None
      else
        rest |> parseMore {o with Dependencies = tag :: o.Dependencies}
    | "-F" :: rest ->
      rest |> parseMore {o with Force = true}
    | [] ->
      {o with Dependencies = o.Dependencies |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  args |> parseMore {
    LayerTag = None
    Dependencies = []
    Force = false
    Scaffold = None
  }

let private runLayer o =
      
  let centralSettings = CentralSettings.Load()
  let status, repoRoot, repoSettings =
    let repoRoot = "." |> GitRepoFolder.LocateRepoRootFrom
    if repoRoot = null then
      cp "\frNo git repository found in the current folder or its parents\f0."
      1, null, null
    else
      let repoSettings = repoRoot.TryLoadGitVaultSettings()
      if repoSettings = null then
        cp $"\foNo gitvault settings found in repository \fg{repoRoot.Folder}\f0."
        1, repoRoot, null
      else
        if repoSettings.ByAnchor.Count > 1 then
          cp $"\foMulti-anchor repositories are not yet supported by the \fylayer\fo command\f0."
          1, repoRoot, repoSettings
        elif repoSettings.ByAnchor.Count = 0 then
          cp "\frError: no anchors found for this repository (internal error)\f0."
          1, repoRoot, repoSettings
        else
          0, repoRoot, repoSettings
  if status <> 0 then
    cp ""
    Usage.usage "layer"
    status
  else
    use repo = new Repository(repoRoot.Folder)
    let refsdb = repo |> GitRefsDb.ForRepository
    let allrepotips = refsdb.ReferencedCommits |> Seq.toArray
    let latestTip =
      if allrepotips.Length = 0 then
        None
      else
        allrepotips
        |> Seq.maxBy (fun commit -> commit.Committer.When)
        |> Some
    let latestCommitUtc =
      match latestTip with
      | Some(commit) -> commit.Committer.When.ToUniversalTime()
      | None -> DateTimeOffset.UtcNow
    let tagOption =
      match o.LayerTag, o.Scaffold with
      | Some(Explicit(tag)), None -> tag |> Some
      | Some(FromScaffold), Some(group) -> group |> Some
      | Some(Automatic), None -> latestCommitUtc.ToString("yyyyMMdd-HHmmss") |> Some
      | None, None ->
        cp "\foMissing \fg-tag\fo, \fg-autotag\fo, or \fg-scaffold\f0."
        cp ""
        Usage.usage "layer"
        None
      | _, _ ->
        cp "\frInternal error\f0."
        None
    match tagOption with
    | Some(tag) ->
      let anchorSettings = repoSettings.ByAnchor.Values |> Seq.exactlyOne
      let anchorName = anchorSettings.VaultAnchor
      let hostName = anchorSettings.HostName
      let repoName = anchorSettings.RepoName
      let bundleRecordCache = new BundleRecordCache(centralSettings, null, null, null)
      let kss = new KeyServerService()
      use keychain = new KeyChain()
      cp $"Building layer bundle '\fc{repoName}\f0.\fy{hostName}\f0.\fg{tag}\f0.lbundle' in anchor '\fb{anchorName}\f0'."
      let prefix = $"{repoName}.{hostName}.{tag}"

      cp "\frNYI\f0!"
      1
    | None ->
      1

let run args =
  let oo = args |> parseOptions
  match oo with
  | None ->
    cp ""
    Usage.usage "layer"
    1
  | Some o ->
    o |> runLayer

