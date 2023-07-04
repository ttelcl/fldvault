module ExtractApp

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

type private FileKey =
  | FileName of string
  | FileId of string

type private ExtractFile = {
  Key: FileKey
  NameOverride: string
}

type private ExtractOptions = {
  VaultName: string
  OutDir: string
  AllowSame: bool
  ExtractAll: bool
}

let runExtract args =
  0
