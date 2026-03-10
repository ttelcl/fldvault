# FldVault.KeyServer.LightweightClient

This library provides a lightweight API for communicating with an
FldVault Key Server.

## Features

* It only implements a small subset of the client operations. Many clients
  may prefer to use the full client functionality implemented in 
  `FldVault.KeyServer` instead.
* The primary use case is applications that want to upload keys to a server,
  for instance to support alternative key sources beyond the default
  passphrase based keys.
* It only depends on `UdSocketLib`, not on any other FldVault libraries. 
  Instead, this reimplements a small subset of `FldVault.Core` and
  `FldVault.KeyServer`.
