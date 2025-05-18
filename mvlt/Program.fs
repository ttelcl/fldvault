// (c) 2025  ttelcl / ttelcl

open System

open CommonTools
open ColorPrint
open ExceptionTool
open Usage

let rec run arglist =
  // For subcommand based apps, split based on subcommand here
  match arglist with
  | "-v" :: rest ->
    verbose <- true
    rest |> run
  | "--help" :: _
  | "-h" :: _
  | [] ->
    usage ""
    0  // program return status code to the operating system; 0 == "OK"
  | "info" :: rest
  | "i" :: rest ->
    rest |> AppInfo.run
  |"check" :: rest ->
    "-c" :: rest |> AppInfo.run
  | "create" :: rest
  | "c" :: rest ->
    rest |> AppCreate.run
  | "extract" :: rest
  | "x" :: rest ->
    rest |> AppExtract.run
  | atcmd :: rest when atcmd.StartsWith("@") ->
    let cmdFile = atcmd.Substring(1)
    let args2 = CommandFile.expandCommandFile cmdFile
    match args2 with
    | None ->
      usage ""
      1
    | Some(cmdArgs) ->
      let expandedArgs = cmdArgs @ rest
      expandedArgs |> run
  | x :: _ ->
    cp $"\foUnknown command \fy{x}\f0."
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



