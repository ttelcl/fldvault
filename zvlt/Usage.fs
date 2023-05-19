// (c) 2023  ttelcl / ttelcl
module Usage

open CommonTools
open ColorPrint

let usage detailed =
  cp "\foZVLT file encryption / decryption CLI\f0"
  cp ""
  cp "\fozvlt key new \fg-p\f0 [\fg-d \fc<directory>\f0]"
  cp "  Create a new key in the given directory using a passphrase"
  cp "  \fg-p\fx\f0              Indicates the new key uses a passphrase"
  cp "  \fx  \fx\fx              (other methods may be implemented in the future)"
  cp "  \fg-d \fc<directory>\f0  The folder where the key will be used."
  cp "\fg-v               \f0Verbose mode"



