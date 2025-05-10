# Single-file encrypted file format

`*.mvlt` files are similar to `*.zvlt` files, but encrypt only a single file.
In other words, they provide just an encryption layer, not any archiving.

* Signature (4 bytes): 'MVLT'
* Minor version (2 bytes): 0x0000
* Major version (2 bytes): 0x0001
* Reserved (8 bytes): 0x0000000000000000
* A PassphraseKeyInfoFile block (96 bytes). This identifies the key.
* The timestamp of the file in epoch-ticks (8 bytes)
* The original length of the file (4 bytes)

The rest of the file is a sequence of encrypted blocks, each of which
encrypts a chunk of the file. Each chunk encodes 0xD0000 bytes 
(VaultFormat.VaultChunkSize) from the cleartext, except for the last
one, which may be smaller. Before encryption, the chunk is compressed
using BZ2 compression (level 9), unless it doesn't compress well.
The chunk size is chosen to be the smallest multiple of 64 kb that is
less than 900000 bytes, guaranteeing that the compression processes exactly
one block (level 9 BZ2 compression -> compression block size is 9 * 100000).

Each compressed block is encrypted as follows:
* The compression algorithm is AES-GCM
* The nonce is 12 bytes (generated based on the current time, with a twist
  to guarantee that it is unique)
* The authentication tag is 16 bytes
* The AES-GCM associated data is normally the 16 bytes of the previous
  block's tag, except for the first block, which uses 16 bytes constructed as follows:
  * 8 bytes: the original length of the file
  * 8 bytes: the timestamp of the file in epoch-ticks

Each block is serialized as follows:
* Size + flags (4 bytes):
  * The lower 3 bytes: Size: the total size of this block in this file in bytes
          (including this size+flags field itself)
  * The upper byte: flags:
    * 0x01: the block is compressed
* Nonce (12 bytes): the nonce used for this block
* Authentication tag (16 bytes): the authentication tag for this block
* Ciphertext (variable): the ciphertext of the block

Note that the decoded and decrypted sizes are implicit, not explicit:
* The decrypted size of the block is the same as the ciphertext size
* The decompressed size of the block is always 0xD0000 bytes, except possibly
  the last block, whose size can be calculated from the original length of the file

Unlike the ZVLT format, the MVLT format does not have a block kind - there is only
one kind of block.
