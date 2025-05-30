module AppBundlesFetch

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open FileUtilities

open FldVault.KeyServer
open FldVault.Core.Crypto
open FldVault.Core.Mvlt
open FldVault.Core.Vaults

open GitVaultLib.Configuration
open GitVaultLib.GitThings
open GitVaultLib.VaultThings

open ColorPrint
open CommonTools

type private FetchTarget =
  | All
  | CurrentRepo

type private Options = {
  Targets: FetchTarget
}

let run args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      parseMore o rest
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-all" :: rest ->
      rest |> parseMore { o with Targets = All }
    | "-current" :: rest ->
      rest |> parseMore { o with Targets = CurrentRepo }
    | [] ->
      Some o
    | x :: _ ->
      cp $"\frUnknown option: \fy{x}\f0."
      None
  let oo = args |> parseMore { 
    Targets = CurrentRepo
  }
  match oo with
  | None ->
    Usage.usage "bundles-fetch"
    1
  | Some o ->
    cp "\frNot implemented yet\f0."
    Usage.usage "bundles-fetch"
    1
