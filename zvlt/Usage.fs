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
  cp "\fozvlt check [\fg-key \fc<id>\f0|\fg-kf \fc<file.key-info>\f0] [\fg-dv \fc<directory>\f0]"
  cp "  Check the passphrase of a key"
  cp "  \fg-dv \fc<directory>\f0  The vault folder."
  cp "  \fg-key \fc<id>\f0        The first few characters of the key id"
  cp "  \fg-kf \fc<file>\f0       The full name of the key file"
  cp "\fozvlt unlock [\fg-key \fc<id>\f0|\fg-kf \fc<file.key-info>\f0] [\fg-dv \fc<directory>\f0]"
  cp "  Unlock a key, temporarily storing the raw key (until locked)"
  cp "\fozvlt lock [\fg-key \fc<id>\f0|\fg-kf \fc<file.key-info>\f0] [\fg-dv \fc<directory>\f0]"
  cp "  Lock a key, deleting the temporary raw key copy created by \fgunlock\f0."
  cp "\fozvlt put [\fg-key \fc<id>\f0|\fg-kf \fc<file.key-info>\f0] [\fg-dv \fc<directory>\f0] \fg-f \fc<file>\f0"
  cp "  Store a file in the vault folder as a new vault file"
  cp "\fozvlt extract \fg-f \fc<file.zvlt>\f0 [\fg-od \fc<out-dir>\f0]"
  cp "  Extract a file from a new vault file into the \fcout-dir\f0"
  cp "  \fg-f \fc<file.zvlt>\f0   The vault file to extract the content of"
  cp "  \fg-od \fc<out-dir>\f0    The directory to extract to (preserving the original file name)"
  cp "  \fx    \fx         \fx    Default: current directory"
  cp "Common options:"
  cp "\fg-v               \f0Verbose mode"



