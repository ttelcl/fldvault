module AppAnchorAdd

open System
open System.IO

open GitVaultLib.Configuration

open ColorPrint
open CommonTools

type private Options = {
  AnchorName: string
  AnchorFolder: string
}

let rec private parseMore o args =
  match args with
  | "-v" :: rest ->
    verbose <- true
    parseMore o rest
  | "--help" :: _ 
  | "-h" :: _ ->
    None
  | "-a" :: name :: rest ->
    if CentralSettings.IsValidName(name, false) |> not then
      cp $"\foAnchor name \fy{name}\fo is not valid\f0."
      None
    else
      rest |> parseMore { o with AnchorName = name }
  | "-f" :: folder :: rest ->
    if folder |> Directory.Exists |> not then
      cp $"\foAnchor folder \fy{folder}\fo does not exist\f0."
      None
    else
      rest |> parseMore { o with AnchorFolder = folder }
  | [] ->
    if o.AnchorName |> String.IsNullOrEmpty then
      cp "\foAnchor name not specified\f0."
      None
    elif o.AnchorFolder |> String.IsNullOrEmpty then
      cp "\foAnchor folder not specified\f0."
      None
    else
      Some o
  | x :: _ ->
    cp $"\foUnknown option \fy{x}\f0."
    None

let private runAnchorAdd o =
  let centralSettings = CentralSettings.Load()
  let error = centralSettings.TryAddAnchor(o.AnchorName, o.AnchorFolder)
  if error |> String.IsNullOrEmpty |> not then
    cp $"\frError\fo: {error}\f0."
    1
  else
    let anchorFolder = centralSettings.Anchors[o.AnchorName]
    cp $"\fgAnchor \fy{o.AnchorName}\f0 added: \fg{anchorFolder}\f0."
    cp "\fgDone\f0."
    0

let run args =
  let oo = args |> parseMore {
    AnchorName = null
    AnchorFolder = null
  }
  match oo with
  | None ->
    Usage.usage "anchor-add"
    1
  | Some o ->
    o |> runAnchorAdd

