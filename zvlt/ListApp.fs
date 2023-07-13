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
    | file :: rest when file.EndsWith(".zvlt") ->
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
    cp $"Vault file created on \fc{vaultFile.Header.TimeStamp |> formatLocal}\f0 format \fyZVLT {major}.{minor}\f0."
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
          cp $"Key \fg{pkif.KeyId}\f0 created \fc{pkif.UtcKeyStamp |> formatLocal}\f0 - \fcLocked\f0"
          use rawkey = pkif |> KeyEntry.enterKeyFor
          rawkey |> keyChain.PutCopy
        else
          cp $"Key \fg{pkif.KeyId}\f0 created \fc{pkif.UtcKeyStamp |> formatLocal}\f0 - \foUNLOCKED\f0"
          rawKey
    let fileElements =
      vaultFile.FileElements()
      |> Seq.map(fun fe -> new FileElement(fe))
      |> Seq.toArray
    if fileElements.Length = 0 then
      cp "\foThis vault is empty\f0."
    else
      use cryptor = if rawKey = null then null else vaultFile.CreateCryptor(keyChain)
      use reader = new VaultFileReader(vaultFile, cryptor)
      for fe in fileElements do
        let header = fe.GetHeader(reader)
        let length = fe.GetContentLength()
        cp $"File id \fy{header.FileId}\f0. Added on \fc{header.EncryptionStampUtc |> formatLocal}\f0:"
        cp $"   Total content length = \fb{length}\f0."
        if cryptor <> null then
          let metadata = fe.GetMetadata(reader)
          if metadata.Name |> String.IsNullOrEmpty then
            cp $"   File name: \foNot specified\f0."
          else
            cp $"   File name: \fg{metadata.Name}\f0."
          if metadata.Stamp.HasValue then
            cp $"   File time: \fc{metadata.UtcStamp.Value |> formatLocal}\f0."
          else
            cp $"   File time: \foNot specified\f0."
          if metadata.Size.HasValue then
            let color = if metadata.Size.Value = length then "\fb" else "\fr"
            cp $"   File size: {color}{metadata.Size.Value}\f0."
          else
            cp $"   File size: \foNot specified\f0."
          if metadata.OtherFields.Count = 0 then
            cp $"   Custom metadata: \fonone\f0."
          else
            cp $"   Custom metadata: \fc{metadata.OtherFields.Count}\f0."
          ()
    0
  | None ->
    Usage.usage "list"
    0

