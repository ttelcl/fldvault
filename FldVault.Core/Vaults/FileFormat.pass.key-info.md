# Passphrase key link files (_\<id\>_.pass.key-info)

These small files (96 bytes) contain the information needed to turn
a passphrase into an actual key. Their name include the key's ID (as
a GUID).

| Name | Format | Notes |
| --- |
| Signature | 8 bytes | "PASSINF\0" = 0x00464E4953534150L |
| Stamp | 8 bytes | Timestamp this key-info was written (epoch-ticks) |
| Key ID | 16 bytes (Guid) | The key ID (should match the file name) |
| Salt | 64 bytes | The salt for the RFC2898 key derivation |

The Key ID is calculated from the raw key bytes: the first 16 bytes
of the SHA256 hash of the raw key bytes.
