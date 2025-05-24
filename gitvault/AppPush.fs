module AppPush

open System
open System.IO

open GitVaultLib.Configuration

open FldVault.Core.Vaults

open GitVaultLib.GitThings
open GitVaultLib.VaultThings

open Newtonsoft.Json

open ColorPrint
open CommonTools

type private PushTarget =
  | All
  // | Anchor of string

type private Options = {
  Targets: PushTarget
}

let private runPush o =
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
        0, repoRoot, repoSettings
  if status <> 0 then
    status
  else
    let repotips = GitTips.ForRepository(repoRoot.Folder)

    for repoAnchorSettings in repoSettings.ByAnchor.Values do
      let vaultFolder = repoAnchorSettings.GetRepoVaultFolder(centralSettings)
      let bundleFile = repoAnchorSettings.GetBundleFileName(centralSettings)
      let bundleTips = GitTips.ForBundleFile(bundleFile) // empty if there is no bundle file
      if repotips.AreSame(bundleTips) then
        cp $"\fgNo changes\f0 in branches or tags for \fc{repoAnchorSettings.VaultAnchor}\f0|\fg{repoAnchorSettings.HostName}\f0. Skipping"
      else
        cp $"Bundle is out of date: \fc{repoAnchorSettings.VaultAnchor}\f0|\fg{repoAnchorSettings.HostName}\f0."
        let bundleFile = repoAnchorSettings.GetBundleFileName(centralSettings) //bundleInfo.BundleFile
        cp $"Bundling to \fy{bundleFile}\f0..."
        let result = GitRunner.CreateBundle(bundleFile, null)
        if result.StatusCode <> 0 then
          cp $"\frError\fo: Bundling failed with status code \fc{result.StatusCode}\f0."
          for line in result.ErrorLines do
            cp $"\fo  {line}\f0"
        else
          cp $"\fgBundle created successfully\f0."
      let keyError = repoAnchorSettings.CanGetKey(centralSettings)
      if keyError |> String.IsNullOrEmpty |> not then
        cp $"\foKey unavailable\f0 ({vaultFolder.VaultFolder}) {keyError}\f0 \frSkipping encryption stage\f0."
      else
        let bundleInfo = repoAnchorSettings.ToBundleInfo(centralSettings)
        ()
    cp "NYI: runPush not implemented yet"
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
    | "-all" :: rest ->
      rest |> parseMore { o with Targets = All }
    | [] ->
      Some o
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  let oo = args |> parseMore {
    Targets = All
  }
  match oo with
  | None ->
    Usage.usage "push"
    1
  | Some o ->
    o |> runPush




