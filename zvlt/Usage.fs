// (c) 2023  ttelcl / ttelcl
module Usage

open CommonTools
open ColorPrint

let usage detail =
  let matchDetail key =
    detail = "all" || key = detail
  let showSummary key =
    matchDetail key || detail = ""
  cp "\foZVLT file encryption / decryption CLI\f0"
  cp "\frunder development \foCurrently the concept of a 'vault' is in flux.\f0"
  cp ""
  if showSummary "key-new" then
    cp "\fozvlt key-new \fg-p\f0 [\fg-dv \fc<directory>\f0]"
    cp "  Create a new directory key in the given directory using a passphrase"
  if matchDetail "key-new" then
    cp "  \fg-p\fx\f0               Indicates the new key uses a passphrase"
    cp "  \fx  \fx\fx               (other methods may be implemented in the future)"
    cp "  \fg-dv \fc<directory>\f0  The folder where the key will be used."
    cp ""
  if showSummary "check" then
    cp "\fozvlt check [\fg-key \fc<id>\f0|\fg-kf \fc<file.key-info>\f0] [\fg-dv \fc<directory>\f0]"
    cp "  Check the passphrase of a key"
  if matchDetail "check" then
    cp "  \fg-dv \fc<directory>\f0  The vault folder."
    cp "  \fg-key \fc<id>\f0        The first few characters of the key id"
    cp "  \fg-kf \fc<file>\f0       The full name of the key file"
    cp ""
  if showSummary "status" then
    cp "\fozvlt status [\fg-key \fc<id>\f0|\fg-kf \fc<file.key-info>\f0] [\fg-dv \fc<directory>\f0]"
    cp "  Check the unlock status of a key (without otherwise checking it)"
  if matchDetail "status" then
    cp ""
  if showSummary "unlock" then
    cp "\fozvlt unlock [\fg-key \fc<id>\f0|\fg-kf \fc<file.key-info>\f0] [\fg-dv \fc<directory>\f0]"
    cp "  Unlock a key, temporarily storing the raw key (until locked)"
  if matchDetail "unlock" then
    cp ""
  if showSummary "lock" then
    cp "\fozvlt lock [\fg-key \fc<id>\f0|\fg-kf \fc<file.key-info>\f0] [\fg-dv \fc<directory>\f0]"
    cp "  Lock a key, deleting the temporary raw key copy created by \fozvlt unlock\f0."
  if matchDetail "lock" then
    cp ""
  if showSummary "put" then
    cp "\fozvlt put [\fg-kf \fc<file.key-info>\f0] \fg-f \fc<file>\f0"
    cp "  Store a file in the vault folder as a new vault file"
  if matchDetail "put" then
    cp "  \fg-kf \fc<file.key-info>\f0  The descriptor file of the key to use."
    cp "  \fx    \fx               \fx  Also determines the destination folder."
    cp ""
  if showSummary "extract" then
    cp "\fozvlt extract \fg-f \fc<file.zvlt>\f0 [\fg-od \fc<out-dir>\f0]"
    cp "  Extract a file from a new vault file into the \fcout-dir\f0"
  if matchDetail "extract" then
    cp "  \fg-f \fc<file.zvlt>\f0   The vault file to extract the content of"
    cp "  \fg-od \fc<out-dir>\f0    The directory to extract to (preserving the original file name)"
    cp "  \fx    \fx         \fx    Default: current directory"
    cp ""
  // show always:
  cp "\fozvlt help \f0[\fc<command>\f0]"
  cp "  Show help with extra detail for the specified command (or all)"
  
  cp "Common options:"
  cp "\fg-v               \f0Verbose mode"



