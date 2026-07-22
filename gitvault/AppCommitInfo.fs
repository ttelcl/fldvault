module AppCommitInfo

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open LibGit2Sharp

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

type private Options = {
  Witness: string
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
      rest |> parseMore {o with Witness = name}
    | [] ->
      o |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  args |> parseMore {
    Witness = Environment.CurrentDirectory
  }

let private runCommitInfo o =
  let repoFolder = o.Witness |> GitRepoFolder.LocateRepoRootFrom
  if repoFolder = null then
    cp $"\frNo git folder found for \fy{o.Witness}\f0."
    1
  else
    cp $"Found repository \fg{repoFolder.AutoRepoName}\f0 (\fk{repoFolder.Folder}\f0)"
    use repo = new Repository(repoFolder.Folder)
    //   Maybe better try this in C#...
    //let referenceMap =
    //  repo.Refs.ToDictionary(r => r.CanonicalName, r => r.ResolveToDirectReference().TargetIdentifier);

    cp "\frWIP\f0."
    1

let run args =
  let oo = args |> parseOptions
  match oo with
  | None ->
    cp ""
    Usage.usage "commitinfo"
    1
  | Some o ->
    o |> runCommitInfo
