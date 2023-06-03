# File Formats

_Old content, in process of being updated_

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


