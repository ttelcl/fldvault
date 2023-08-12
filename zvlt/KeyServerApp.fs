module KeyServerApp

open System
open System.IO
open System.Runtime.ExceptionServices
open System.Threading

open FldVault.Core
open FldVault.Core.Crypto
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2
open FldVault.KeyServer

open UdSocketLib.Communication
open UdSocketLib.Framing
open UdSocketLib.Framing.Layer1

open ColorPrint
open CommonTools

type private KeyServeOptions = {
  TargetFile: string
}

let resolveKey seedService (kf:string) =
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

let runKeyServe args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      None
    | "-f" :: file :: rest ->
      rest |> parseMore {o with TargetFile = file}
    | file :: rest when file.EndsWith(".zvlt") || file.EndsWith(".key-info") ->
      rest |> parseMore {o with TargetFile = file}
    | [] ->
      if o.TargetFile |> String.IsNullOrEmpty then 
        failwith "No target file specified"
      Some(o)
    | x :: _ ->
      failwith $"Unrecognized argument '{x}'"
  let oo = args |> parseMore {
    TargetFile = null
  }
  match oo with
  | Some(o) ->
    let file = o.TargetFile |> Path.GetFullPath
    if file |> File.Exists then
      use keyChain = new KeyChain()
      let keyServer = new KeyServerService()
      if keyServer.ServerAvailable |> not then
        cp "\frError\f0: \foNo key server found\f0."
        1
      else
        // Do not include the key server as a potential source for the key to upload to itself!
        let seedService = KeyUtilities.setupKeySeedService true true None
        let folder, seed = resolveKey seedService file
        let ok = seed.TryResolveKey(keyChain)
        if ok then
          let uploaded = seed.KeyId |> keyChain.FindDirect |> KeyServer.uploadKeyResync keyServer
          if uploaded then 0 else 1
        else
          cp "Key retrieval failed"
          1
    else
      cp $"\frFile not found\f0: \fo{file}\f0."
      1
  | None ->
    Usage.usage "key-serve"
    0


