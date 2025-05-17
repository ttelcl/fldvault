// (c) 2023  ttelcl / ttelcl

open System

open CommonTools
open ExceptionTool
open Usage
open ColorPrint

let rec run arglist =
  match arglist with
  | "-v" :: rest ->
    verbose <- true
    rest |> run
  | "--help" :: _
  | "-h" :: _  ->
    usage "all-deprecated"
    0 
  | [] ->
    usage (if verbose then "all" else "all-summary")
    0  // program return status code to the operating system; 0 == "OK"
  | "help" :: command :: rest ->
    usage command
    0
  | "help" :: [] ->
    usage (if verbose then "all" else "all-deprecated")
    0
  | "key" :: "new" :: rest
  | "key-new" :: rest ->
    rest |> NewKeyApp.runNewKey
  | "serve" :: rest
  | "key" :: "serve" :: rest
  | "upload" :: rest
  | "key" :: "upload" :: rest
  | "key-serve" :: rest ->
    rest |> KeyServerApp.runKeyServe
  | "create" :: rest ->
    rest |> CreateApp.runCreate
  | "list" :: rest ->
    rest |> ListApp.runList
  | "dump" :: rest ->
    rest |> DumpApp.runDump
  | "append" :: rest ->
    rest |> AppendApp.runAppend
  | "check" :: rest ->
    rest |> KeyApp.runCheckKey
  | "status" :: rest ->
    rest |> KeyApp.runStatusKey
  | "unlock" :: rest ->
    rest |> KeyApp.runUnlockKey
  | "lock" :: rest ->
    rest |> KeyApp.runLockKey
  | "extract" :: rest ->
    rest |> ExtractApp.runExtract
  | "register" :: rest ->
    rest |> FileRegisterApp.run
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



