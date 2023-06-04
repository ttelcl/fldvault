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
are normally prescribed by the application. There are a small number
of predefined kinds that have a standardized meaning.

| Kind | Hex | Size | Notes |
| --- |
| _(null)_ | 0x00000000 | 8 | Generic terminator |
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

## File Header Block

The file header of a ZVLT file is a block of kind 'Zvlt'

| Name | Format | Notes |
| --- | 
| Kind | 'Zvlt' | 0x746C665A |
| Size | 4 bytes | value is 32 |
| Version | 1 int (2 shorts) | 0x00020000 |
| Unused | 1 int | 0x00000000 |
| Key ID | Guid (16 bytes) | |

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

## File element

Files are written as a variable number of blocks: header, name, one or
more content blocks. Each content block except possibly
the last contains 256kb of content

### File header block

| Name | Format | Notes |
| --- |
| Kind | 'FLX(' | 0x28584C46 |
| Block Size | 4 bytes | Value 28 |
| Zvlt Stamp | 8 bytes | Time stamp this block was encrypted |
| File Stamp | 8 bytes | Time stamp of source file |
| File size | 4 bytes | Size of the file |

The number of content blocks can be predicted from the file size, but
can also be determined by the terminator block.

### File name block

This carries the file name in encrypted form. The file name must
be relative. Typically without any path components, but if there are path
components they must use '/' as separator. The character '\' is forbidden.
The file name cannot have path segments that are '..' or '.'.

| Name | Format | Notes |
| --- |
| Kind | 'FNAM' | 0x4D414E46 |
| Block Size | 4 bytes | |
| Nonce | 12 bytes | AES-GCM nonce |
| Auth Tag | 16 bytes | The resulting authentication tag |
| CipherText | (Size) bytes | The ciphertext |

The "associated data" for the name is constructed as the following
28 bytes:

* `<block header>` (8 bytes, 'FNAM' + size)
* `<element header past block header>` (20 bytes)

### File first content block

| Name | Format | Notes |
| --- |
| Kind | 'FCT1' | 0x31544346 |
| Block Size | 4 bytes | |
| Nonce | 12 bytes | AES-GCM nonce |
| Auth Tag | 16 bytes | The resulting authentication tag |
| CipherText | (Size) bytes | The ciphertext |

The "associated data" for the content is constructed as the following
24 bytes:

* `<block header>` (8 bytes, 'FCT1' + size)
* `<ZVLT stamp>` (8 bytes, from element header)
* `<File stamp>` (8 bytes, from element header)

### File subsequent content blocks

| Name | Format | Notes |
| --- |
| Kind | 'FCTn' | 0x6E544346 |
| Block Size | 4 bytes | |
| Nonce | 12 bytes | AES-GCM nonce |
| Auth Tag | 16 bytes | The resulting authentication tag |
| CipherText | (Size) bytes | The ciphertext |

The "associated data" is now calculated differently: it is
the Auth Tag of the previous block (16 bytes)

### File terminator

This is just the generic "implied group terminator"

| Name | Format | Notes |
| --- |
| Kind | ')   ' | 0x20202029 |
| Block Size | 4 bytes | 8 |

## Unauthenticated Comment

A block that carries a non-encrypted text, without authentication
info.

| Name | Format | Notes |
| --- |
| Kind | 'UCMT' | 0x544D4F43 |
| Block Size | 4 bytes | |
| Comment | * | Encoded in UTF8 |
