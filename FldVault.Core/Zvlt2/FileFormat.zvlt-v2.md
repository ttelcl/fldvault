# *.zvlt files, redesigned (v2)

## Block files

z-vault files (v2 and later) are a specialization of _block files_.
Block files are just a sequence of blocks. Each block is serialized
in the following form:

| Name | Format | Notes |
| --- |
| Kind | 4 bytes | Usually described as a 4CC |
| Size | 4 bytes | The total byte size of the block (*) |
| Content | (Size-8) bytes | The content |

The `Kind` of the block determines the format of the content. Kinds
are normally prescribed by the application.

Note that the "Kind" of the first block in a block file implicitly
acts as the file type signature.

There are a small number of predefined kinds that have a standardized meaning.

| Kind | Hex | Size | Notes |
| --- |
| '    ' | 0x20202020 | 8 | Generic terminator |
| '((((' | 0x28282828 | 8 | Group start |
| '))))' | 0x29292929 | 8 | Group end |
| ')   ' | 0x20202029 | 8 | Implied group terminator |

* The null kind can be used to indicate the end of a sequence of blocks.
* The Group Start and Group End are empty marker blocks that indicate that
the blocks in between belong together in some way.
* Some groups have a custom start indicator, with some characters,
of which the last is '(' (0x28 as first byte). The implied group
terminator indicates the end of such groups

## Blocks in *.zvlt files

The rest of this document descrobes the blocks that must or may
appear in a *.zvlt file. Each ZVLT file must start with a ZVLT file
header block, and may subsequently contain 0 or more content elements,
each of which contain 1 or more blocks.

## ZVLT File Header Block

The file header of a ZVLT file is a block of kind 'Zvlt'

| Name | Format | Notes |
| --- | 
| Kind | 'Zvlt' | 0x746C665A |
| Size | 4 bytes | value is 48 |
| Version | 1 int (2 shorts) | 0x00020002 |
| Reserved | 1 int | 0x00000000 |
| Key ID | Guid (16 bytes) | |
| ZVLT Stamp | 8 bytes | Vault create timestamp (epoch-ticks) |
| Reserved | 8 bytes | 0x0000000000000000L |

### Encrypted content sub-blocks

Many blocks carry AES-GCM encrypted content. The "associated data" part
varies depending on the exact kind of block. The encrypted data is
serialized as part of the block's content as the following sub-block:

| Name | Format | Notes |
| --- |
| Size | int | The size in bytes of the ciphertext (and plaintext) |
| Nonce | 12 bytes | AES-GCM nonce |
| Auth Tag | 16 bytes | The resulting authentication tag |
| CipherText | (Size) bytes | The ciphertext |

### Time stamps

Any time stamps are expressed Epoch Ticks: the number of 100 nanosecond
intervals since the Unix epoch (UTC implied). That is the number of 
.net ticks minus 0x089F7FF5F7B58000L

## Unauthenticated Comment

A block that carries a non-encrypted text, without authentication
info.

| Name | Format | Notes |
| --- |
| Kind | 'UCMT' | 0x544D4F43 |
| Block Size | 4 bytes | |
| Comment | * | Encoded in UTF8 |

## Passphrase key info

Used as an embedded version of an external *.pass.key-info file. The
content is almost the same as the *.pass.key-info file content, except
that the file signature is replaced by a block header.

Presence of this block is optional, but enables the ZVLT file to be used
stand-alone.

| Name | Format | Notes |
| --- |
| Kind | 'PASS' | 0x53534150 |
| Block Size | 4 bytes | 96 |
| Stamp | 8 bytes | Timestamp this key-info was originally generated |
| Key ID | 16 bytes (Guid) | The key ID (should match the file key) |
| Salt | 64 bytes | The salt for the RFC2898 key derivation |

## File element

Files are written as a variable number of blocks: header, metadata,
one or more content blocks. Each content block contains up to 256kb
of content.

### Content file header block

| Name | Format | Notes |
| --- |
| Kind | 'FLX(' | 0x28584C46 |
| Block Size | 4 bytes | Value 32 |
| Encryption Stamp | 8 bytes | Time stamp this element was encrypted |
| File Id | Guid (16 bytes) | Randomly assigned GUID |

The number of content blocks is determined by the terminator block.

### File metadata block

This block contains an UTF8 encoded JSON string representing an
object with metadata. The format is intended to be extensible, but
should have the fields below. Note that the fields are in principle
optional: they are not guaranteed to be present.

| Field | Description |
| --- |
| name | the name of the file (*). |
| size | size in bytes (if known in advance) |
| stamp | the last write time stamp in epoch ticks | 

(*) The file name can optionally include a `/` separated _relative_
path. None of the path segments are allowed to be `.` nor `..`.

The metadata can have any additional fields you want. Note that these
all will be included in the "additional data" that is chained through
the encrypted blocks; this mechanism acts as a form of non-repudiation.

#### Binary encoding of the metadata block

| Name | Format | Notes |
| --- |
| Kind | 'FMET' | 0x54454D46 |
| Block Size | 4 bytes | |
| Nonce | 12 bytes | AES-GCM nonce |
| Auth Tag | 16 bytes | The resulting authentication tag |
| CipherText | (Size) bytes | The ciphertext |

The "associated data" for the metadata is constructed as the following
24 bytes:

* `<block header>` (8 bytes, 'FMET' + size)
* `<encryption stamp from the FLX header>` (8 bytes)
* `<ZVLT stamp from the ZVLT header>` (8 bytes)

### File content blocks

| Name | Format | Notes |
| --- |
| Kind | 'FCNT' | 0x544E4346 |
| Block Size | 4 bytes | |
| Nonce | 12 bytes | AES-GCM nonce |
| Auth Tag | 16 bytes | The resulting authentication tag |
| CipherText | (Size) bytes | The ciphertext |

The "associated data" is the Auth Tag of the previous block
(16 bytes), using the Auth tag of the metadata block for the
first content block.

### File terminator

This is just the generic "implied group terminator"

| Name | Format | Notes |
| --- |
| Kind | '`)   `' (3 spaces) | 0x20202029 |
| Block Size | 4 bytes | 8 |
