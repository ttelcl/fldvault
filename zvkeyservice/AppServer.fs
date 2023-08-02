module AppServer

open System
open System.IO

open FldVault.Core.Crypto
open FldVault.KeyServer

open UdSocketLib.Communication
open UdSocketLib.Framing
open UdSocketLib.Framing.Layer1

open ColorPrint
open CommonTools
open ExceptionTool


type private ServerOptions = {
  SocketPath: string
  Force: bool
}

let private stamp () =
  let t = DateTimeOffset.Now
  t.ToString("yyyy-MM-dd HH:mm:ss")

let readMessageCode (frameIn:MessageFrameIn) = frameIn.MessageCode()

let private processMessage (service: KeyServerService) frameIn (frameOut: MessageFrameOut) =
  let msgCode = frameIn |> readMessageCode
  match msgCode with
  | _ ->
    cp "\foUnrecognized message\f0."
    frameOut.WriteNoContentMessage(MessageCodes.Unrecognized)

let private listenLoop (service: KeyServerService) =
  let socketService = service.SocketService
  use listener = socketService.StartServer(16)
  let ct = consoleCancelToken
  // We can allocate the buffers here outside the connection flow, because
  // this is a single-connection-at-a-time implementation now.
  use frameIn = new MessageFrameIn()
  use frameOut = new MessageFrameOut()
  cp "\fyPressing \frCTRL-C\fy aborts the server."
  let oneClientCycleTask () =
    task {
      cp "\fkWaiting for connection\f0."
      use! connection = ct |> listener.AcceptAsync
      cpx $"\fk{stamp()}\f0 Connected.  "
      let! ok = connection.TryFillFrameAsync(frameIn, ct)
      if ok then
        let msgId = frameIn |> readMessageCode
        cp $"Received message \fc%08X{msgId}\f0."
        try
          processMessage service frameIn frameOut
        with
        | ex ->
          ex |> fancyExceptionPrint verbose
          resetColor ()
          cp "  ... \fmSending exception message as reply\f0."
          ex |> frameOut.WriteErrorResponse
        do! connection.SendFrameAsync(frameOut, ct)
      else
        cp $"\frReceive error\f0. \foAborting this connection\f0."
    }
  while canceled() |> not do
    let tsk = oneClientCycleTask()
    tsk.Wait(ct)
    ()
  0

let private serveApp o =
  use keyChain = new KeyChain()
  let service = new KeyServerService(keyChain, o.SocketPath)
  cp $"Starting key server on socket '\fg{service.SocketPath}\f0'"

  let canStart =
    if service.SocketPath |> File.Exists then
      cp $"Key server socket seems to be in use already '\fr{service.SocketPath}\f0'"
      if o.Force then
        cp "\fg-F\fy functionality is not yet implemented\f0."
      false
    else
      true
  if canStart then  
    cp $"Starting key server on socket '\fg{service.SocketPath}\f0'"
    service |> listenLoop
  else
    cp "\frServer startup aborted\f0."
    1

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
    o |> serveApp
  | None ->
    Usage.usage "run"
    0

