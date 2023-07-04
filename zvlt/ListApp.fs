module ListApp

open System
open System.IO

open FldVault.Core
open FldVault.Core.Crypto
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools

type private ListOptions = {
  VaultFile: string
  PublicOnly: bool
}

let private formatLocal (stamp: DateTime) =
  stamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz")

let runList args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      None
    | "-p" :: rest
    | "-pub" :: rest
    | "-public" :: rest ->
      rest |> parseMore {o with PublicOnly = true}
    | "-vf" :: file :: rest ->
      rest |> parseMore {o with VaultFile = file}
    | [] ->
      if o.VaultFile |> String.IsNullOrEmpty then
        failwith "No vault file specified"
      Some(o)
    | x :: _ ->
      failwith $"Unrecognized argument '{x}'"
  let oo = args |> parseMore {
    VaultFile = null
    PublicOnly = false
  }
  match oo with
  | Some(o) ->
    let vaultFile = VaultFile.Open(o.VaultFile)
    let major, minor =
      let version = vaultFile.Header.Version
      version >>> 16, version &&& 0x0FFFF
    cp $"Vault file created on \fb{vaultFile.Header.TimeStamp |> formatLocal}\f0 format \foZVLT {major}.{minor}\f0."
    let pkif = vaultFile.GetPassphraseInfo()
    if pkif = null then
      failwith "No key information found in the vault"
    let unlockCache = new UnlockStore()
    use keyChain = new KeyChain()
    let rawKey =
      if o.PublicOnly then
        null
      else
        let rawKey = keyChain.FindOrImportKey(pkif.KeyId, unlockCache)
        if rawKey = null then
          cp $"Key \fg{pkif.KeyId}\f0 created \fb{pkif.UtcKeyStamp |> formatLocal}\f0 - \fcLocked\f0"
          pkif |> KeyEntry.enterKeyFor :> KeyBuffer
        else
          cp $"Key \fg{pkif.KeyId}\f0 created \fb{pkif.UtcKeyStamp |> formatLocal}\f0 - \foUNLOCKED\f0"
          rawKey
    let fileElements =
      vaultFile.FileElements()
      |> Seq.map(fun fe -> new FileElement(fe))
      |> Seq.toArray
    use cryptor = if rawKey = null then null else vaultFile.CreateCryptor(keyChain)
    use reader = new VaultFileReader(vaultFile, cryptor)
    for fe in fileElements do
      cp $"File id {fe.GetHeader(reader)}"
    0
  | None ->
    Usage.usage "list"
    0

