// (c) 2023  ttelcl / ttelcl

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
  | "-help" :: _
  | "-h" :: _
  | [] ->
    usage ""
    0  // program return status code to the operating system; 0 == "OK"
  | "serve" :: rest
  | "run" :: rest ->
    rest |> AppServer.runServer
  | "stop" :: rest ->
    rest |> AppServer.runStop
  | x :: _ ->
    cp $"\frUnrecognized command \f0'\fo{x}\f0'."
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



