module KeyUtilities

open System
open System.IO

open FldVault.Core.Crypto
open FldVault.Core.KeyResolution
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools
open FldVault.KeyServer

/// Get a PassphraseKeyInfoFile instance from a *.pass.keyinfo or *.zvlt file
let getPassKeyInfoFromFile (fileName: string) =
  if fileName.EndsWith(".pass.key-info", StringComparison.InvariantCultureIgnoreCase) then
    PassphraseKeyInfoFile.ReadFrom(fileName)
  elif fileName.EndsWith(".zvlt", StringComparison.InvariantCultureIgnoreCase) then
    let vf = VaultFile.Open(fileName)
    let pkif = vf.GetPassphraseInfo()
    if pkif = null then
      failwith $"Vault '{Path.GetFileName(fileName)}' does not contain its own key-info block."
    pkif
  else
    failwith $"Unrecognized key provider file '{Path.GetFileName(fileName)}'"

/// Return a new KeySeedService with just the null key service preinstalled
/// Use a pipeline of addXXXXKeySeedService functions to complete the setup
let minimalKeySeedService () =
  KeySeedService.NewEmptyKeyService()
    .AddNullKeyService()

let addUnlockKeySeedService (ks: KeySeedService) =
  ks.AddUnlockStoreService ()

let addPassphraseKeySeedService (ks: KeySeedService) =
  let keyEntry (guid: Guid) =
    KeyEntry.enterRawKey $"Please enter passphrase for key '{guid}'"
  ks.AddPassphraseKeyService(keyEntry)

let completeKeySeedService ks =
  ks :> IKeySeedService

let addKeyServerSeedService (kss: KeyServerService) (ks: KeySeedService) =
  if kss <> null then // else silently ignore
    if kss.ServerAvailable |> not then
      cp "\foKey server not available\f0."
      ks
    else
      let ksss = new KeyServerSeedService(kss)
      ksss |> ks.AddSeedService
  else
    ks

let addConditional condition (adder: KeySeedService -> KeySeedService) (ks: KeySeedService) =
  if condition then ks |> adder else ks  

let setupKeySeedService useUnlock usePassphrase keyserver =
  let svc = minimalKeySeedService()
  if useUnlock then
    svc |> addUnlockKeySeedService |> ignore
  match keyserver with
  | Some(kss) ->
    svc |> addKeyServerSeedService kss |> ignore
  | None ->
    ()
  if usePassphrase then
    svc |> addPassphraseKeySeedService |> ignore
  svc |> completeKeySeedService

let hatchKeyIntoChain (seedService: IKeySeedService) vaultFile keyChain =
  let seed = vaultFile |> seedService.TryCreateSeedForVault
  if seed = null then
    failwith $"Insufficient information to locate key '{vaultFile.KeyId}'"
  if seed.TryResolveKey(keyChain) |> not then
    failwith $"Key not found or not available: '{vaultFile.KeyId}'"

let trySeedFromKeyInfoFile (seedService: IKeySeedService) keyInfoFile =
  keyInfoFile |> seedService.TryCreateFromKeyInfoFile

let trySeedFromVault (seedService: IKeySeedService) vault =
  vault |> seedService.TryCreateSeedForVault

//
