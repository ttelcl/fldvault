module CreateApp

open System
open System.IO

open FldVault.Core
open FldVault.Core.Crypto
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools

type private KeySource = 
  | AutoKey
  | KeyInfo of string
  | KeyFile of string
  | Passphrase

type private CreateOptions = {
  VaultName: string
  Source: KeySource
}

let formatStampLocal (t: DateTime) =
  t.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")

let private findSoleKey vaultFolder =
  let di = new DirectoryInfo(vaultFolder)
  let keyFiles =
    di.GetFiles("*.key-info")
  if keyFiles.Length = 1 then
    keyFiles[0].FullName
  else
    if keyFiles.Length = 0 then
      failwith $"No *.key-info files found in '{vaultFolder}'"
    else
      cp "\foMultiple keys found.\f0 Use \fg-key\f0 to select one:"
      for fi in keyFiles do
        let kin = KeyInfoName.FromFile(fi.FullName)
        let stamp = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
        cp $"  \fc{kin.KeyId}\f0.{kin.FileSuffix}  \fb{stamp}\f0"
      failwith $"Ambiguous key: multiple *.key-info files found in '{vaultFolder}'."

let private findTaggedKey (tag: string) vaultFolder =
  let di = new DirectoryInfo(vaultFolder)
  let keyFiles =
    di.GetFiles("*.key-info")
    |> Array.filter(fun fi -> fi.Name.StartsWith(tag))
  if keyFiles.Length = 1 then
    keyFiles[0].FullName
  else
    if keyFiles.Length = 0 then
      failwith $"No *.key-info files matching '{tag}' found in '{vaultFolder}'"
    else
      cp "\foMultiple matching keys found.\f0 Use \fg-key\f0 to select a more specific one:"
      for fi in keyFiles do
        let kin = KeyInfoName.FromFile(fi.FullName)
        let stamp = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
        cp $"  \fc{kin.KeyId}\f0.{kin.FileSuffix}  \fb{stamp}\f0"
      failwith $"Ambiguous key: multiple *.key-info files matching '{tag}' found in '{vaultFolder}'."

let runCreate args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      Usage.usage "create"
      None
    | "-vf" :: file :: rest ->
      if file.EndsWith(".zvlt") |> not then
        failwith $"Expecting vault file name to have the extension '.zvlt'"
      rest |> parseMore {o with VaultName = file}
    | file :: rest when file.EndsWith(".zvlt") ->
      rest |> parseMore {o with VaultName = file}
    | "-key" :: keytag :: rest ->
      match o.Source with
      | AutoKey ->
        rest |> parseMore {o with Source = KeySource.KeyInfo(keytag)}
      | _ ->
        failwith $"'-key': duplicate key definition. '-key', '-kf', '-p' are mutually exclusive."
    | "-kf" :: file :: rest ->
      match o.Source with
      | AutoKey ->
        rest |> parseMore {o with Source = KeySource.KeyFile(file)}
      | _ ->
        failwith $"'-kf': duplicate key definition. '-key', '-kf', '-p' are mutually exclusive."
    | "-p" :: rest ->
      match o.Source with
      | AutoKey ->
        rest |> parseMore {o with Source = KeySource.Passphrase}
      | _ ->
        failwith $"'-p': duplicate key definition. '-key', '-kf', '-p' are mutually exclusive."
    | [] ->
      if o.VaultName |> String.IsNullOrEmpty then
        failwith "No vault name specified"
      Some(o)
    | x :: _ ->
      failwith $"Unrecognized argument '%s{x}'"
  let oo = args |> parseMore {
    VaultName = null
    Source = KeySource.AutoKey
  }
  match oo with
  | Some(o) ->
    let o, vaultFolderHint =
      if o.VaultName.IndexOfAny([| '/'; '\\' |]) >= 0 then
        // vault name is treated as fully specified, absolute path, if necessary
        // resolved relative to CWD
        let vaultName = Path.GetFullPath(o.VaultName)
        {o with VaultName = vaultName}, Some(Path.GetDirectoryName(vaultName))
      else
        // vault name is a simple pathless file name, to be resolved relative to the
        // key source.
        o, None
    let keyDirectory, keyFile =
      match o.Source with
      | AutoKey ->
        let kd =
          match vaultFolderHint with
          | Some(vf) -> vf
          | None -> Environment.CurrentDirectory
        let kf = kd |> findSoleKey
        kd, kf
      | KeyInfo(ki)->
        let kd =
          match vaultFolderHint with
          | Some(vf) -> vf
          | None -> Environment.CurrentDirectory
        let kf = kd |> findTaggedKey ki
        kd, kf
      | KeyFile(kf) ->
        match vaultFolderHint with
        | Some(vf) ->
          let kf1 = Path.Combine(vf, kf)
          let kf2 = Path.Combine(Environment.CurrentDirectory, kf)
          // Resolve '-kf' value relative to vault folder or current folder,
          // whichever exists (preferring vault folder)
          let kfex =
            if File.Exists(kf1) then
              kf1
            elif File.Exists(kf2) then
              kf2
            else
              if kf1 = kf2 then 
                failwith $"Cannot find key file '{kf1}'"
              else
                failwith $"Cannot find either key file candidate '{kf1}' nor '{kf2}'"
          let kd = Path.GetDirectoryName(kfex)
          kd, kfex
        | None ->
          let kfex = Path.GetFullPath(kf)
          let kd = Path.GetDirectoryName(kfex)
          kd, kfex
      | Passphrase ->
        let kd =
          match vaultFolderHint with
          | Some(vf) -> vf
          | None -> Environment.CurrentDirectory
        // Before creating the new key we should first verify that the vault we
        // will create does not exist yet. This requires looking ahead a bit
        // and deriving the vault file name early.
        let vaultFolder0 =
          match vaultFolderHint with
          | Some(vf) -> vf
          | None -> kd
        let vaultFile0 = Path.Combine(vaultFolder0, o.VaultName)
        if File.Exists(vaultFile0) then
          failwith $"The output file already exists: {vaultFile0}"
        // Now create the new key
        let pk1 = KeyEntry.enterNewKey "Enter a passphrase for the new vault key"
        let pk2 = KeyEntry.enterKey "Re-enter the passphrase" pk1.Salt
        if pk1.GetId() <> pk2.GetId() then
          failwith "The passphrases did not match"
        let pkif = new PassphraseKeyInfoFile(pk1)
        let kf = pkif.WriteToFolder(kd)
        cp $"Created new key info file: \fg{kf}\f0"
        kd, kf
    let vaultFolder =
      match vaultFolderHint with
      | Some(vf) -> vf
      | None -> keyDirectory
    let vaultFile = Path.Combine(vaultFolder, o.VaultName)
    if File.Exists(vaultFile) then
      failwith $"The output file already exists: {vaultFile}"
    let pkif = keyFile |> KeyUtilities.getPassKeyInfoFromFile
    cp $"Key folder:   \fc{keyDirectory}\f0"
    cp $"Key file:     \fg{Path.GetFileName(keyFile)}\f0"
    cp $"Vault folder: \fc{vaultFolder}\f0"
    cp $"Vault file:   \fg{Path.GetFileName(vaultFile)}\f0"
    cp $"Key:          \fo{pkif.KeyId}\f0 (created \fb{pkif.UtcKeyStamp |> formatStampLocal}\f0)"
    
    if o.Source <> KeySource.Passphrase then
      // Note that this function doesn't actually use the key, we just verify
      // that we have it (unless we already did so by creating it)
      use pk = pkif |> KeyEntry.enterKeyFor
      ()
    cp $"Creating new vault file \fg{vaultFile}\f0 using key \fy{pkif.KeyId}\f0."
    let newvault = VaultFile.OpenOrCreate(vaultFile, pkif)
    0
  | _ -> 0
