// (c) 2023  ttelcl / ttelcl
module Usage

open CommonTools
open ColorPrint

let usage detailed =
  cp "\foZVLT key server\f0 (pseudo-service)"
  cp ""
  cp "\fozvkeyservice run\f0 [options]"
  cp " Run the key service"
  cp " \fg-F\f0        (or \fg-force\f0) Try to replace a seemingly running server"
  cp ""
  cp "\fozvkeyservice stop\f0"
  cp " If a key service is running, send it a stop command"
  cp ""
  cp "\fyCommon options"
  cp " \fg-v               \f0Verbose mode"



