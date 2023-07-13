module KeyUtilities

open System
open System.IO

open FldVault.Core.Crypto
open FldVault.Core.KeyResolution
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools

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

let loadKeyIntoChain (vaultFile: VaultFile) (keyChain: KeyChain) =
  let pkif = vaultFile.GetPassphraseInfo()
  if pkif = null then
    failwith "No key information found in the vault"
  let rawKey = keyChain.FindOrImportKey(pkif.KeyId, UnlockStore.Default)
  if rawKey = null then
    cp $"Key \fy{pkif.KeyId}\f0 is \folocked\f0."
    use k = pkif |> KeyEntry.enterKeyFor
    k |> keyChain.PutCopy |> ignore
  else
    cp $"Key \fy{pkif.KeyId}\f0 is \fcunlocked\f0."

let setupKeySeedService () =
  KeySeedService.NewStandardKeyService(fun guid -> KeyEntry.enterRawKey $"Please enter passphrase for key '{guid}'")

let loadKeyIntoChain2 (seedService: KeySeedService) vaultFile keyChain =
  let seed = vaultFile |> seedService.TryCreateSeedForVault
  if seed = null then
    failwith $"Insufficient information to locate key '{vaultFile.KeyId}'"
  if keyChain |> seed.TryResolveKey |> not then
    failwith $"Key not found or not available: '{vaultFile.KeyId}'"
