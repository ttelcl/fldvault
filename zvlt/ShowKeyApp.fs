module ShowKeyApp

open System
open System.IO

open FldVault.Core.Vaults

open ColorPrint
open CommonTools

type private Options = {
  InputFile: string
  IncludePassphrase: bool
}

let run args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      None
    | "-p" :: rest ->
      rest |> parseMore { o with IncludePassphrase = true }
    | "-f" :: fileName :: rest ->
      rest |> parseMore { o with InputFile = fileName }
    | [] ->
      if o.InputFile |> String.IsNullOrEmpty then
        cp "\frError\fo: No input file specified\f0."
        None
      elif o.InputFile |> File.Exists |> not then
        cp $"\frError\fo: Input file \fy{o.InputFile}\fo does not exist\f0."
        None
      else
        Some o
    | x :: _ ->
      cp $"\frError\fo Unknown option: \fc{x}\f0."
      None
  let oo = args |> parseMore {
    InputFile = null
    IncludePassphrase = false
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
      let info = zkey.ToZkeyTransferString(o.IncludePassphrase)
      cp $"Key used by \fc{o.InputFile}\f0:"
      let color = if o.IncludePassphrase then "\fy" else "\fg"
      cp $"{color}{info}\f0"
      0
