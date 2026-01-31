module AppPush

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
open GitVaultLib.Delta
open GitVaultLib.GitThings
open GitVaultLib.VaultThings

open ColorPrint
open CommonTools

type private PushTarget =
  | All
  // | Anchor of string

type private Options = {
  Targets: PushTarget
  Full: bool
}

let private runPush o =
  let centralSettings = CentralSettings.Load()
  let bundleRecordCache = new BundleRecordCache(centralSettings, null, null, null)
  let kss = new KeyServerService()
  use keychain = new KeyChain()
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
        let recipes = DeltaRecipes.TryLoad(repoRoot)
        let hasRecipes = recipes <> null && recipes.Recipes.Count > 0;
        if hasRecipes && not(o.Full) then
          cp $"\foThis repository has delta recipes. Pass \fg-full\fo to confirm full send mode\f0."
          cp "(\foor use \fygitvault delta send\fo instead to use delta mode\f0)"
          1, repoRoot, repoSettings
        else
          if hasRecipes then
            cp "Using full bundle mode (delta recipes ignored)\f0."
          else
            cp "\fkUsing full bundle mode (no delta recipes found)\f0."
          0, repoRoot, repoSettings
  if status <> 0 then
    status
  else
    let repoBundleSource = repoRoot.GetBundleSource()
    let repotips = GitTips.ForRepository(repoRoot.Folder)
    let reporoots = GitRoots.ForRepository(repoRoot.Folder)
    for repoAnchorSettings in repoSettings.ByAnchor.Values do
      let repoAnchorBundleSource = repoAnchorSettings.GetBundleSource(centralSettings)
      let vaultFolder = repoAnchorSettings.GetRepoVaultFolder(centralSettings)
      let bundleFile = repoAnchorSettings.GetBundleFileName(centralSettings)
      let bundleTips = GitTips.ForBundleFile(bundleFile) // empty if there is no bundle file
      // Todo: report differences between bundle and repo tips
      let bundledOk =
        if repotips.AreSame(bundleTips) then
          cp $"\fgNo changes\f0 in branches or tags for \fc{repoAnchorSettings.VaultAnchor}\f0|\fg{repoAnchorSettings.HostName}\f0. Skipping"
          true
        elif repoAnchorBundleSource = null then
          cp $"\frNo bundle source found for this anchor+repo+host.\fo This repo is not the owner\f0 (is there a name conflict with an external bundle?) Skipping."
          false
        elif repoBundleSource.SameSource(repoAnchorBundleSource) |> not then
          cp $"\frThis repo is not the owner of 'its' bundle.\fo It is owned by \fc{repoAnchorBundleSource.SourceFolder}\f0. Skipping."
          false
        else
          cp $"Bundle is out of date: \fc{repoAnchorSettings.VaultAnchor}\f0|\fg{repoAnchorSettings.HostName}\f0."
          let bundleFile = repoAnchorSettings.GetBundleFileName(centralSettings) //bundleInfo.BundleFile
          cp $"Bundling to \fc{bundleFile}\f0..."
          let result = GitRunner.CreateBundle(bundleFile, null)
          if result.StatusCode <> 0 then
            cp $"\frError\fo: Bundling failed with status code \fc{result.StatusCode}\f0."
            for line in result.ErrorLines do
              cp $"\fo  {line}\f0"
            false
          else
            cp $"\fgBundle created successfully\f0."
            true
      let keyError = repoAnchorSettings.CanGetKey(centralSettings)
      if keyError |> String.IsNullOrEmpty |> not then
        cp $"\foKey unavailable\f0 ({vaultFolder.VaultFolder}) {keyError}\f0 \frSkipping encryption stage\f0."
      elif bundledOk |> not then
        cp "\frBundling stage failed.\fo Skipping Encryption phase\f0."
      else
        let bundleRecord = repoAnchorSettings.GetBundleRecord(bundleRecordCache)
        //let bundleInfo = repoAnchorSettings.ToBundleInfo(centralSettings)
        let keyInfo = bundleRecord.GetZkeyOrFail()
        let vaultFileName = bundleRecord.GetVaultFileNameOrFail()
        let keyId = keyInfo.KeyGuid
        let vaultIsOutdated =
          FileUtils.IsFileOutdated(vaultFileName, bundleRecord.BundleFileName)
        if vaultIsOutdated |> not then
          cp $"\fgVault file is up-to-date\f0 ({vaultFileName})"
        else
          cp $"Vault file is out-of-date \f0(\fc{vaultFileName}\f0)"
          let keyLoaded =
            if keyId |> keychain.ContainsKey |> not then
              if kss.ServerAvailable then
                let presence = kss.LookupKeySync(keyId, keychain)
                match presence with
                | KeyPresence.Unavailable ->
                  cp $"\foKey \fb{keyId}\fo not found in the key server\f0."
                  cp $"\fySkipping encryption\f0. To fix, unlock the key in the key server GUI and try again."
                  false
                | KeyPresence.Cloaked ->
                  cp $"Key \fb{keyId}\f0 is present but currently \fohidden\f0 in the key server."
                  cp $"\fySkipping encryption\f0. To fix, un-hide the key in the key server GUI and try again."
                  false
                | KeyPresence.Present ->
                  cp $"Key \fb{keyId}\f0 \fgloaded successfully\f0 from the key server\f0."
                  true
                | x -> 
                  cp $"\frInternal Error\fo: Unrecognized key presence status: \fr{x}\f0."
                  false
              else
                cp $"\foKey server is not available, cannot load key \fb{keyId}\f0."
                cp $"\fySkipping encryption\f0. To fix, start the \fgZvault Key Server GUI\f0, and try again."
                false
            else
              true
          if keyLoaded then // else: a message was already printed
            let metadata = new JObject()
            metadata.Add("tips", repotips.TipMap |> JToken.FromObject)
            metadata.Add("roots", reporoots.Roots |> JArray.FromObject)
            //let dbg = JsonConvert.SerializeObject(metadata, Formatting.Indented)
            //cp $"Metadata for vault file: \fw{dbg}\f0"
            let encryptionTask =
              task {
                let! writtenFile =
                  MvltWriter.CompressAndEncrypt(
                    bundleRecord.BundleFileName,
                    vaultFileName,
                    keychain,
                    keyInfo.ToPassphraseKeyInfoFile(),
                    ?metadata = Some(metadata))
                return writtenFile
              }
            let writtenFile =
              encryptionTask |> Async.AwaitTask |> Async.RunSynchronously
            cp $"Vault file \fc{writtenFile}\f0 created \fgsuccessfully\f0."
            ()
    0

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
    | "-full" :: rest ->
      rest |> parseMore { o with Full = true }
    | [] ->
      Some o
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  let oo = args |> parseMore {
    Targets = All
    Full = false
  }
  match oo with
  | None ->
    Usage.usage "push"
    1
  | Some o ->
    o |> runPush




