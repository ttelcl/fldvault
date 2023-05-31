module EncryptApp

open System
open System.IO

open FldVault.Core
open FldVault.Core.Crypto
open FldVault.Core.Vaults

open ColorPrint
open CommonTools

type private PutOptions = {
  KeyFile: string
  DataFile: string
  Force: bool
}

let runPut args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-kf" :: keyfile :: rest ->
      rest |> parseMore {o with KeyFile = keyfile}
    | "-f" :: datafile :: rest ->
      rest |> parseMore {o with DataFile = datafile}
    | "-F" :: rest ->
      rest |> parseMore {o with Force = true}
    | [] ->
      if o.KeyFile |> String.IsNullOrEmpty then
        failwith "Missing key-info file (-kf)"
      if o.DataFile |> String.IsNullOrEmpty then
        failwith "Missing data file (-f)"
      o
    | x :: _ ->
      failwith $"Unrecognized argument '{x}'"
  let o = args |> parseMore {
    KeyFile = null
    DataFile = null
    Force = false
  }
  let keyFile = Path.GetFullPath(o.KeyFile)
  if keyFile |> File.Exists |> not then
    failwith $"No such key-info file: {keyFile}"
  let vaultsFolder = new VaultsFolder(Path.GetDirectoryName(keyFile))
  let kin = KeyInfoName.TryFromFile(keyFile)
  if kin = null then
    failwith $"The name of the key-info file is not in a recognized format."
  let pkif = vaultsFolder.TryReadPassphraseInfo(kin)
  if pkif = null then
    failwith "Key info loading failed"
  cp $"Key: ID \fg{kin.KeyId}\f0 / Kind '\fb{kin.Kind}\f0'"
  let dataFile = Path.GetFullPath(o.DataFile)
  if dataFile |> File.Exists |> not then
    failwith $"No such data file: {dataFile}"
  let dataFolder = Path.GetDirectoryName(dataFile)
  let keysInDataFolder = Directory.GetFiles(dataFolder, "*.key-info")
  if keysInDataFolder.Length > 0 then
    cp "\foWarning! \fyPlaintext files should be in directories separate from encrypted files."
    cp "\fyThe directory containing your data file however has *.key-info files, suggesting that"
    cp "\fyyou are about to violate that principle."
    if o.Force then
      cp "\foContinuing because you passed the \fg-F\f0 flag."
    else
      cp "\frAborting. \foPass \fg-F\fo to bypass this check\f0."
      failwith "Operation refused."
  let unlockCache = new UnlockStore()
  use keyChain = new KeyChain()
  let rawKey = keyChain.FindOrImportKey(kin.KeyId, unlockCache) // no "use" - keyChain takes care of disposal
  let rawKey =
    if rawKey <> null then
      cp "Using \focached unlock key\f0."
      rawKey
    else
      cp $"Key \fg{kin.KeyId}\f0 is not unlocked."
      if kin.Kind <> KeyKind.Passphrase then
        cp $"resolving key kind '{kin.Kind}' is not directly supported yet."
        failwith $"Not supported: resolving locked keys of type '{kin.Kind}'"
      use tmpKey = KeyEntry.enterKey "Enter passphrase" pkif.Salt
      tmpKey |> keyChain.PutCopy
  cp $"Retrieved raw key for key ID \fg{rawKey.GetId()}\f0"

  let dfi = new FileInfo(dataFile)
  let vw = new VaultWriter(VaultFormat.VaultSignatureFile, rawKey, dfi.LastWriteTimeUtc)
  let tmpName = Path.Combine(vaultsFolder.Folder, $"{Guid.NewGuid()}.zvlt.tmp")
  cp $"Storing \fg{Path.GetFileName(dataFile)}\f0 (\fb{dfi.Length}\f0 bytes)"
  let finalGuid =
    use fs = File.Create(tmpName)
    use bw = new BinaryWriter(fs)
    vw.WriteHeader(bw)
    vw.WriteFileNameSegment(bw, Path.GetFileName(dataFile)) |> ignore
    use dfs = File.OpenRead(dataFile)
    vw.WriteContentSegment(bw, dfs)
  cp $"Checksum GUID is \fc{finalGuid}\f0"
  let finalName = Path.Combine(vaultsFolder.Folder, $"{finalGuid}.zvlt")
  File.Move(tmpName, finalName)
  let ffi = new FileInfo(finalName)
  cp $"Finished vault \fG{Path.GetDirectoryName(finalName)}\f0\\\fy{Path.GetFileName(finalName)}\f0 (\fb{ffi.Length}\f0 bytes)"
  0
