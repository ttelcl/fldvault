// (c) 2023  ttelcl / ttelcl

open System

open CommonTools
open ExceptionTool
open Usage
open ColorPrint

let rec run arglist =
  // For subcommand based apps, split based on subcommand here
  match arglist with
  | "-v" :: rest ->
    verbose <- true
    rest |> run
  | "--help" :: _
  | "-h" :: _
  | [] ->
    usage verbose
    0  // program return status code to the operating system; 0 == "OK"
  | "key" :: "new" :: rest
  | "key-new" :: rest ->
    rest |> KeyApp.runNewKey
  | "check" :: rest ->
    rest |> KeyApp.runCheckKey
  | "unlock" :: rest ->
    rest |> KeyApp.runUnlockKey
  | "lock" :: rest ->
    rest |> KeyApp.runLockKey
  | x :: _ ->
    cp $"\frUnknown or incomplete command: {x}"
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



