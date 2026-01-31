module AppDelta

open System
open System.IO

open Newtonsoft.Json

open FileUtilities

open FldVault.KeyServer
open FldVault.Core.Crypto
open FldVault.Core.Mvlt
open FldVault.Core.Vaults

open GitVaultLib.Configuration
open GitVaultLib.Delta
open GitVaultLib.GitThings
open GitVaultLib.VaultThings

open ColorPrint
open CommonTools

let private runDeltaNew args =
  cp "\frNYI\f0."
  1

let private runDeltaEdit args =
  cp "\frNYI\f0."
  1

let private runDeltaDrop args =
  cp "\frNYI\f0."
  1

let private runDeltaDefault args =
  cp "\frNYI\f0."
  1

let private runDeltaSend args =
  cp "\frNYI\f0."
  1

let run args =
  match args with
  | "-h" :: _
  | [] ->
    Usage.usage "delta"
    1
  | "new" :: rest -> rest |> runDeltaNew
  | "edit" :: rest -> rest |> runDeltaEdit
  | "drop" :: rest -> rest |> runDeltaDrop
  | "default" :: rest -> rest |> runDeltaDefault
  | "send" :: rest -> rest |> runDeltaSend
  | x :: _ ->
    cp $"\frUnknown delta subcommand \f0'\fo{x}\f0'"
    Usage.usage "delta"
    1
