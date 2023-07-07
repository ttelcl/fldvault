﻿module DumpApp

open System
open System.Collections.Generic
open System.IO

open FldVault.Core
open FldVault.Core.BlockFiles
open FldVault.Core.Vaults
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools

type private DumpOptions = {
  VaultFile: string
}

let private formatLocal (stamp: DateTime) =
  stamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz")

let memoize f =
  let dict = Dictionary<_, _>();
  fun c ->
      let exist, value = dict.TryGetValue c
      match exist with
      | true -> value
      | _ -> 
          let value = f c
          dict.Add(c, value)
          value

let spaces = memoize (fun n -> if n > 0 then new String(' ', n) else "")

let runDump args =
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "-h" :: _ ->
      None
    | "-vf" :: file :: rest ->
      rest |> parseMore {o with VaultFile = file}
    | [] ->
      if o.VaultFile |> String.IsNullOrEmpty then
        failwith "No vault file specified"
      Some(o)
    | x :: _ ->
      failwith $"Unrecognized argument '{x}'"
  let oo = args |> parseMore {
    VaultFile = null
  }
  let rec dumpBlocks prefixLength (blocks: IBlockElementContainer) =
    let prefix = prefixLength |> spaces
    let suffix = 6-prefixLength |> spaces
    for block in blocks.Children do
      let label = BlockType.ToText(block.Block.Kind)
      let kind = block.Block.Kind.ToString("X8")
      let offset = "@" + block.Block.Offset.ToString("X8")
      let size = block.Block.Size
      let childCount = block.Children.Count
      let childText =
        if childCount = 0 then
          $"\fk0 children\f0"
        else
          $"\fc{childCount}\f0 children"
      cp $"%s{prefix}\fy{offset}\f0 '\fg{label}\f0' (\fG0x{kind}\f0) {suffix}\fb%6d{size}\f0 bytes {childText}."
      block |> dumpBlocks (prefixLength + 3)
  match oo with
  | Some(o) ->
    let vaultFile = VaultFile.Open(o.VaultFile)
    let major, minor =
      let version = vaultFile.Header.Version
      version >>> 16, version &&& 0x0FFFF
    cp $"Vault file created on \fc{vaultFile.Header.TimeStamp |> formatLocal}\f0 format \fyZVLT {major}.{minor}\f0."
    vaultFile |> dumpBlocks 0
    0
  | None ->
    Usage.usage "dump"
    0
