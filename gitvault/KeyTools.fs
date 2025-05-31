module KeyTools

open System
open System.IO

open FldVault.KeyServer
open FldVault.Core.Crypto
open FldVault.Core.Mvlt
open FldVault.Core.Vaults

open GitVaultLib.Configuration
open GitVaultLib.GitThings
open GitVaultLib.VaultThings

open ColorPrint
open CommonTools

type KeyInfrastructure = {
  KeyServer: KeyServerService
  Chain: KeyChain
}

type KeyToken = {
  KeyId: Guid
  Chain: KeyChain
}

type KeyLookupFailure =
  | NoServer
  | Unavailable
  | Hidden

type KeyLookupResult =
  | Failure of KeyLookupFailure
  | Success of KeyToken

let createInfrastructure keyChain =
  {
    KeyServer = new KeyServerService()
    Chain = keyChain
  }

let lookupKey (infra: KeyInfrastructure) keyId =
  if keyId |> infra.Chain.ContainsKey then
    // stay silent - it was already loaded before
    { KeyId = keyId; Chain = infra.Chain } |> KeyLookupResult.Success
  elif infra.KeyServer.ServerAvailable |> not then
    cp "  \frCannot resolve keys. \foKey server is not running\f0."
    KeyLookupFailure.NoServer |> KeyLookupResult.Failure
  else
    let presence = infra.KeyServer.LookupKeySync(keyId, infra.Chain)
    match presence with
    | KeyPresence.Unavailable ->
      cp $"  \foKey \fy{keyId}\fo is not unlocked in the key server (enter the passphrase there to unlock)\f0."
      KeyLookupFailure.Unavailable |> KeyLookupResult.Failure
    | KeyPresence.Cloaked ->
      cp $"  \foKey \fy{keyId}\fo is available in the key server, but hidden\f0. Please unhide it there."
      KeyLookupFailure.Hidden |> KeyLookupResult.Failure
    | KeyPresence.Present ->
      cp $"  (\fwKey \fg{keyId}\fw sucessfully loaded\f0)"
      { KeyId = keyId; Chain = infra.Chain } |> KeyLookupResult.Success
    | x ->
      failwith $"Unrecognized server response {x}"

let lookupVaultFolderKey (infra: KeyInfrastructure) (vaultFolder: RepoVaultFolder) =
  let zkey = vaultFolder.GetVaultKey()
  let keyId = zkey.KeyGuid
  if keyId |> infra.Chain.ContainsKey then
    // stay silent - it was already loaded before
    { KeyId = keyId; Chain = infra.Chain } |> KeyLookupResult.Success
  elif infra.KeyServer.ServerAvailable |> not then
    cp "  \frCannot resolve keys. \foKey server is not running\f0."
    KeyLookupFailure.NoServer |> KeyLookupResult.Failure
  else
    let presence = infra.KeyServer.LookupKeySync(keyId, infra.Chain)
    match presence with
    | KeyPresence.Unavailable ->
      // Make sure there is a sensible registration present in the server
      let folderKeyFile = vaultFolder.GetFolderKeyFileName(true)
      infra.KeyServer.RegisterFileSync(folderKeyFile, infra.Chain) |> ignore
      cp $"  \foKey \fy{keyId}\fo is not unlocked in the key server (enter the passphrase there to unlock)\f0."
      KeyLookupFailure.Unavailable |> KeyLookupResult.Failure
    | KeyPresence.Cloaked ->
      cp $"  \foKey \fy{keyId}\fo is available in the key server, but hidden\f0. Please unhide it there."
      KeyLookupFailure.Hidden |> KeyLookupResult.Failure
    | KeyPresence.Present ->
      cp $"  (\fwKey \fg{keyId}\fw sucessfully loaded\f0)"
      { KeyId = keyId; Chain = infra.Chain } |> KeyLookupResult.Success
    | x ->
      failwith $"Unrecognized server response {x}"
