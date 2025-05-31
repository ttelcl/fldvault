module AppSettingsShow

open System
open System.IO

open Newtonsoft.Json

open GitVaultLib.Configuration
open GitVaultLib.GitThings

open ColorPrint
open CommonTools

let private formatFileStatus fileName =
  if File.Exists(fileName) then
    let lastModified = File.GetLastWriteTime(fileName)
    let lastModifiedText = lastModified.ToString("yyyy-MM-dd HH:mm:ss")
    $"\fb{lastModifiedText}"
  else
    $"\frmissing"

let run args =
  // there are no arguments in this case, no need to parse them
  let settings = CentralSettings.Load()
  let json = JsonConvert.SerializeObject(settings, Formatting.Indented)

  cp $"\foMachine-wide\f0 gitvault settings (\fg{CentralSettings.CentralSettingsFileName}\f0):\n"
  cp $"---\n\fw{json}\f0\n---\n"

  let repoRoot = "." |> GitRepoFolder.LocateRepoRootFrom
  let bundleRecordCache = new BundleRecordCache(settings, null, null, null)
  if repoRoot <> null then
    cp $"Repository root: \fg{repoRoot.Folder}\f0."
    let repoSettings = repoRoot.TryLoadGitVaultSettings()
    if repoSettings <> null then
      cp $"Repository settings file: \fg{repoRoot.GitvaultSettingsFile}\f0."
      cp "---"
      cp $"\fw{JsonConvert.SerializeObject(repoSettings, Formatting.Indented)}\f0"
      cp "---"
      for anchor in repoSettings.ByAnchor.Values do
        let keyError = anchor.CanGetKey(settings)
        if keyError |> String.IsNullOrEmpty then
          let bundleRecord = anchor.GetBundleRecord(bundleRecordCache)
          let e, zkey = bundleRecord.TryGetZkey()
          // let bundleInfo = anchor.ToBundleInfo(settings)
          cp $"Vault key id: \fc{zkey.KeyGuid}\f0."
          let vaultFile = bundleRecord.GetVaultFileNameOrFail()
          let vaultFileStatus =  vaultFile |> formatFileStatus 
          cp $"Vault file:   \fg{vaultFile}\f0 ({vaultFileStatus}\f0)"
          let bundleFile = bundleRecord.BundleFileName
          let bundleFileStatus = bundleFile |> formatFileStatus
          cp $"Bundle file:  \fy{bundleFile}\f0 ({bundleFileStatus}\f0)"
        else
          cp $"Key is not available yet: \fo{keyError}\f0."
          let rvf = anchor.GetRepoVaultFolder(settings)
          cp $"Vault folder: \fg{rvf.VaultFolder}\f0."
    else
      cp $"\foNo gitvault settings found\f0."
  else
    cp "No git repository found in the current folder or its parents\f0."
  
  if settings.Anchors.Count = 0 then
    cp "\frNo vault anchor folders have been registered yet!\f0"
    cp "Use the \fogitvault anchor add\f0 command to get started."
    cp ""
    Usage.usage "anchor-add"
    1
  else
    0

