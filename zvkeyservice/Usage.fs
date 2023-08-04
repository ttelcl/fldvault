// (c) 2023  ttelcl / ttelcl
module Usage

open CommonTools
open ColorPrint

let usage detail =
  cp "\foZVLT key server\f0 (pseudo-service)"
  cp ""
  cp "\fozvkeyservice serve\f0 [\fg-s \fc<path>\f0] [\fg-F\f0]"
  cp " Run the key service"
  cp " \fg-s \fc<path>\f0   Use a non-standard socket for communication"
  cp " \fg-F\f0\fx          (or \fg-force\f0) Try to replace a broken server"
  cp ""
  cp "\fyCommon options"
  cp " \fg-v               \f0Verbose mode"



