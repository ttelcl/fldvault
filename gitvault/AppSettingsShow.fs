module AppSettingsShow

open System
open System.IO

open Newtonsoft.Json

open GitVaultLib.Configuration

open ColorPrint
open CommonTools

let run args =
  // there are no arguments in this case, no need to parse them
  let settings = CentralSettings.Load()
  let json = JsonConvert.SerializeObject(settings, Formatting.Indented)

  cp $"Machine-wide gitvault settings (\fg{CentralSettings.CentralSettingsFileName}\f0):\n"
  cp $"{json}\n\f0"
  
  if settings.Anchors.Count = 0 then
    cp "\frNo vault anchor folders have been registered yet!\f0"
    cp "Use the \fogitvault anchor add\f0 command to get started."
    cp ""
    Usage.usage "anchor-add"
    1
  else
    0

