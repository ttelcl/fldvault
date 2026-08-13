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

open ColorPrint
open CommonTools

type private Options = {
  LayerTag: string option
  Dependencies: string list
  Force: bool
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
      let tag =
        if tag |> String.IsNullOrEmpty then
          None
        else
          // TODO: argument validation
          tag |> Some
      rest |> parseMore {o with LayerTag = tag }
    | "-on" :: tag :: rest ->
      if tag |> String.IsNullOrEmpty then
        cp "\fo-on\fr argument cannot be empty\f0."
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
    status
  else
    let anchorSettings = repoSettings.ByAnchor.Values |> Seq.exactlyOne
    let anchorName = anchorSettings.VaultAnchor
    let hostName = anchorSettings.HostName
    let repoName = anchorSettings.RepoName
    let bundleRecordCache = new BundleRecordCache(centralSettings, null, null, null)
    let kss = new KeyServerService()
    use keychain = new KeyChain()
    let now = DateTime.Now
    let tag =
      match o.LayerTag with
      | Some tag -> tag
      | None -> now.ToString("yyyyMMdd-HHmmss")
    cp $"Building layer bundle '\fc{repoName}\f0.\fy{hostName}\f0.\fg{tag}\f0' in anchor '\fb{anchorName}\f0'."
    cp "\frNYI\f0!"
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

