module ZkeyApp

open System
open System.IO

open FldVault.Core.Vaults

open ColorPrint
open CommonTools

type private NameSource =
  | FromId
  | FromSource

type private Options = {
  InputFile: string
  NamingSource: NameSource Option
}

let run args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      None
    | "-id" :: rest ->
      rest |> parseMore { o with NamingSource = FromId |> Some }
    | "-src" :: rest
    | "-source" :: rest ->
      rest |> parseMore { o with NamingSource = FromSource |> Some }
    | "-f" :: fileName :: rest ->
      rest |> parseMore { o with InputFile = fileName }
    | [] ->
      if o.InputFile |> String.IsNullOrEmpty then
        cp "\frError\fo: No input file specified\f0."
        None
      elif o.InputFile |> File.Exists |> not then
        cp $"\frError\fo: Input file \fy{o.InputFile}\fo does not exist\f0."
        None
      elif o.NamingSource.IsNone then
        cp "\frError\fo: No naming source specified \f0(\fg-id\f0 or \fg-src\f0)."
        None
      else
        Some o
    | x :: _ ->
      cp $"\frError\fo Unknown option: \fc{x}\f0."
      None
  let oo = args |> parseMore {
    InputFile = null
    NamingSource = None
  }
  match oo with
  | None ->
    Usage.usage "showkey"
    1
  | Some o ->
    let pkif = o.InputFile |> PassphraseKeyInfoFile.TryFromFile
    if pkif = null then
      cp $"\fo: Could not get key from \fy{o.InputFile}\f0 (unsupported format)."
      1
    else
      let zkey = Zkey.FromPassphraseKeyInfoFile(pkif)
      let nameTag =
        match o.NamingSource with
        | Some FromId -> zkey.KeyId
        | Some FromSource -> Path.GetFileNameWithoutExtension(o.InputFile)
        | None -> failwith "Internal error"
      let fileName = $"{nameTag}.zkey"
      if File.Exists(fileName) then
        cp $"\foFile \fy{fileName}\fo already exists."
        1
      else
        cp $"Saving \fg{fileName}\f0."
        let json = zkey.ToString(true)
        File.WriteAllText(fileName, json)
        0

