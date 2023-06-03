# Key unlock files (_\<id\>_.unlock)

These files are stored in a "safe" place (separate from the actual
vault files, in a directory that should not be cloud-backed) and
temporarily carry the raw keys of vault files.
This mechanism aims to avoid having to type the passphrase repeatedly
in th CLI.

In the future some kind of "key server" may be developed to provide
a similar feature using in-memory.

| Name | Format | Notes |
| --- |
| Signature | 8 bytes | 0x000059454B574152 "RAWKEY\0\0" |
| Unused | 8 bytes | 0L |
| Key bytes | 32 bytes | The key id can be deduced from the bytes |

