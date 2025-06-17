module AppBundlesConnect

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

type private ConnectTarget =
  | AnchorHost of string * string
  | AnchorAll of string
  | All

type private Options = {
  Target: ConnectTarget option
}

let private runConnect o =
  let centralSettings = CentralSettings.Load()
  let repo, repoSettings =
    let repo = GitRepoFolder.LocateRepoRootFrom(Environment.CurrentDirectory)
    if repo = null then
      cp "\foExpecting this to be called from within a git repository\f0."
      null, null
    else
      let settings = repo |> RepoSettings.TryLoad
      if settings = null then
        cp "\foThis git repository has not been initialized for use with gitvault yet\f0."
        cp "Call \fogitvault repo init\f0 to initialize it first."
        null, null
      else
        repo, settings
  if repo = null || repoSettings = null then
    1
  else
    cp "NYI"
    1

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
      rest |> parseMore {o with Target = ConnectTarget.All |> Some}
    | "-a" :: spec :: rest ->
      let parts = spec.Split('.')
      if parts.Length = 1 then
        let anchor = parts[0]
        if anchor |> CentralSettings.IsValidAnchor |> not then
          cp $"\fo'\fc{anchor}\fo' is not a valid anchor name\f0."
          None
        else
          rest |> parseMore {o with Target = ConnectTarget.AnchorAll(anchor) |> Some}
      elif parts.Length = 2 then
        let anchor = parts[0]
        let host = parts[1]
        if anchor |> CentralSettings.IsValidAnchor |> not then
          cp $"\fo'\fc{anchor}\fo' is not a valid anchor name\f0."
          None
        elif host |> CentralSettings.IsValidHost |> not then
          cp $"\fo'\fc{host}\fo' is not a valid 'host name'\f0."
          None
        else
          rest |> parseMore {o with Target = ConnectTarget.AnchorHost(anchor, host) |> Some}
      elif parts.Length = 0 then
        cp $"\foEmpty argument to \fg-a\f0."
        None
      else
        cp $"\foToo many '.' characters in \fg-a \fc{spec}\f0 (expecting one or none)"
        None
    | [] ->
      if o.Target.IsNone then
        cp "\foNo target specified\f0."
        None
      else
        o |> Some
    | x :: _ ->
      cp $"\frUnknown option: \fy{x}\f0."
      None
  let oo = args |> parseMore {
    Target = None
  }
  match oo with
  | None ->
    Usage.usage "bundles-connect"
    1
  | Some(o) ->
    o |> runConnect
