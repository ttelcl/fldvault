module FileRegisterApp

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

type private RegisterFileOptions = {
  TargetFile: string
  SocketName: string
}

let run args =
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
    SocketName = null
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
        try
          let registration = keyServer.RegisterFileSync(file, keyChain) |> Option.ofNullable
          match registration with
          | Some(keyId) ->
            cp $"\fGThe key is already known at the server\f0 (\fg{keyId}\f0)."
          | None ->
            cp "\fgRegistered the file with the server\f0. The key is \fynot\f0 yet available."
          0
        with
          | ex ->
            cp $"\frFailed\f0: \fy{ex.Message}\f0."
            1
    else
      cp $"\frFile not found\f0: \fo{file}\f0."
      1
  | None ->
    Usage.usage "register"
    0
