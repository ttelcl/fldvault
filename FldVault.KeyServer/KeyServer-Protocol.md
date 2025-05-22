# ZVault Key Server protocol

## Introduction

The key server protocol is used to access a machine-local key server application.
A windows key server UI application is provided: `ZvaultKeyServer.exe`.

Communication uses UNIX domain sockets on Windows. While the protocol should
work on Linux too, the UI application is Windows only (WPF).

Clients can use the protocol to:

* Check if a key is available in the server based on the key ID
* Check if a key is available in the server based on a file using the key
* Checking a key in either way will add an entry to the server UI, so that the
  user can unlock the key with its passphrase.
* Upload a key to the server
* Request a key to be deleted from the server

The protocol is designed to work based on client requests, to which the server
should respond immediately (user interaction or other indetermined delays are
not allowed).

This library provides the key server specific functionality. It builds on top
of the more generic communication implemented in `UdSocketLib`, which defines
the framing and message format.

## Message introduction

Messages are identified by a 4-byte message ID. Known message IDs are defined
in two layers:

* The generic messages are defined in `UdSocketLib` in the static class
  `UdSocketLib.Framing.Layer1.MessageCodes` (refered to as namespace `MC` below).
* The key server specific messages are defined in this library here, in
  the static class `FldVault.KeyServer.KeyServerMessages`. This class also
  defines helper methods to write and read the messages to/from frames.
  (referred to as namespace `KSM` below)

This document provides a high level overview of the messages. For details
see the code, in particular the documentation comments in the static
classes mentioned above. Note that the specific messages may use the same
generic message ID, but are named differently (they are just aliases in that
case).

## Available messages

| Request | Response | Description |
| --- |
| * | `MC.Unrecognized` | Generic response similar to HTTP 40x. |
| * | `MC.ErrorText` | Generic error response, with explanatory text for the end user. |
| `MC.KeepAlive` | `MC.KeepAlive` | Keep the connection alive (not really used). |
| `KSM.KeyRequestCode` |  | Request a key based on a key ID. |
|  | `KSM.KeyResponseCode` | Succesful response to a key request. |
|  | `KSM.KeyNotFoundCode` | Key not found. |
| `KSM.KeyForFileCode` |  | Request a key based on a file. |
|  | `KSM.KeyResponseCode` | Succesful response to a key request. |
|  | `KSM.KeyNotFoundCode` | Key not found. |
|  | `MC.ErrorText` | Something's not right with the file (missing, wrong format, etc.) |
| `KSM.KeyInfoCode` |  | Request a key descriptor (zkey) based on a key ID. |
|  | `KSM.KeyInfoResponseCode` | Succesful response to a key info request. |
|  | `KSM.KeyNotFoundCode` | Key not found. |
| `KSM.KeyUploadCode` |  | Upload a key to the server. |
|  | `KSM.KeyUploadedCode` | Succesful response to a key upload (no content). |
| `KSM.KeyRemoveCode` |  | Remove a key from the server. |
|  | `KSM.KeyRemovedCode` | Succesful response to a key removal. |
| `KSM.KeyPresenceListCode` |  | Check a list of key IDs for their presence in the server. _Note: for security reasons there is no API to list ALL keys._ |
|  | `KSM.KeyPresenceListCode` | Response to a key presence list request (same code as request!). |
| `KSM.ServerDiagnosticsCode` |  | Trigger server diagnostics. The diagnostics are logged by the server, not returned |
|  | `MC.OkNoContent` | The (empty) response. |






