module AppServer

open System
open System.IO
open System.Runtime.ExceptionServices
open System.Threading

open FldVault.Core.Crypto
open FldVault.KeyServer

open UdSocketLib.Communication
open UdSocketLib.Framing
open UdSocketLib.Framing.Layer1

open ColorPrint
open CommonTools
open ExceptionTool

// https://stackoverflow.com/a/72132958/271323
let inline reraiseAnywhere<'a> (e: exn) : 'a =
    ExceptionDispatchInfo.Capture(e).Throw()
    Unchecked.defaultof<'a>

type private ServerOptions = {
  SocketPath: string
  Force: bool
}

let private stamp () =
  let t = DateTimeOffset.Now
  t.ToString("yyyy-MM-dd HH:mm:ss")

let readMessageCode (frameIn:MessageFrameIn) = frameIn.MessageCode()

let private processMessage (keyChain: KeyChain) frameIn (frameOut: MessageFrameOut) =
  let msgCode = frameIn |> readMessageCode
  match msgCode with
  | KeyServerMessages.KeyUploadCode ->
    let keyId = frameIn.ReadKeyUpload keyChain
    cp $"Key upload imported: \fg{keyId}\f0."
    frameOut.WriteNoContentMessage(KeyServerMessages.KeyUploadedCode)
  | KeyServerMessages.KeyRemoveCode ->
    let keyId = frameIn.ReadKeyRemove()
    let deleted = keyId |> keyChain.Delete
    if deleted then
      cp $"\foKey to delete was not found\f0: \fy{keyId}\f0."
      frameOut.WriteNoContentMessage(KeyServerMessages.KeyNotFoundCode)
    else
      cp $"Key deleted: \fr{keyId}\f0."
      frameOut.WriteNoContentMessage(KeyServerMessages.KeyRemovedCode)
  | KeyServerMessages.KeyRequestCode ->
    let keyId = frameIn.ReadKeyRequest()
    let key = keyChain.FindDirect(keyId)
    if key = null then
      cp $"\foKey not found\f0: \fy{keyId}\f0."
      frameOut.WriteNoContentMessage(KeyServerMessages.KeyNotFoundCode)
    else
      cp $"Key found \fg{keyId}\f0."
      key |> frameOut.WriteKeyResponse
  | KeyServerMessages.KeyPresenceListCode ->
    let keysRequested = frameIn.ReadKeyPresence()
    let keysFound = keysRequested |> Seq.filter keyChain.ContainsKey |> Seq.toArray
    cp $"Key presence check: \fg{keysFound.Length}\f0 of \fb{keysRequested.Count}\f0 are present."
    frameOut.WriteKeyPresence(keysFound)
  | _ ->
    cp $"\foUnrecognized message \fc0x%08X{msgCode}\f0."
    frameOut.WriteNoContentMessage(MessageCodes.Unrecognized)

let private serveOneClientAsync (keyChain: KeyChain) (connection: UdSocketServer) (ct: CancellationToken) =
  task {
    // This version allocates new frames for each request
    use frameIn = new MessageFrameIn()
    use frameOut = new MessageFrameOut()
    cpx $"\fk{stamp()}\f0 Connected.  "
    let! ok = connection.TryFillFrameAsync(frameIn, ct)
    if ok && not(ct.IsCancellationRequested) then
      let msgId = frameIn |> readMessageCode
      cp $"Received message \fc%08X{msgId}\f0."
      try
        processMessage keyChain frameIn frameOut
      with
      | :? OperationCanceledException as oce ->
        cp "\frCanceled\f0!"
        // Cannot use "reraise()" in a task!
        // see https://stackoverflow.com/a/72132958/271323
        return (reraiseAnywhere oce)
      | ex ->
        ex |> fancyExceptionPrint verbose
        resetColor ()
        cp "  ... \fmSending exception message as reply\f0."
        ex |> frameOut.WriteErrorResponse
      do! connection.SendFrameAsync(frameOut, ct)
    else
      cp $"\frReceive error\f0. \foAborting this connection\f0."
  }

let private serverloopAsync (service: KeyServerService) (keyChain: KeyChain) (ct: CancellationToken) =
  task {
    let socketService = service.SocketService
    use listener = socketService.StartServer(16)
    cp "\fyPressing \frCTRL-C\fy aborts the server."
    while ct.IsCancellationRequested |> not do
      cp "\fkWaiting for connection\f0."
      use! connection = ct |> listener.AcceptAsync
      do! serveOneClientAsync keyChain connection ct
      ()
  }
  
let private serveApp o =
  use keyChain = new KeyChain()
  let service = new KeyServerService(o.SocketPath)
  let canStart =
    if service.SocketPath |> File.Exists then
      cp $"\frKey server socket seems to be in use already\f0"
      cp $"  (\fo{service.SocketPath}\f0)"
      if o.Force then
        cp "\fg-F\fy functionality is not yet implemented\f0."
      false
    else
      true
  if canStart then  
    cp $"Starting key server on socket '\fg{service.SocketPath}\f0'"
    // service |> listenLoop
    let loopTask = serverloopAsync service keyChain CommonTools.consoleCancelToken
    try
      loopTask.Wait()
    with
    | :? AggregateException as ae ->
      // Because we stop by triggering cancellation, waiting for the result is
      // ugly. This "let the cancel exception come and catch it" method
      // actually is relatively not-ugly ...
      cp $"\fyStopped\f0!"
    cp $"Exit stats: there were %d{keyChain.KeyCount} keys in the server."
    let fingerprints =
      keyChain.EnumerateFingerprints()
      |> Seq.toArray
      |> Array.sort
    for fingerprint in fingerprints do
      if fingerprint = "ad7a6866-62f8" then
        cp $"  \fC{fingerprint}-...\f0  (null key)."
      else
        cp $"  \fG{fingerprint}-...\f0."
    0
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

