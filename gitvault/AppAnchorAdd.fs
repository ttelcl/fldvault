module AppAnchorAdd

open System
open System.IO

open GitVaultLib.Configuration

open ColorPrint
open CommonTools

type private Options = {
  AnchorName: string // can be null or empty to have it derived from the folder
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
    if CentralSettings.IsValidAnchor(name) |> not then
      cp $"\foAnchor name \fy{name}\fo is not valid\f0."
      None
    else
      rest |> parseMore { o with AnchorName = name }
  | "-f" :: folder :: rest ->
    let folder = (folder |> Path.GetFullPath).TrimEnd('/', '\\')
    // Let library code handle anchor name generation
    rest |> parseMore { o with AnchorFolder = folder }
  | [] ->
    if o.AnchorFolder |> String.IsNullOrEmpty then
      cp "\foAnchor folder not specified\f0."
      None
    elif o.AnchorName |> String.IsNullOrEmpty then
      cp "\foAnchor name not specified\f0 - \fyIt will be derived from the folder (if possible)\f0."
      Some o
    else
      Some o
  | x :: _ ->
    cp $"\foUnknown option \fy{x}\f0."
    None

let private runAnchorAdd o =
  let centralSettings = CentralSettings.Load()
  let error, entry = centralSettings.TryAddAnchor(o.AnchorName, o.AnchorFolder)
  if error |> String.IsNullOrEmpty |> not then
    cp $"\frError\fo: {error}\f0."
    1
  else
    let entry = entry.Value
    cp $"Anchor '\fc{entry.Key}\f0' added: \fg{entry.Value}\f0."
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

