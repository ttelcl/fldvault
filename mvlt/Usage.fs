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
  cp "\fomvlt \fycreate \fg-f \fcfile \fg-k \fcguid|*.pass.key-info|*.zvlt|*.mvlt \fg-of \fcfolder\f0"
  cp "\fomvlt \fyc \fg-f \fcfile \fg-k \fcguid|*.pass.key-info|*.zvlt|*.mvlt \fg-of \fcfolder\f0"
  cp "   Encrypt a file"
  cp ""
  cp "\fomvlt \fyextract \fg-a \fc*.mvlt \fg-of \fcfolder\f0"
  cp "\fomvlt \fyx \fg-a \fc*.mvlt \fg-of \fcfolder\f0"
  cp "   Decrypt a file"
  cp ""
  cp "\fg-v               \f0Verbose mode"



