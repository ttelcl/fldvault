# File Formats

# Vault files (*.zvlt)

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

## Overall structure, zvlt version 2.0

In version 2 and onward there are two layers of structure that build
on top of each other. 

### The base layer: blocks

At the base layer the entire file is a sequence of _blocks_. Each block
has the following structure:

| Name | Format | Notes |
| --- |
| Kind | 4 bytes (4CC) | 4 bytes that can be interpreted as 4 ASCII characters. <br> Block type or signature |
| Size | 4 bytes (int) | Total block size (including Kind & Size) |
| Content | `Size` - 8 bytes | Block content |

### The content layer

The file content is tructured similar as V1, but now embedded as a series
of blocks.

`<vaultfile2>` = `<file-header-block>` `<segment>*`

`<segment>` = `<segment-header-block>` `<chunk>*`

`<chunk>` = `<chunk-block>`

As far as content is concerned: V2 makes different assumptions:

* V2 no longer assumes that there is only one file (or other blob) in each
vault file. Therefore some of the V1 header fields no longer make sense,
and the vault file name can no longer be determined by "the" content (since
there may be multiple. Or none.)

* V2 no longer assumes the length of segment is known in advance; it may
be written as a stream. Therefore the segment length cannot be part of 
the "associated data".

#### file-header-block

| Name | Format | Notes |
| --- |
| Block Kind | 4CC | 'Zvlt' (0x746C665A) |
| Block Size | int | 32 |
| version | 4 bytes (int) | interpreted as 2 shorts. Value 0x00020000 (2.0) |
| unused | 4 bytes (int) | 0 |
| Key ID | 16 bytes (Guid) | |

#### segment-header-block

| Name | Format | Notes |
| --- |
| Block Kind | 4CC | 'SEGH' |
| Block Size | int | 32 |
| Purpose | 4CC | purpose indicator code. last character defines restrictions |
| Flags | int | bit field. see below |
| Vault Time | 8 bytes | EpochTicks: time of encryption |
| Source Time | 8 bytes | time of source file, or 0 if not applicable |

Note there is no segment length. But some header field values imply the
content must fit in one chunk (256 kb).

The "Purpose" is a 4CC where the last character (first byte) has a special
flag meaning.

| PurposeFlag | Notes |
| --- |
| 'M' | There may be multiple chunks. Content may be persisted.	|
| '1' | Single chunk. Content may be persisted. |
| 'S' | Single Secret. Content may be decoded to memory only |
| 'P' | Single non-encrypted chunk |

| Purpose | Hex | Notes |
| --- |
| 'NAM1' | ? | A (persistable) file name associated with the next segment |
| 'NAMS' | ? | Some non-persistable name associated with the next segment |
| 'FILM' | ? | File content. May have multiple chunks. persistable |
| 'KEYS' | ? | Linked key - a key encrypted with another one. |

_to be continued_

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

