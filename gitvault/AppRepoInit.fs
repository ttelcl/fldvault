module AppRepoInit

open System
open System.IO

open GitVaultLib.Configuration

open FldVault.Core.Vaults

open GitVaultLib.GitThings

open ColorPrint
open CommonTools

type private Options = {
  VaultAnchorName: string
  BundleAnchorName: string
  KeyInfo: Zkey
  RepoName: string
  RepoFolder: GitRepoFolder
  HostName: string
}

let private runAppRepoInit o =
  cp $"\frNot Yet Implemented\f0."
  1

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
      | "-k" :: keyFile :: rest ->
        let pkif = PassphraseKeyInfoFile.TryFromFile(keyFile)
        if pkif = null then
          cp $"\foKey file \fy{keyFile}\fo is not recognized as a key info source\f0."
          None
        else
          let zkey = Zkey.FromPassphraseKeyInfoFile(pkif)
          rest |> parseMore { o with KeyInfo = zkey }
      | "-K" :: rest ->
        cp "\fo'\fg-K\fo' option is not yet implemented\f0. Use \fg-k\f0 instead."
        None
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
        elif o.KeyInfo = null then
          cp "\foKey descriptor not specified\f0."
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
      KeyInfo = null
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

