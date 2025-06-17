// (c) 2025  ttelcl / ttelcl

open System

open CommonTools
open ColorPrint
open ExceptionTool

let rec run arglist =
  // For subcommand based apps, split based on subcommand here
  match arglist with
  | "-v" :: rest ->
    verbose <- true
    rest |> run
  | [] ->
    Usage.usage ""
    0  // program return status code to the operating system; 0 == "OK"
  | "--help" :: _
  | "-help" :: _
  | "help" :: _
  | "-h" :: _ ->
    Usage.usage "all"
    0  // program return status code to the operating system; 0 == "OK"
  | "settings" :: "show" :: rest
  | "settings" :: rest ->
    rest |> AppSettingsShow.run
  | "repo" :: "init" :: rest ->
    rest |> AppRepoInit.run
  | "anchor" :: "add" :: rest ->
    rest |> AppAnchorAdd.run
  | "send" :: rest
  | "repo" :: "send" :: rest ->
    rest |> AppPush.run
  | "ingest" :: rest 
  | "bundles" :: "ingest" :: rest ->
    rest |> AppBundlesFetch.run
  | "bundles" :: "status" :: rest 
  | "status" :: rest 
  | "bundles" :: "info" :: rest ->
    rest |> AppBundlesStatus.run
  | "connect" :: rest ->
    rest |> AppBundlesConnect.run
  | x :: _ ->
    cp $"\frUnknown command:\f0 '\fy{x}\f0'"
    Usage.usage ""
    1

[<EntryPoint>]
let main args =
  try
    args |> Array.toList |> run
  with
  | ex ->
    ex |> fancyExceptionPrint verbose
    resetColor ()
    1



