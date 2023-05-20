// (c) 2023  ttelcl / ttelcl
module Usage

open CommonTools
open ColorPrint

let usage detailed =
  cp "\foZVLT file encryption / decryption CLI\f0"
  cp ""
  cp "\fozvlt key-new \fg-p\f0 [\fg-dv \fc<directory>\f0]"
  cp "  Create a new key in the given directory using a passphrase"
  cp "  \fg-p\fx\f0               Indicates the new key uses a passphrase"
  cp "  \fx  \fx\fx               (other methods may be implemented in the future)"
  cp "  \fg-dv \fc<directory>\f0  The folder where the key will be used."
  cp "\fozvlt check [\fg-key \fc<id>\f0|\fg-kf \fc<file>\f0] [\fg-dv \fc<directory>\f0]"
  cp "  Check the passphrase of a key"
  cp "  \fg-dv \fc<directory>\f0  The vault folder."
  cp "  \fg-key \fc<id>\f0        The first few characters of the key id"
  cp "  \fg-kf \fc<file>\f0       The full name of the key file"
  cp "\fozvlt unlock [\fg-key \fc<id>\f0|\fg-kf \fc<file>\f0] [\fg-d \fc<directory>\f0]"
  cp "\fozvlt lock [\fg-key \fc<id>\f0|\fg-kf \fc<file>\f0] [\fg-d \fc<directory>\f0]"
  cp "\fg-v               \f0Verbose mode"



