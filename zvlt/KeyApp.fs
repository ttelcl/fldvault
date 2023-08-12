module KeyApp

open System
open System.IO

open FldVault.Core
open FldVault.Core.Crypto
open FldVault.Core.KeyResolution
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2
open FldVault.KeyServer

open ColorPrint
open CommonTools

type private KeyOpOptions = {
  KeyTag: string option
  KeyFile: string option
  Folder: string
}

let private parseKeyOptions args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      None
    | "-key" :: keytag :: rest ->
      rest |> parseMore {o with KeyTag = Some(keytag)}
    | "-kf" :: file :: rest ->
      rest |> parseMore {o with KeyFile = Some(file)}
    | file :: rest when file.EndsWith(".key-info") ->
      rest |> parseMore {o with KeyFile = Some(file)}
    | file :: rest when file.EndsWith(".zvlt") ->
      rest |> parseMore {o with KeyFile = Some(file)}
    | "-dv" :: folder :: rest ->
      if folder |> Directory.Exists |> not then
        failwith $"Unknown directory {folder}"
      rest |> parseMore {o with Folder = folder}
    | [] ->
      match o.KeyTag, o.KeyFile with
      | Some(_), None -> ()
      | None, Some(_) -> ()
      | None, None ->
        failwith "Expecting one of -key or -kf"
      | Some(_), Some(_) ->
        failwith "Expecting either -key or -kf, but not both"
      Some(o)
    | x :: _ ->
      failwith $"Unrecognized argument {x}"
  args |> parseMore {
    KeyTag = None
    KeyFile = None
    Folder = "."
  }

let private resolveKeyFile o =
  let vaultsFolder, pkif =
    match o.KeyTag, o.KeyFile with
    | Some(tag), None ->
      let folder = new VaultsFolder(o.Folder)
      let kins = folder.FindKeysByPrefix(tag, null) |> Seq.toArray
      if kins.Length < 1 then
        cp $"No keys matching \fg{tag}\f0 found in \fc{folder.Folder}\f0"
        failwith "No matching keys found"
      if kins.Length > 1 then
        cp $"Ambiguous key: \fb{kins.Length}\f0 keys matching \fg{tag}\f0 found in \fc{folder.Folder}\f0"
        failwith "Too many matching keys found"
      folder, folder.TryReadPassphraseInfo(kins[0])
    | None, Some(kf) ->
      let kf = Path.GetFullPath(Path.Combine(o.Folder, kf))
      let folder = new VaultsFolder(Path.GetDirectoryName(kf))
      if kf.EndsWith(".pass.key-info", StringComparison.InvariantCultureIgnoreCase) then
        let kin = KeyInfoName.TryFromFile(kf);
        if kin = null then
          cpx $"The name of the key file is not in a recognized form: "
          cp $"\fc{Path.GetDirectoryName(kf)}\f0{Path.DirectorySeparatorChar}\fg{Path.GetFileName(kf)}\f0"
          failwith "Key file name is not in a recognized form"
        if File.Exists(kf) |> not then
          cp $"File not found: \fc{Path.GetDirectoryName(kf)}\f0/\fc{Path.GetFileName(kf)}\f0"
          failwith "No such key-info file"
        folder, folder.TryReadPassphraseInfo(kin)
      elif kf.EndsWith(".zvlt", StringComparison.InvariantCultureIgnoreCase) then
        let vf = kf |> VaultFile.Open
        let pkif = vf.GetPassphraseInfo()
        if pkif = null then
          failwith $"No key info found inside vault file {kf}"
        folder, pkif
      else
        failwith $"Unsupported file type for '-kf': {Path.GetFileName(kf)}"
    | None, None ->
      failwith "Expecting one of -key or -kf"
    | Some(_), Some(_) ->
      failwith "Expecting either -key or -kf, but not both"
  vaultsFolder, pkif

let private resolveKey seedService o =
  let vaultsFolder, seed =
    match o.KeyTag, o.KeyFile with
    | Some(tag), None ->
      let folder = new VaultsFolder(o.Folder)
      let kins = folder.FindKeysByPrefix(tag, null) |> Seq.toArray
      if kins.Length < 1 then
        cp $"No keys matching \fg{tag}\f0 found in \fc{folder.Folder}\f0"
        failwith "No matching keys found"
      if kins.Length > 1 then
        cp $"Ambiguous key: \fb{kins.Length}\f0 keys matching \fg{tag}\f0 found in \fc{folder.Folder}\f0"
        failwith "Too many matching keys found"
      let kin = kins[0]
      let kf = Path.Combine(folder.Folder, kin.FileName)
      if File.Exists(kf) |> not then
        cp $"File not found: \fc{Path.GetDirectoryName(kf)}\f0/\fc{Path.GetFileName(kf)}\f0"
        failwith $"Internal error: file not found"
      let seed = kf |> KeyUtilities.trySeedFromKeyInfoFile seedService
      if seed = null then
        failwith $"Failed to load key info from '{kf}'"
      folder, seed
    | None, Some(kf) ->
      let kf = Path.GetFullPath(Path.Combine(o.Folder, kf))
      let folder = new VaultsFolder(Path.GetDirectoryName(kf))
      if kf.EndsWith(".key-info", StringComparison.InvariantCultureIgnoreCase) then
        let kin = KeyInfoName.TryFromFile(kf);
        if kin = null then
          cpx $"The name of the key file is not in a recognized form: "
          cp $"\fc{Path.GetDirectoryName(kf)}\f0{Path.DirectorySeparatorChar}\fg{Path.GetFileName(kf)}\f0"
          failwith "Key file name is not in a recognized form"
        if File.Exists(kf) |> not then
          cp $"File not found: \fc{Path.GetDirectoryName(kf)}\f0/\fc{Path.GetFileName(kf)}\f0"
          failwith "No such key-info file"
        let seed = kf |> KeyUtilities.trySeedFromKeyInfoFile seedService
        if seed = null then
          failwith $"Failed to load key info from '{kf}'"
        folder, seed
      elif kf.EndsWith(".zvlt", StringComparison.InvariantCultureIgnoreCase) then
        let vf = kf |> VaultFile.Open
        let seed = vf |> KeyUtilities.trySeedFromVault seedService
        if seed = null then
          failwith $"No key info found inside vault file {kf}"
        folder, seed
      else
        failwith $"Unsupported file type for '-kf': {Path.GetFileName(kf)}"
    | None, None ->
      failwith "Expecting one of -key or -kf"
    | Some(_), Some(_) ->
      failwith "Expecting either -key or -kf, but not both"
  vaultsFolder, seed

let runCheckKey args =
  let oo = args |> parseKeyOptions
  match oo with
  | Some(o) ->
    let keyServer = new KeyServerService()
    let seedService = KeyUtilities.setupKeySeedService true true (keyServer |> Some)
    let vaultsFolder, seed = o |> resolveKey seedService
    if seed = null then
      failwith "Key info loading failed"
    let unlockCache = UnlockStore.Default
    use keyChain = new KeyChain()
    if seed.KeyId = NullKey.NullKeyId then
      cp $"Key \fb{seed.KeyId}\f0 (the \fonull key\f0)"
    else
      cp $"Key \fg{seed.KeyId}\f0"
    let rawKey = keyChain.FindOrImportKey(seed.KeyId, unlockCache) // no "use" - keyChain takes care of disposal
    let lockStatus = if rawKey <> null then "\foUnlocked\f0" else "\fbLocked\f0"
    cp $"  Lock status: {lockStatus}"
    if seed.KeyId = NullKey.NullKeyId then
      cp $"     Key Kind: \fonull key\f0."
      cp "\fyThe \fonull key\fy is always available and doesn't support any further checking\f0."
      0
    else
      let passphraseSeeds = seed.TryAdapt<PassphraseKeyInfoFile>() |> Seq.toArray
      match passphraseSeeds.Length with
      | 0 ->
        cp "No passphrase entries to validate found for this key."
        0
      | 1 ->
        cp $"     Key Kind: \fgpassphrase based key\f0."
        let pkif = passphraseSeeds[0].KeyDetail
        use keyCheck = KeyEntry.enterKey $"Enter passphrase for key '{pkif.KeyId}'" pkif.Salt
        let guidCheck = keyCheck.GetId()
        let ppStatus = if guidCheck = seed.KeyId then "\fgCorrect\f0" else "\frWrong\f0"
        cp $"  Passphrase status: {ppStatus}"
        if guidCheck = seed.KeyId then 0 else 1
      | n ->
        cp $"\frNot yet supported\f0: multiple (\fb{n}\f0) passphrase based keys were found linked to this key."
        for ppseed in passphraseSeeds do
          let pkif = ppseed.KeyDetail
          cp $"   - \fy{pkif.KeyId}\f0."
        1
  | None ->
    Usage.usage "check"
    0

let runStatusKey args =
  let oo = args |> parseKeyOptions
  match oo with
  | Some(o) ->
    let keyServer = new KeyServerService()
    let seedService = KeyUtilities.setupKeySeedService true true (keyServer |> Some)
    let _, seed = o |> resolveKey seedService
    if seed = null then
      failwith "Key info loading failed"
    let unlockCache = new UnlockStore()
    use keyChain = new KeyChain()
    let rawKey = keyChain.FindOrImportKey(seed.KeyId, unlockCache) // no "use" - keyChain takes care of disposal
    if seed.KeyId = NullKey.NullKeyId then
      cp $"Key \fb{seed.KeyId}\f0 (the \fonull key\f0)"
    else
      cp $"Key \fg{seed.KeyId}\f0"
    let lockStatus = if rawKey <> null then "\foUnlocked\f0" else "\fbLocked\f0"
    cp $"  Lock status: {lockStatus}"
    0
  | None ->
    Usage.usage "status"
    0

let runUnlockKey args =
  let oo = args |> parseKeyOptions
  match oo with
  | Some(o) ->
    let keyServer = new KeyServerService()
    let seedService = KeyUtilities.setupKeySeedService true true (keyServer |> Some)
    let _, seed = o |> resolveKey seedService
    if seed = null then
      failwith "Key info loading failed"
    let unlockCache = new UnlockStore()
    use keyChain = new KeyChain()
    let rawKey = keyChain.FindOrImportKey(seed.KeyId, unlockCache) // no "use" - keyChain takes care of disposal
    let isUnlocked = rawKey <> null
    if seed.KeyId = NullKey.NullKeyId then
      cp $"Key \fb{seed.KeyId}\f0 is the \fonull key\f0, which is always \foUnlocked\f0"
      0
    elif isUnlocked then
      cp $"Key \fg{seed.KeyId}\f0 is already \foUnlocked\f0"
      0
    else
      cp $"Key \fg{seed.KeyId}\f0 is currently \fbLocked\f0"
      let ok = seed.TryResolveKey(keyChain)
      if ok then
        let pk = seed.KeyId |> keyChain.FindDirect
        if pk = null then
          failwith $"Internal error: key unexpectedly missing from key chain"
        unlockCache.StoreKey(pk) |> ignore
        cp $"\foUnlocking\f0 key \fg{seed.KeyId}\f0"
        0
      else
        cp "\frIncorrect key\f0."
        1
  | None ->
    Usage.usage "unlock"
    0

let runLockKey args =
  let oo = args |> parseKeyOptions
  match oo with
  | Some(o) ->
    let keyServer = new KeyServerService()
    let seedService = KeyUtilities.setupKeySeedService true true (keyServer |> Some)
    let _, seed = o |> resolveKey seedService
    if seed = null then
      failwith "Key info loading failed"
    if seed.KeyId = NullKey.NullKeyId then
      cp $"'\fb{seed.KeyId}\f0' is the \fonull key\f0, a special key that is always unlocked (and cannot be locked)"
      1
    else
      let unlockCache = new UnlockStore()
      use keyChain = new KeyChain()
      let rawKey = keyChain.FindOrImportKey(seed.KeyId, unlockCache) // no "use" - keyChain takes care of disposal
      let isUnlocked = rawKey <> null
      if isUnlocked then
        cp $"\fbLocking\f0 key \fg{seed.KeyId}\f0"
        unlockCache.EraseKey(seed.KeyId) |> ignore
        0
      else
        cp $"Key \fg{seed.KeyId}\f0 was already \fbLocked\f0"
        0
  | None ->
    Usage.usage "lock"
    0
