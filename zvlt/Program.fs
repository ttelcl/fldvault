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
    usage "all"
    0 
  | [] ->
    usage (if verbose then "all" else "")
    0  // program return status code to the operating system; 0 == "OK"
  | "help" :: command :: rest ->
    usage command
    0
  | "help" :: [] ->
    usage "all"
    0
  | "key" :: "new" :: rest
  | "key-new" :: rest ->
    rest |> KeyApp.runNewKey
  | "create" :: rest ->
    rest |> CreateApp.runCreate
  | "check" :: rest ->
    rest |> KeyApp.runCheckKey
  | "status" :: rest ->
    rest |> KeyApp.runStatusKey
  | "unlock" :: rest ->
    rest |> KeyApp.runUnlockKey
  | "lock" :: rest ->
    rest |> KeyApp.runLockKey
  | "put" :: rest ->
    rest |> EncryptApp.runPut
  | "extract" :: rest ->
    rest |> DecryptApp.runExtract
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



