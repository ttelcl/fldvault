﻿module AppendApp

open System
open System.IO

open FldVault.Core
open FldVault.Core.BlockFiles
open FldVault.Core.Crypto
open FldVault.Core.Utilities
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools

type HashSet<'a> = System.Collections.Generic.HashSet<'a>

type private PathFile = {
  // command line parsing focused representation of a file to append
  Path: string
  File: string
}

type private AppendOptions = {
  VaultFile: string
  Path: string
  Files: PathFile list
}

type private FileTarget = {
  // execution focused representation of a file to append
  Label: string
  Source: string
}

let private pathFileToFileTarget pf = 
  let shortName = Path.GetFileName(pf.File)
  let prefix = if pf.Path |> String.IsNullOrEmpty then "" else $"{pf.Path}/"
  {
    Label = prefix + shortName
    Source = Path.GetFullPath(pf.File)
  }

let runAppend args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      None
    | [] ->
      if o.VaultFile |> String.IsNullOrEmpty then
        failwith "No target vault specified"
      if o.Files |> List.isEmpty then
        failwith "No files to add specified"
      Some({o with Files = o.Files |> List.rev})
    | "-vf" :: vault :: rest ->
      rest |> parseMore {o with VaultFile = vault}
    | "-p" :: path :: rest ->
      let path = if path = "." then "" else path
      let path = path.Replace('\\', '/')
      let badIdx = path.IndexOfAny([| ':'; ';'; '|'; '<'; '>'; '*'; '?' |])
      if badIdx >= 0 then
        failwith $"Invalid character in path '{path}': '{path[badIdx]}'"
      let segments = 
        path.Split('/') // silently remove empty segments (including leading or trailing '/')
        |> Array.where (fun segment -> segment |> String.IsNullOrEmpty |> not)
      if segments |> Seq.contains ".." then
        failwith $"'{path}': virtual paths must not contain '..' segments"
      rest |> parseMore {o with Path = String.Join("/", segments)}
    | "-f" :: file :: rest ->
      let pf = {Path = o.Path; File = file}
      rest |> parseMore {o with Files = pf :: o.Files}
    | x :: _ ->
      failwith $"Unrecognized argument '{x}'"
  let oo = args |> parseMore {
    VaultFile = null
    Path = ""
    Files = []
  }
  match oo with
  | Some(o) ->
    let vaultFile = VaultFile.Open(o.VaultFile)
    let pkif = vaultFile.GetPassphraseInfo()
    if pkif = null then
      failwith "No key information found in the vault"
    let unlockCache = new UnlockStore()
    use keyChain = new KeyChain()
    let _ =
      let rawKey = keyChain.FindOrImportKey(pkif.KeyId, unlockCache)
      if rawKey = null then
        cp $"Key \fy{pkif.KeyId}\f0 is \folocked\f0."
        use k = pkif |> KeyEntry.enterKeyFor :> KeyBuffer
        k |> keyChain.PutCopy
      else
        cp $"Key \fy{pkif.KeyId}\f0 is \fcunlocked\f0."
        rawKey
    use cryptor = vaultFile.CreateCryptor(keyChain)
    let targets = o.Files |> List.map pathFileToFileTarget
    let alreadyAdded =
      let existingMetadata =
        use reader = new VaultFileReader(vaultFile, cryptor)
        vaultFile.FileElements()
        |> Seq.map (fun ibe -> new FileElement(ibe))
        |> Seq.map (fun fe -> fe.GetMetadata(reader))
        |> Seq.toArray
      let existingNames =
        existingMetadata
        |> Seq.map (fun meta -> meta.Name)
        |> Seq.where (fun name -> name |> String.IsNullOrEmpty |> not)
        |> Seq.toArray
      let existingNameSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
      existingNames |> Seq.iter (fun name -> existingNameSet.Add(name) |> ignore)
      targets
      |> Seq.where (fun tgt -> existingNameSet.Contains(tgt.Label))
      |> Seq.toArray
    if alreadyAdded.Length > 0 then
      cp "\frError\f0 These files already exist in the vault:"
      for aa in alreadyAdded do
        cp $"  \fo{aa.Label}\f0 (\fy{aa.Source}\f0)"
      failwith $"Adding this file would create an ambiguously named entry in the vault"
    use writer = new VaultFileWriter(vaultFile, cryptor)
    for target in targets do
      cp $"Adding entry '\fg{target.Label}\f0' (\fk{target.Source}\f0)"
      // Use the long form append here to have more control over the metadata (enable paths)
      let fi = new FileInfo(target.Source)
      let meta = new FileMetadata(target.Label, fi.LastWriteTimeUtc |> EpochTicks.FromUtc, fi.Length)
      let _ =
        use source = File.OpenRead(target.Source)
        writer.AppendFile(source, meta)
      ()
    0
  | None ->
    Usage.usage "append"
    0



