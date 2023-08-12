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

let uploadKeySync (kss: KeyServerService) (keyChain: KeyChain) keyId =
  if kss.SocketPath |> File.Exists |> not then
    failwith "The key server is not running"
  use keyBuffer = keyId |> keyChain.FindCopy
  if keyBuffer = null then
    failwith $"Key not present in the key chain: {keyId}"
  cp $"Uploading key \fg{keyBuffer.GetId()}\f0 to key server."
  let socketService = kss.SocketService
  use frameOut = new MessageFrameOut()
  frameOut.WriteKeyUpload(keyBuffer)
  use client = socketService.ConnectClientSync()
  frameOut |> client.SendFrameSync
  use frameIn = new MessageFrameIn()
  let ok = frameIn |> client.TryFillFrameSync
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

let checkKeyPresences (kss: KeyServerService) keyIds =
  if kss.ServerAvailable |> not then
    failwith "The key server is not running"
  keyIds |> kss.CheckKeyPresenceSync

let checkKeyPresence1 kss keyId =
  let hashset = [| keyId |] |> checkKeyPresences kss
  keyId |> hashset.Contains