module KeyUtilities

open System
open System.IO

open FldVault.Core.Vaults
open FldVault.Core.Zvlt2

open ColorPrint
open CommonTools

/// Get a PassphraseKeyInfoFile instance from a *.pass.keyinfo or *.zvlt file
let getPassKeyInfoFromFile (fileName: string) =
  if fileName.EndsWith(".pass.key-info", StringComparison.InvariantCultureIgnoreCase) then
    PassphraseKeyInfoFile.ReadFrom(fileName)
  elif fileName.EndsWith(".zvlt", StringComparison.InvariantCultureIgnoreCase) then
    let vf = VaultFile.Open(fileName)
    let pkif = vf.GetPassphraseInfo()
    if pkif = null then
      failwith $"Vault '{Path.GetFileName(fileName)}' does not contain its own key-info block."
    pkif
  else
    failwith $"Unrecognized key provider file '{Path.GetFileName(fileName)}'"

