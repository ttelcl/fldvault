// (c) 2025  ttelcl / ttelcl
module Usage

open CommonTools
open ColorPrint

let usage focus =
  let showSection section =
    focus = "" || focus = section
  if showSection "" then
    cp "\foEncrypt / decrypt a file\f0"
    cp ""
  if showSection "info" || showSection "check" then
    cp "\fomvlt \fyinfo \fg-a \fc*.mvlt\f0 [\fg-check\f0]"
    cp "\fomvlt \fyi \fg-a \fc*.mvlt\f0"
    cp "   Get information on an *.mvlt vault. With \fg-check\f0 the behaviour"
    cp "   is that \fomvlt check\f0. Without only basic information is shown"
    cp "   and the key (and key server) is not required."
    cp ""
    cp "\fomvlt \fycheck \fg-a \fc*.mvlt\f0"
    cp "   Alias for \fomvlt info -check\f0. Displays information about blocks and"
    cp "   the key, validates blocks, and displays metadata. Requires the key to be"
    cp "   available."
    cp ""
  if showSection "create" then
    cp "\fomvlt \fycreate \fg-f \fcfile \fg-k \fc<key-source> \fg-of \fcfolder\f0"
    cp "\fomvlt \fyc \fg-f \fcfile \fg-k \fc<key-source> \fg-of \fcfolder\f0"
    cp "   Encrypt a file"
    cp "   \fg-k \fcfile.zkey\f0  Get the key from a zkey (JSON key descriptor) file"
    cp "   \fg-k \fcfile.pass.key-info\f0  Get the key from a *.pass.key-info (binary key descriptor) file"
    cp "   \fg-k \fcfile.zvlt\f0  Copy the key from a zvlt Z-vault file"
    cp "   \fg-k \fcfile.mvlt\f0  Copy the key from another mvlt M-vault file"
    cp ""
  if showSection "extract" then
    cp "\fomvlt \fyextract \fg-a \fc*.mvlt \fg-of \fcfolder\f0"
    cp "\fomvlt \fyx \fg-a \fc*.mvlt \fg-of \fcfolder\f0"
    cp "   Decrypt a file"
    cp ""
  if showSection "" then
    cp "\fg-v               \f0Verbose mode"



