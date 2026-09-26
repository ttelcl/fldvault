CKEY
# Master Key file format (`*.mzvlt`)

Master key files (`*.mzvlt` files) are a specialization of
[`*.zvlt`](FileFormat.zvlt.md) files that carry encrypted
keys for other files instead of encrypted files.

## Introduction

Any `*.mzvlt` file is also a valid `*.zvlt` formatted file but
carries different content. Here are the differences or specializations:

* A master key file has its `purpose` field in the vault header block
  set to 0x5453414D (`MAST`) instead of the default 0x00000000.
* A master key file is not expected to contain files as content
* A master key file contains zero or more child keys, carried in
  one or more `CKEY` blocks (described below). Each `CKEY` block
  can carry zero or more child keys.
* Additionally, a master key file may carry `PASX` blocks for the
  child keys. This is optional per key: each key in a `CKEY` block
  may or may not have a matching `PASX` block. `PASX` blocks
  provide the glue to convert a user entered passphrase into the
  actual key value. However the usefulness of this can be debated.
* Master key files MUST have a `PASS` block (and thus: have a 
  passphrase). Clients must not look up the master key from the key
  server, but always request manual passphrase entry.

## History note
* The initial design used `KTRX` blocks, one per key, instead of
  `CKEY` block(s). However the `CKEY` based design is preferred
  since no information about the contained keys is exposed before
  decryption (other than the number of keys). In contrast, `PASX`
  blocks publicly advertise the ID of the key they describe.

## Blocks in the master key file

### `PASS`: vault key descriptor

This block is documented in [FileFormat.zvlt.md](FileFormat.zvlt.md).

### `PASX`: child key descriptors

This block is documented in [FileFormat.zvlt.md](FileFormat.zvlt.md).

### `CKEY`: child keys

This is an encrypted block that carries a list of zero or more
child keys (32 bytes each), as an array. For each key there is just
that key itself, no key id or other metadata (the key ID can be
calculated from the keys, after all).

| Name | Format | Notes |
| --- |
| Kind | 'CKEY' | 0x59454B43 |
| Block Size | 4 bytes | 36 + N*32 |
| Nonce | 12 bytes | AES-GCM nonce |
| Auth Tag | 16 bytes | The resulting authentication tag |
| CipherText | N*32 bytes | The ciphertext (the encrypted target keys) |

There is no associated data. The number of keys can be calculated from
the block length.


