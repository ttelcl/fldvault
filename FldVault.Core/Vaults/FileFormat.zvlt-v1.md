# Vault files (*.zvlt) Version 1.1 

## (DEPRECATED / historical)

:warning: _Information in this document describes an earlier design
of *.zvlt files and is no longer used_ :warning:

<hr>

The current vault file format supports two variants:

* "Normal" vault files containing an encrypted normal file, intended to be
decrypted to a file at some point in the future
* "Secret" vault files containing a "small" secret value, intended to only ever
be decrypted to memory, never to a file or other persisted medium.

## Overall structure, version 1.1

`<vaultfile>` = `<header>` `<segment>*`

`<segment>` = `<kind-and-length>` `<chunk-count>` `<chunk>*`

`<chunk>` = `<chunksize>` `<nonce>` `<auth-tag>` `<ciphertext>`

_See below for version 2_

## File header (1.1)

The file header has 48 bytes and contains the following fields

| Name | Format | Description |
|  --- | --- | --- |
| Signature | 8 bytes (long) | See signature table below |
| Version   | 4 bytes (int)  | 0x00010001 |
| Unused    | 4 bytes (int)  | 0x00000000 |
| Key ID    | 16 bytes (guid) | |
| Write Time | 8 bytes (long) | In epoch-ticks (see note below) |
| Source Time | 8 bytes (long) | In epock-ticks (see note below) |

| Signature | ASCII | Binary | Notes |
| --- |
| Normal | "ZVLTFLE\0" | 0x00454C46544C565A | Decryptable to file |
| Secret | "ZVLTSEC\0" | 0x00434553544C565A | Decryptable to memory only |

### Time stamps

Time stamps are represented in "epoch ticks". Epoch ticks are .net ticks
since the unix epoch: 100 nanosecond intervals since 1970-01-01 00:00:00 UTC.
In other words: `stamp.Ticks - 0x089F7FF5F7B58000L`.

## Segment header (1.1)

| Name | Format | Description |
|  --- | --- | --- |
| kind and length | 8 bytes | treated as 2 bytes + 6 bytes |
| kind | MSB 2 bytes of those 8 | Segment type indicator. See below |
| length | LSB 6 bytes of those 8 | Total source length. <br> In secret vaults capped at 256Kb |
| chunk count | 4 bytes (int) | Number of chunks in the segment. <br> In secret vaults capped at 1 |

### Segment kinds

* Odd segment kinds indicate that the segment should be decoded to memory only,
and can contain only one chunk.
* Even segment kinds may be decoded to files (or other persisted media) and may
contain multiple chunks.

| Kind | Vault | Description |
| --- |
| 0 | unused | Reserved to act as "terminator" |
| 1 | normal | File name (UTF8, relative path or no path at all)|
| 2 | normal | File contemt |
| 3 | secret | Secret blob content |

## Chunks (1.1)

The content for each segment is split in one or more "chunks". Each chunk
is encrypted or decrypted in one call using AES-GCM. Since the input and
output must fit in a reasonably small memory buffer, the maximum size of
a chunk is restricted - we chose the maximum chunk size as 256kb. The
reason chunks exist is because the system must be able to handle files
larger than that.

When encrypting data that is not intended to ever be persisted to a file
(but used in-memory only), the segment must have only 1 chunk.

### Chunk content

| Name | Format | Notes |
| --- |
| Size | 4 bytes (int) | The total chunk size in the vault file in bytes |
| Nonce | 12 bytes | The AES-GCM nonce for this chunk |
| Auth Tag | 16 bytes | The AES-GCM authentication tag |
| Ciphertext | X bytes | The rest of the chunk |

### Chunk associated data

The encryption does include a 16 byte "associated data" for each chunk.
This data is deterministic, so it is not stored anywhere. For the second
and later chunks of a segment, the associated data is the Authentication
Tag of the previous chunk. For the first chunk the 16 bytes are two
8 byte fields.

| Name | Format | Notes |
| --- |
| kind and segment length | 8 bytes | Copied from the segment header |
| write time | 8 bytes | Copied from the vault header |

The associated data is part of the checksum (authentication tag) of a
chunk. The idea is to include fields that when included in the MAC 
prevent misusing an encrypted chunk to pose as anything other than
what it actually is.

