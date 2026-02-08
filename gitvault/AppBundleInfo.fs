module AppBundleInfo

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
open GitVaultLib.Delta
open GitVaultLib.GitThings
open GitVaultLib.VaultThings

open ColorPrint
open CommonTools

type private BundleSource =
  | BundleFile of string

type private BundleInfoOptions = {
  Source: BundleSource option
}

let private parseOptions args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      parseMore o rest
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-f" :: name :: rest ->
      rest |> parseMore {o with Source = name |> BundleSource.BundleFile |> Some}
    | [] ->
      if o.Source.IsNone then
        cp "\foMissing bundle source option\f0."
        None
      else
        o |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  args |> parseMore {
    Source = None
  }

let private runBundleInfo o =
  let filename =
    match o.Source with
    | Some(BundleFile(filename)) ->
      filename |> Some
    | _ ->
      cp "\frNot found or not yet implemented\f0."
      None
  match filename with
  | Some filename ->
    let bundleHeader = filename |> BundleHeader.FromFile
    //for line in filename |> BundleHeader.ReadHeaderLines do
    //  cp $"  \fg{line}\f0."
    let json = JsonConvert.SerializeObject(bundleHeader, Formatting.Indented)
    cp "\f0--------------"
    cp $"\fg{json}"
    cp "\f0--------------"
    0
  | None ->
    // message was printed already
    1

let run args =
  let oo = args |> parseOptions
  match oo with
  | None ->
    cp ""
    Usage.usage "bundle"
    1
  | Some o ->
    o |> runBundleInfo
