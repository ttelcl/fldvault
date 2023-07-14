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
    if vaultFile.KeyId |> NullKey.IsNullKey then
      cp $"Key ID \fb{vaultFile.KeyId}\f0. \foWarning\f0: \fonull key!\f0; Vault content is merely obfuscated, not encrypted."
    else
      cp $"Key ID \fy{vaultFile.KeyId}\f0."
    use keyChain = new KeyChain()
    let seedService = KeyUtilities.setupKeySeedService()
    if o.PublicOnly |> not then
      KeyUtilities.hatchKeyIntoChain seedService vaultFile keyChain
    let fileElements =
      vaultFile.FileElements()
      |> Seq.map(fun fe -> new FileElement(fe))
      |> Seq.toArray
    if fileElements.Length = 0 then
      cp "\foThis vault is empty\f0."
    else
      use cryptor = if o.PublicOnly then null else vaultFile.CreateCryptor(keyChain)
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

