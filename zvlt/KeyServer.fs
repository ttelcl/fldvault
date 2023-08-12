module KeyServer

// functionality related to zvlt.exe <-> zvkeyservice communication

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

let createKeyService () =
  let kss = new KeyServerService(null)
  kss

let uploadKeyAsync (kss: KeyServerService) (keyBuffer: KeyBuffer) =
  if kss.SocketPath |> File.Exists |> not then
    failwith "The key server is not running"
  task {
    cp $"Uploading key \fg{keyBuffer.GetId()}\f0 to key server."
    let ct = consoleCancelToken
    let socketService = kss.SocketService
    use! client = socketService.ConnectClientAsync(ct)
    use frameOut = new MessageFrameOut()
    frameOut.WriteKeyUpload(keyBuffer)
    do! client.SendFrameAsync(frameOut, ct)
    use frameIn = new MessageFrameIn()
    let! ok = client.TryFillFrameAsync(frameIn, ct)
    let success = 
      if ok then
        let msgCode = frameIn.MessageCode()
        match msgCode with
        | KeyServerMessages.KeyUploadedCode ->
          true
        | MessageCodes.ErrorText ->
          let ok, message, _ = frameIn.TryReadText()
          if ok then
            cp $"\foServer reported \frerror\f0: \fy{message}\f0."
          else
            cp $"\foServer reported \frerror\f0."
          false
        | x ->
          cp $"\foUnexpected server response\f0: code \fb0x%08X{x}\f0."
          false
      else
        cp "\frNo response from key server\f0."
        false
    return success
  }

let uploadKeyResync (kss: KeyServerService) (keyBuffer: KeyBuffer) =
  let tsk = uploadKeyAsync kss keyBuffer
  tsk.Wait(consoleCancelToken)
  tsk.Result

