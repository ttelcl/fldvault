module AppServer

open System
open System.IO

open FldVault.Core.Crypto
open FldVault.KeyServer

open ColorPrint
open CommonTools

type private ServerOptions = {
  SocketPath: string
  Force: bool
}

let private serve o =
  use keyChain = new KeyChain()
  let service = new KeyServerService(keyChain, o.SocketPath)
  cp $"Starting key server on socket '\fg{service.SocketPath}\f0'"
  failwith "NYI"
  0

let runServer args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _
    | "-help" :: _
    | "--help" :: _ ->
      None
    | "-F" :: rest
    | "-force" :: rest ->
      rest |> parseMore {o with Force = true}
    | "-s" :: path :: rest ->
      rest |> parseMore {o with SocketPath = path}
    | x :: _ ->
      failwith $"Unrecognized argument '{x}'"
    | [] ->
      Some(o)
  let oo = args |> parseMore {
    SocketPath = null
    Force = false
  }
  match oo with
  | Some(o) ->
    o |> serve
  | None ->
    Usage.usage "run"
    0

