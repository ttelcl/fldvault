module KeyApp

open System
open System.IO

open FldVault.Core
open FldVault.Core.Crypto
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools

type KeyKind =
  | Passphrase

type private NewKeyOptions = {
  Folder: string
  KeyKind: KeyKind option
}

let runNewKey args =
  let rec parsemore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parsemore o
    | "-h" :: _ ->
      None
    | "-p" :: rest ->
      rest |> parsemore {o with KeyKind = Some(KeyKind.Passphrase)}
    | "-dv" :: folder :: rest ->
      if folder |> Directory.Exists |> not then
        failwith $"Unknown directory {folder}"
      rest |> parsemore {o with Folder = folder}
    | [] ->
      if o.KeyKind |> Option.isNone then
        failwith $"No key kind specified. Consider '-p' (for a passphrase based key)"
      Some(o)
    | x :: _ ->
      failwith $"Unrecognized argument: {x}"
  let oo = args |> parsemore {
    Folder = Environment.CurrentDirectory
    KeyKind = None
  }
  match oo with
  | Some(o) ->
    let pkif =
      match o.KeyKind with
      | Some(KeyKind.Passphrase) ->
        use key1 = KeyEntry.enterNewKey "Enter key"
        // cp $"That's key ID \fc{key1.GetId()}\f0."
        use key2 = KeyEntry.enterKey "Reenter key" key1.Salt
        // cp $"That's key ID \fc{key2.GetId()}\f0."
        if key1.GetId() <> key2.GetId() then
          failwith "The keys are different."
        new PassphraseKeyInfoFile(key1)
      | None ->
        failwith "No key kind provided"
    cp $"Initializing \fg{pkif.DefaultFileName}\f0 in \fc{o.Folder}\f0."
    let vaultsFolder = new VaultsFolder(o.Folder)
    let fnm = vaultsFolder.PutKeyInfo(pkif)
    0
  | None ->
    Usage.usage "key-new"
    0

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

let runCheckKey args =
  let oo = args |> parseKeyOptions
  match oo with
  | Some(o) ->
    let vaultsFolder, pkif = o |> resolveKeyFile
    if pkif = null then
      failwith "Key info loading failed"
    let unlockCache = new UnlockStore()
    use keyChain = new KeyChain()
    let rawKey = keyChain.FindOrImportKey(pkif.KeyId, unlockCache) // no "use" - keyChain takes care of disposal
    cp $"Key \fg{pkif.KeyId}\f0"
    let lockStatus = if rawKey <> null then "\foUnlocked\f0" else "\fbLocked\f0"
    cp $"  Lock status: {lockStatus}"
    use keyCheck = KeyEntry.enterKey "Enter passphrase" pkif.Salt
    let guidCheck = keyCheck.GetId()
    let ppStatus = if guidCheck = pkif.KeyId then "\fgCorrect\f0" else "\frWrong\f0"
    cp $"  Passphrase status: {ppStatus}"
    if guidCheck = pkif.KeyId then 0 else 1
  | None ->
    Usage.usage "check"
    0

let runStatusKey args =
  let oo = args |> parseKeyOptions
  match oo with
  | Some(o) ->
    let vaultsFolder, pkif = o |> resolveKeyFile
    if pkif = null then
      failwith "Key info loading failed"
    let unlockCache = new UnlockStore()
    use keyChain = new KeyChain()
    let rawKey = keyChain.FindOrImportKey(pkif.KeyId, unlockCache) // no "use" - keyChain takes care of disposal
    cp $"Key \fg{pkif.KeyId}\f0"
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
    let vaultsFolder, pkif = o |> resolveKeyFile
    if pkif = null then
      failwith "Key info loading failed"
    let unlockCache = new UnlockStore()
    use keyChain = new KeyChain()
    let rawKey = keyChain.FindOrImportKey(pkif.KeyId, unlockCache) // no "use" - keyChain takes care of disposal
    let isUnlocked = rawKey <> null
    if isUnlocked then
      cp $"Key \fg{pkif.KeyId}\f0 is already \foUnlocked\f0"
      0
    else
      cp $"Key \fg{pkif.KeyId}\f0 is currently \fbLocked\f0"
      use pk = KeyEntry.enterKey "Enter passphrase" pkif.Salt
      let guidCheck = pk.GetId()
      if guidCheck <> pkif.KeyId then
        cp "\frIncorrect key\f0."
        1
      else
        keyChain.PutCopy(pk) |> ignore
        unlockCache.StoreKey(pk) |> ignore
        cp $"\foUnlocking\f0 key \fg{pkif.KeyId}\f0"
        0
  | None ->
    Usage.usage "unlock"
    0

let runLockKey args =
  let oo = args |> parseKeyOptions
  match oo with
  | Some(o) ->
    let vaultsFolder, pkif = o |> resolveKeyFile
    if pkif = null then
      failwith "Key info loading failed"
    let unlockCache = new UnlockStore()
    use keyChain = new KeyChain()
    let rawKey = keyChain.FindOrImportKey(pkif.KeyId, unlockCache) // no "use" - keyChain takes care of disposal
    let isUnlocked = rawKey <> null
    if isUnlocked then
      cp $"\fbLocking\f0 key \fg{pkif.KeyId}\f0"
      unlockCache.EraseKey(pkif.KeyId) |> ignore
      0
    else
      cp $"Key \fg{pkif.KeyId}\f0 was already \fbLocked\f0"
      0
  | None ->
    Usage.usage "lock"
    0
