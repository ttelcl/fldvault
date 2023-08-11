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

//let setupKeySeedServiceOLD () =
//  cp "\frWarning\f0: \foUsing deprecated key access service setup\f0."
//  let keyEntry (guid: Guid) =
//    KeyEntry.enterRawKey $"Please enter passphrase for key '{guid}'"
//  KeySeedService.NewStandardKeyService(keyEntry) :> IKeySeedService

let setupKeySeedService () =
  // Backward compatibility mode, also example of the "new" way
  cp "\frWarning\f0: \foUsing deprecated key access service setup\f0."
  minimalKeySeedService()
  |> addConditional true addUnlockKeySeedService
  |> addConditional true addPassphraseKeySeedService
  |> completeKeySeedService

let setupkeySeedServiceEx includeUnlock (kss: KeyServerService) includePassphrase =
  minimalKeySeedService()
  |> addConditional includeUnlock addUnlockKeySeedService
  |> addConditional (kss <> null) (addKeyServerSeedService kss)
  |> addConditional includePassphrase addPassphraseKeySeedService
  |> completeKeySeedService

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
