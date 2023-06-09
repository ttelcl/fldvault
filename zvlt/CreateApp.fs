﻿module CreateApp

open System
open System.IO

open FldVault.Core
open FldVault.Core.Crypto
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools
open FldVault.Core.KeyResolution

type private KeySource = 
  | AutoKey
  | KeyInfo of string
  | KeyFile of string
  | Passphrase
  | NullKey

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
        failwith $"'-key': duplicate key definition. '-key', '-kf', '-p', '-null' are mutually exclusive."
    | "-kf" :: file :: rest ->
      match o.Source with
      | AutoKey ->
        rest |> parseMore {o with Source = KeySource.KeyFile(file)}
      | _ ->
        failwith $"'-kf': duplicate key definition. '-key', '-kf', '-p', '-null' are mutually exclusive."
    | "-p" :: rest ->
      match o.Source with
      | AutoKey ->
        rest |> parseMore {o with Source = KeySource.Passphrase}
      | _ ->
        failwith $"'-p': duplicate key definition. '-key', '-kf', '-p', '-null' are mutually exclusive."
    | "-null" :: rest ->
      match o.Source with
      | AutoKey ->
        rest |> parseMore {o with Source = KeySource.NullKey}
      | _ ->
        failwith $"'-null': duplicate key definition. '-key', '-kf', '-p', '-null' are mutually exclusive."
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
        let vaultFile0 = Path.Combine(kd, o.VaultName)
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
      | NullKey ->
        let kd =
          match vaultFolderHint with
          | Some(vf) -> vf
          | None -> Environment.CurrentDirectory
        // Before creating the new key we should first verify that the vault we
        // will create does not exist yet. This requires looking ahead a bit
        // and deriving the vault file name early.
        let vaultFile0 = Path.Combine(kd, o.VaultName)
        if File.Exists(vaultFile0) then
          failwith $"The output file already exists: {vaultFile0}"
        // In this case our "key file" is imaginary: it doesn't actually
        // exist, but we can fake a name for it
        let kin = new KeyInfoName(NullKey.NullKeyId, KeyKind.Null)
        let kf = Path.Combine(kd, kin.FileName)
        cp $"Using imaginary key file: \fg{kf}\f0"
        kd, kf
    let vaultFolder =
      match vaultFolderHint with
      | Some(vf) -> vf
      | None -> keyDirectory
    let vaultFile = Path.Combine(vaultFolder, o.VaultName)
    if File.Exists(vaultFile) then
      failwith $"The output file already exists: {vaultFile}"
    cp $"Vault folder: \fc{vaultFolder}\f0"
    cp $"Vault file:   \fg{Path.GetFileName(vaultFile)}\f0"
    cp $"Key folder:   \fc{keyDirectory}\f0"
    cp $"Key file:     \fg{Path.GetFileName(keyFile)}\f0"
    
    match o.Source with
    | KeySource.AutoKey
    | KeySource.KeyFile(_)
    | KeySource.KeyInfo(_) ->
      let pkif = keyFile |> KeyUtilities.getPassKeyInfoFromFile
      cp $"Key:          \fo{pkif.KeyId}\f0 (created \fb{pkif.UtcKeyStamp |> formatStampLocal}\f0)"
      // Note that this function doesn't actually use the key, we just verify
      // that we have it (unless we already implicitly did so by creating it)
      use pk = pkif |> KeyEntry.enterKeyFor
      ()
    | KeySource.Passphrase ->
      ()
    | KeySource.NullKey ->
      ()

    let seedService =
      match o.Source with
      | KeySource.AutoKey
      | KeySource.KeyFile(_)
      | KeySource.KeyInfo(_)
      | KeySource.Passphrase ->
        new PassphraseKeyResolver(null) :> IKeySeedService
      | KeySource.NullKey ->
        new NullSeedService() :> IKeySeedService

    let seed =
      if keyFile.EndsWith(".key-info", StringComparison.InvariantCultureIgnoreCase) then
        keyFile |> seedService.TryCreateFromKeyInfoFile
      elif keyFile.EndsWith(".zvlt", StringComparison.InvariantCultureIgnoreCase) then
        let vf = VaultFile.Open(keyFile)
        vf |> seedService.TryCreateSeedForVault
      else
        failwith $"Unrecognized key provider file '{Path.GetFileName(keyFile)}'"
        
    if seed = null then
      failwith $"Internal error: Failed to create key info seed"

    cp $"Creating new vault file \fg{vaultFile}\f0 using key \fy{seed.KeyId}\f0."
    let _ = VaultFile.OpenOrCreate(vaultFile, seed)
    0
  | _ -> 0
