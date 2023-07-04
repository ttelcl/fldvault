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
  cp ""
  if showSummary "key-new" then
    cp "\fozvlt key-new \fg-p\f0 [\fg-dv \fc<directory>\f0]"
    cp "  Create a new key (\fb*.key-info\f0) in the given directory."
    cp "  \fkCurrently only passphrase based keys are supported\f0."
  if matchDetail "key-new" then
    cp "  \fg-p\fx\f0               Indicates the new key uses a passphrase"
    cp "  \fx  \fx\fx               (other methods may be implemented in the future)"
    cp "  \fg-dv \fc<directory>\f0  The folder where the key will be used."
    cp ""
  if showSummary "create" then
    cp "\fozvlt create \fg-vf \fc<file.zvlt>\f0 [\fg-key \fc<id>\f0|\fg-kf \fc<other.zvlt>\f0|\fg-p\f0]"
    cp "  Create a new empty vault file. The key is taken from an existing \fb*.key-info\f0"
    cp "  or \fb*.zvlt\f0 file. To use a new key call \fozvlt key-new\f0 first."
  if matchDetail "create" then
    cp "  \fg-vf \fc<file.zvlt>\f0  The name of the new vault file. Created in the same folder as the"
    cp "                            key file, unless you include a path."
    cp "  \fg-key \fc<id>\f0        The first few characters of the name of an existing \fb*.key-info\f0"
    cp "  \fx     \fx    \fx        file in the same folder as the new vault file."
    cp "  \fg-kf \fc<other.zvlt>\f0 The existing vault file providing the key for the new vault."
    cp "  \fg-p\f0\fx               Create a new key from a passphrase (combining \fokey-new\f0 and \focreate\f0)."
    cp "  [neither \fg-key\f0, \fg-kf\f0 nor \fg-p\f0]  Take the key from the one existing \fb*.key-info\f0 file."
    cp ""
  if showSummary "list" then
    cp "\fozvlt list \fg-vf \fc<file.zvlt>\f0 \fg-public\f0"
    cp "  List files stored in the vault"
  if matchDetail "list" then
    cp "  \fg-vf \fc<file.zvlt>\f0  The vault file to list the contents of."
    cp "  \fg-public\fx\f0          Only provide public information."
    cp ""
  if showSummary "append" then
    cp "\fozvlt append \fg-vf \fc<file.zvlt>\f0 {[\fg-p \fc<path>\f0] \fg-f \fc<file>\f0}"
    cp "  Append a file to a vault (encrypting it)"
  if matchDetail "append" then
    cp "  \fg-vf \fc<file.zvlt>\f0  The name of the existing vault file to append the file to."
    cp "  \fg-f \fc<file>\f0        The name of the file to append. Repeatable. \fkThe original path is ignored\f0."
    cp "  \fg-p \fc<path>\f0        The path to store with subsequent files. Use \fg-p \fc.\f0 to reset."
    cp ""
  if showSummary "check" then
    cp "\fozvlt check [\fg-key \fc<id>\f0|\fg-kf \fc<file.key-info>\f0|\fg-kf \fc<file.zvlt>\f0] [\fg-dv \fc<directory>\f0]"
    cp "  Check the passphrase of a key"
  if matchDetail "check" then
    cp "  \fg-dv \fc<directory>\f0  The vault folder."
    cp "  \fg-key \fc<id>\f0        The first few characters of the key id"
    cp "  \fg-kf \fc<file>\f0       The full name of the key file or a vault file"
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
  if showSummary "dump" then
    cp "\fozvlt dump \fg-vf \fc<file.zvlt>\f0"
    cp "  Dump technical details of a *.szvlt file"
  if matchDetail "dump" then
    cp "  \fg-vf \fc<file.zvlt>\f0  The vault file to analyze."
    cp ""
  if showSummary "extract" then
    cpx "\fozvlt extract \fg-vf \fc<file.zvlt>\f0 [\fg-od \fc<out-dir>\f0] [\fg-same\f0]"
    cp " [\fg-all\f0] {\fg-f \fc<file>\f0|\fg-id \fc<id>\f0} [\fg-n \fc<name>\f0]"
    cp "  Extract file(s) from a vault file into the \fcout-dir\f0"
  if matchDetail "extract" then
    cp "  \fg-vf \fc<file.zvlt>\f0  The vault file to extract content from"
    cp "  \fg-od \fc<out-dir>\f0    The directory to extract to. File names with paths are resolved"
    cp "  \fx    \fx         \fx    relative to this. Default: current directory"
    cp "  \fg-same\f0\fx            Skip the check that prevents extracting to the vault folder"
    cp "  \fg-notime\f0             Do not backdate the extract files to their original time"
    cp "  \fg-x-overwrite\f0        If the target exists, overwrite it"
    cp "  \fg-x-skip\f0             If the target exists, skip it"
    cp "  \fg-f \fc<file>\f0        The name of a file to extract (repeatable)"
    cp "  \fg-id \fc<id>\f0         The start of a file ID (use \folist\f0 to discover file IDs)"
    cp "  \fg-n \fc<name>\f0        Overide the name for the preceding file (\fg-f\f0 or \fg-id\f0)"
    cp "  \fg-all\f0\fx             Extract all files. You can use \fg-f\f0 or \fg-id\f0 with \fg-n\f0 to adjust names."
    cp ""
  // show always:
  cp "\fozvlt help \f0[\fc<command>\f0]"
  cp "  Show help with extra detail for the specified command (or all)"
  
  cp "Common options:"
  cp "\fg-v               \f0Verbose mode"
  cp "\fg-h               \f0Show help for the current command (and abort processing the command line)"
  


