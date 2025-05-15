// (c) 2025  ttelcl / ttelcl
module Usage

open CommonTools
open ColorPrint

let usage focus =
  cp "\foEncrypt / decrypt a file\f0"
  cp ""
  cp "\fomvlt \fyinfo \fg-a \fc*.mvlt\f0"
  cp "\fomvlt \fyi \fg-a \fc*.mvlt\f0"
  cp "   Get information on an *.mvlt vault"
  cp ""
  cp "\fomvlt \fycreate \fg-f \fcfile \fg-k \fc<key-source> \fg-of \fcfolder\f0"
  cp "\fomvlt \fyc \fg-f \fcfile \fg-k \fc<key-source> \fg-of \fcfolder\f0"
  cp "   Encrypt a file"
  cp "   \fg-k \fcfile.zkey\f0  Get the key from a zkey (JSON key descriptor) file"
  cp "   \fg-k \fcfile.pass.key-info\f0  Get the key from a *.pass.key-info (binary key descriptor) file"
  cp "   \fg-k \fcfile.zvlt\f0  Copy the key from a zvlt Z-vault file"
  cp "   \fg-k \fcfile.mvlt\f0  Copy the key from another mvlt M-vault file"
  cp ""
  cp "\fomvlt \fyextract \fg-a \fc*.mvlt \fg-of \fcfolder\f0"
  cp "\fomvlt \fyx \fg-a \fc*.mvlt \fg-of \fcfolder\f0"
  cp "   Decrypt a file"
  cp ""
  cp "\fg-v               \f0Verbose mode"



