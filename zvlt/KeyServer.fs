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

let uploadKey (keyBuffer: KeyBuffer) =
  failwith "NYI"
  ()
