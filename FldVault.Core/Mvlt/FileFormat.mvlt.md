# Single-file encrypted file format

`*.mvlt` files are related to `*.zvlt` files, but encrypt only a single file,
and can be written and read as streams.
In other words, they provide just an encryption layer, not any archiving,
and don't need to know the length of the file in advance.

* Signature (4 bytes): 'MVLT'
* Minor version (2 bytes): 0x0000
* Major version (2 bytes): 0x0001
* Creation timestamp (8 bytes): creation stamp of the MVLT file, expressed
  in epoch ticks.
* A PassphraseKeyInfoFile block (96 bytes). This identifies the key.

| Offset | Name | Size | Notes |
| --- | --- | --- | --- |
| 0x00 | Signature | 4 bytes | 0x544C564D ('MVLT') |
| 0x04 | Minor version | 2 bytes | 0x0000 |
| 0x06 | Major version | 2 bytes | 0x0001 |
| 0x08 | Stamp | 8 bytes | Creation timestamp in epoch-ticks |
| 0x10 | PassphraseKeyInfoFile | 96 bytes | The key info file block |
| 0x70 | Preamble metadata block | variable | The preamble metadata block |
| ? | Start of Data block 1 | variable | The first data block |
| ? | More data blocks | variable | More data blocks |
| ? | Last Data block | variable | The last data block |
| ? | Terminator metadata block | variable | The terminator metadata block |

The rest of the file is a sequence of encrypted blocks, most of which
encrypt a chunk of the file. Each chunk encodes up to 0xD0000 bytes 
(VaultFormat.VaultChunkSize) from the cleartext, except for the last
one, which may be smaller. Before encryption, the chunk is compressed
using BZ2 compression (level 9), unless it doesn't compress well.
The chunk size is chosen to be the smallest multiple of 64 kb that is
less than 900000 bytes, guaranteeing that the compression processes exactly
one block (level 9 BZ2 compression -> compression block size is 9 * 100000).

The following types of blocks are used in the file:

| Type | 4CC | Description | Compression | Encryption |
| --- | --- | --- | --- | --- |
| 'PREM' | 0x4D455250  | Preamble metadata block | Uncompressed | Encrypted |
| 'DCMP' | 0x504D4344 | Data block (compressed) | Compressed | Encrypted |
| 'DUNC' | 0x434E5544 | Data block (not compressed / precompressed) | Uncompressed | Encrypted |
| 'POST' | 0x54534F50 | Terminator metadata block | Uncompressed | Encrypted |

The metadata types store a UTF8 encoded JSON object.

There is precisely one preamble metadata block (first block) and one terminator
metadata block (last block). The content of the two metadata blocks are merged
logically.

Each data block is encrypted using AES-GCM encryption, with the following
parameters:
* The encryption algorithm is AES-GCM
* The nonce is 12 bytes (generated based on the current time, with a twist
  to guarantee that it is unique)
* The authentication tag is 16 bytes
* The AES-GCM "associated data" always has length 16 bytes. However, its content
  varies depending on block type.
  * Type 'PREM' (first block):
    * 16 bytes: The first 16 bytes of the file header (the signature,
      version, and creation time stamp).      
  * Types 'DCMP', 'DUNC' and 'POST' (later blocks):
    * 16 bytes: the authentication tag of the previous block. Note that this
      is always well defined, because the first block is always a type 0x00
      block.

Each block is serialized as follows:

| Offset | Name | Size | Notes |
| --- | --- | --- | --- |
| 0x00 | Block type (a 4CC) | 4 bytes | The block type (as given above) |
| 0x04 | Block size | 4 bytes | The total size of this block (including this field itself) |
| 0x08 | Unpacked size | 4 bytes | The unpacked size of the block content, in bytes |
| 0x0C | Nonce | 12 bytes | The nonce used for this block |
| 0x18 | Authentication tag | 16 bytes | The authentication tag for this block |
| 0x28 | Content | variable | The content of the block, as described below |

Block content:

| Type | Content Description |
| --- | --- |
| 'PREM' | A UTF8 encoded JSON object string |
| 'DCMP' | A BZ2 compressed block of data |
| 'DUNC' | A plain block of data, not compressed |
| 'POST' | A UTF8 encoded JSON object string |

# Metadata blocks

The two metadata blocks (preamble and terminator) are UTF8 encoded JSON
objects, which should be merged together field-by-field to form a single object.
If there are two fields with the same name in both, the value of the field in the
terminator block is used. the combined metadata is expected to contain at least
the following fields:

* `modified`: The ISO 8601 date/time of the last modification of the file in an
  as high resolution as possible, and including the timezone (prefarrably 'Z' for
  UTC).
* `length`: The length of the file in bytes.

Note that the name of the file is not included in the metadata. The name of the file
is derived from the file name of the `*.mvlt` file.


