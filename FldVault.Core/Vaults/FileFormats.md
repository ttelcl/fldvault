# File Formats

## Vault files (*.zvlt)

The current vault file format supports two variants:

* "Normal" vault files containing an encrypted normal file, intended to be
decrypted to a file at some point in the future
* "Secret" vault files containing a "small" secret value, intended to only ever
be decrypted to memory, never to a file or other persisted medium.

### Overall structure

`<vaultfile>` = `<header>` `<segment>*`

`<segment>` = `<kind-and-length>` `<chunk-count>` `<chunk>*`

`<chunk>` = `<chunksize>` `<nonce>` `<auth-tag>` `<ciphertext>`

### File header

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

#### Time stamps

Time stamps are represented in "epoch ticks". Epoch ticks are .net ticks
since the unix epoch: 100 nanosecond intervals since 1970-01-01 00:00:00 UTC.
In other words: stamp.Ticks - 0x089F7FF5F7B58000L.

### Segment header

| Name | Format | Description |
|  --- | --- | --- |
| kind and length | 8 bytes | treated as 2 bytes + 6 bytes |
| kind | MSB 2 bytes of those 8 | Segment type indicator. See below |
| length | LSB 6 bytes of those 8 | Total source length. <br> In secret vaults capped at 256Kb |
| chunk count | 4 bytes (int) | Number of chunks in the segment. <br> In secret vaults capped at 1 |

#### Segment kinds

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










