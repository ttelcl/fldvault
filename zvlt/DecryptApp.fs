module DecryptApp

open System
open System.IO

open FldVault.Core
open FldVault.Core.Crypto
open FldVault.Core.Vaults

open ColorPrint
open CommonTools

type private ExtractOptions = {
  DataFile: string
  OutputFolder: string
}

let runExtract args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-f" :: file :: rest ->
      rest |> parseMore {o with DataFile = file}
    | "-od" :: outDir :: rest ->
      rest |> parseMore {o with OutputFolder = outDir}
    | [] ->
      if o.DataFile |> String.IsNullOrEmpty then
        failwith "No input file specified"
      if o.OutputFolder |> Directory.Exists |> not then
        failwith "Output folder does not exist"
      o
    | x :: _ ->
      failwith $"Unrecognized argument '{x}'"
  let o = args |> parseMore {
    DataFile = null
    OutputFolder = Environment.CurrentDirectory
  }
  let probeName = $"{Guid.NewGuid()}.probe"
  let destProbe = Path.Combine(o.OutputFolder, probeName)
  let srcProbe = Path.Combine(Path.GetDirectoryName(o.DataFile), probeName)
  let sameFolder =
    let empty = Array.Empty<byte>()
    File.WriteAllBytes(destProbe, empty)
    let exists = File.Exists(srcProbe)
    File.Delete(destProbe)
    exists
  if sameFolder then
    failwith $"The destination folder must be different from the source folder"
  failwith "NYI"
  0


