module AppRepoInit

open System
open System.IO

open GitVaultLib.Configuration

open FldVault.Core.Vaults

open GitVaultLib.GitThings

open ColorPrint
open CommonTools

type private KeySource =
  | KeySpecifier of string
  | KeyAuto

type private Options = {
  VaultAnchorName: string
  BundleAnchorName: string
  RepoName: string
  RepoFolder: GitRepoFolder
  HostName: string
}

let private runAppRepoInit o =
  let centralSettings = CentralSettings.Load()
  let vaultAnchor =
    centralSettings.Anchors.[o.VaultAnchorName]
  let bundleAnchor =
    centralSettings.BundleAnchors.[o.BundleAnchorName]
  let repoFolder = o.RepoFolder
  let repoName =
    if o.RepoName |> String.IsNullOrEmpty then
      repoFolder.AutoRepoName
    else
      o.RepoName
  let vaultFolder = Path.Combine(vaultAnchor, repoName)
  let bundleFolder = Path.Combine(bundleAnchor, o.VaultAnchorName, repoName)
  cp "Info:"
  cp $"  Repository    \fb{repoName,15}\f0: \fc{repoFolder.Folder}\f0 / \fb{repoFolder.GitFolder}\f0."
  cp $"  Vault folder  \fb{o.VaultAnchorName,15}\f0: \fg{vaultFolder}\f0."
  cp $"  Bundle folder \fb{o.BundleAnchorName,15}\f0: \fy{bundleFolder}\f0."
  cp $"  Settings file:                 \fg{repoFolder.GitvaultSettingsFile}\f0."

  let error, repoSettings = repoFolder.TryInitGitVaultSettings(
    centralSettings, o.VaultAnchorName, o.BundleAnchorName, o.HostName, repoName)
  if repoSettings = null then
    cp $"\foError: {error}\f0. Initialization aborted"
    1
  else
    if error |> String.IsNullOrEmpty |> not then
      cp $"\foWarning: {error}\f0."
    else
      cp "\fgRepository initialized successfully.\f0"
    let repoVaultFolder = repoSettings.GetRepoVaultFolder(centralSettings)
    let keyError = repoVaultFolder.CanGetKey()
    if keyError |> String.IsNullOrEmpty |> not then
      cp $"\foBeware!\fy {keyError}\f0."
      cp $"You can set the key by putting its zkey file into \fc{vaultFolder}\f0."
    else
      let bundleInfo = repoSettings.ToBundleInfo(centralSettings)
      cp "Bundle Info:"
      cp $"  Bundle file: \fy{bundleInfo.BundleFile}\f0."
      cp $"  Vault file:  \fg{bundleInfo.VaultFile}\f0."
      // let keyText = bundleInfo.KeyInfo.ToZkeyTransferString(false)
      // cp $"  ZKey:\n\fg{keyText}\f0."
      cp $"   Key ID:     \fb{bundleInfo.KeyInfo.KeyGuid}\f0."
      cp "Vault is ready for use.\f0"
    0

let run args =
  let centralSettings = CentralSettings.Load()
  if centralSettings.Anchors.Count = 0 then
    cp "\frNo vault anchor folders have been registered yet!\f0"
    cp "Use the \fogitvault anchor add\f0 command to get started."
    1
  else
    let rec parseMore o args =
      match args with
      | "-v" :: rest ->
        verbose <- true
        parseMore o rest
      | "--help" :: _
      | "-h" :: _ ->
        None
      | "-a" :: name :: rest ->
        let ok, _ = centralSettings.Anchors.TryGetValue(name)
        if ok |> not then
          cp $"\foVault anchor name \fy{name}\fo is not defined\f0."
          None
        else
          rest |> parseMore { o with VaultAnchorName = name }
      | "-b" :: name :: rest ->
        let ok, _ = centralSettings.BundleAnchors.TryGetValue(name)
        if ok |> not then
          cp $"\foBundle anchor name \fy{name}\fo is not defined\f0."
          None
        else
          rest |> parseMore { o with BundleAnchorName = name }
      | "-f" :: witness :: rest ->
        let repo = witness |> GitRepoFolder.LocateRepoRootFrom
        if repo = null then
          cp $"\foWitness folder \fy{witness}\fo is not inside a git repository\f0."
          None
        else
          rest |> parseMore { o with RepoFolder = repo }
      | "-host" :: hostName :: rest ->
        if CentralSettings.IsValidName(hostName, false) then
          cp $"\foThe name \fy{hostName}\fo is not valid as a gitvault 'host name'\f0."
          None
        else
          rest |> parseMore { o with HostName = hostName }
      | "-n" :: repoName :: rest ->
        if CentralSettings.IsValidName(repoName, true) then
          cp $"\foThe name \fy{repoName}\fo is not valid as a gitvault 'repository name'\f0."
          None
        else
          rest |> parseMore { o with RepoName = repoName }
      | [] ->
        let o =
          if o.RepoFolder = null then
            let repo = Environment.CurrentDirectory |> GitRepoFolder.LocateRepoRootFrom
            { o with RepoFolder = repo }
          else
            o
        if o.VaultAnchorName |> String.IsNullOrEmpty then
          cp "\foVault anchor name not specified\f0."
          None
        elif o.VaultAnchorName |> centralSettings.Anchors.ContainsKey |> not then
          cp $"\foVault anchor name \fy{o.VaultAnchorName}\fo is not defined\f0."
          None
        elif o.BundleAnchorName |> String.IsNullOrEmpty then
          cp "\foBundle anchor name not specified\f0."
          None
        elif o.BundleAnchorName |> centralSettings.BundleAnchors.ContainsKey |> not then
          cp $"\foBundle anchor name \fy{o.BundleAnchorName}\fo is not defined\f0."
          None
        elif o.RepoFolder = null then
          // This implies that using the current directory as the witness folder failed
          cp "\foThe current directory is not in a GIT repository (and no \fg-f\fo option given)\f0."
          None
        else
          // A null HostName or RepoName is ok (will be handled in library code)
          Some o
      | x :: _ ->
        cp $"\foUnknown option \fy{x}\f0."
        None
    let oo = args |> parseMore {
      VaultAnchorName = null
      BundleAnchorName = "default"
      RepoName = null
      RepoFolder = null
      HostName = centralSettings.DefaultHostname
    }
    match oo with
    | None ->
      Usage.usage "repo-init"
      1
    | Some o ->
      o |> runAppRepoInit

