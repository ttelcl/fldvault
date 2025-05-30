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

type private StatusTarget =
  | All
  | CurrentRepo

type private Options = {
  Targets: StatusTarget
  WitnessFolder: string
}

let private runBundlesStatus o =
  let settings = CentralSettings.Load()
  let repo = o.WitnessFolder |> GitRepoFolder.LocateRepoRootFrom
  if repo = null then
    cp "\foNo git repository found here\f0."
    1
  else
    let repoSettings = repo.TryLoadGitVaultSettings()
    if repoSettings = null then
      cp $"\foNo gitvault settings found in repository \fy{repo.Folder}\f0."
      1
    else
      for anchorSettings in repoSettings.ByAnchor.Values do
        let repoDb = new LogicalRepository(settings, anchorSettings.VaultAnchor, anchorSettings.RepoName)
        let zkey = repoDb.VaultFolder.GetVaultKey()
        cp $"Anchor \fg{repoDb.AnchorName}\f0 ({repoDb.VaultFolder} / {zkey.KeyGuid}):"
        let keyError = repoDb.VaultFolder.CanGetKey()
        if keyError |> String.IsNullOrEmpty |> not then
          cp $"  \foNo vault key available:\fy {keyError}\f0."
        else
          let repoRecord = repoDb.RegisterSourceRepository(repo, anchorSettings)
          repoDb.RegisterVaults()
          repoDb.RegisterBundles()
          let byhost =
            repoDb.RecordCache.Records.Values
            |> Seq.sortBy (fun br -> br.HostName)
            |> Seq.toArray
          for record in byhost do
            cpx $"\fg{repoDb.AnchorName,10}\f0.\fc{record.HostName,-15}\f0 "
            let kind =
              if record.Key.Equals(repoRecord.Key) then 
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
                "\fr-missing-\f0"
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
                "\fr-missing-\f0"
            let arrow =
              if needsUpdate |> not then
                "\f0   \f0"
              elif record.HasSourceFile then
                "\fc<--\f0"
              else
                "\fy-->\f0"
            cpx $"Vault: {vaultTime,-23} {arrow} Bundle: {bundleTime,-23}"
            cp ""
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
    | "-all" :: rest -> // not implemented yet
      rest |> parseMore { o with Targets = All }
    | "-current" :: rest ->
      rest |> parseMore { o with Targets = CurrentRepo }
    | "-f" :: witnessFolder :: rest ->
      let witnessFolder = (witnessFolder |> Path.GetFullPath).TrimEnd('/', '\\')
      if Directory.Exists(witnessFolder) |> not then
        cp $"\fFolder \fy{witnessFolder}\f0 does not exist."
        None
      else
        rest |> parseMore { o with WitnessFolder = witnessFolder }
    | [] ->
      Some o
    | x :: _ ->
      cp $"\frUnknown option: \fy{x}\f0."
      None
  let oo = args |> parseMore { 
    Targets = CurrentRepo
    WitnessFolder = Environment.CurrentDirectory
  }
  match oo with
  | None ->
    Usage.usage "bundles-status"
    1
  | Some o ->
    o |> runBundlesStatus
