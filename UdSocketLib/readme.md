# UdSocketLib

This library provides a framework for IPC using Unix Domain Sockets.
The implementation works on both Windows and Linux.

## Core functionality

The core functionality is provided using the class `UdSocketService`
as API entry point. A generic use scenario is depicted below. In this
demo scenario the server handles clients one by one: clients have to
wait for the previous client to finish.

### Server side use

```csharp
var service = new UdSocketService(socketpath);
// step 1: create the listening socket
using(var listener = service.StartServer(backlog)) {
  while(!done) {
    // step 2: wait for a client to connect
    using(var serverSideSocket = await listener.AcceptAsync(cancellationToken)) {
      // step 3: communicate with the client via serverSideSocket
      ...
    }
    // We are done with this client, now wait for the next client
    // (or stop the server when 'done').
  }
}
```

### Client side use

```csharp
var service = new UdSocketService(socketpath);
// step 1: connect to the server
using(var clientSideSocket = await service.ConnectClientAsync(cancellationToken) {
  // step 2: communicate with the server via clientSideSocket
  ...
}
```


# Message frame conventions

A message based communication subframework is layered on top of
the core functionality.

## Layer 0

At the lowest level messages are byte blobs of up to 65536 (0x10000)
bytes. The first two bytes contain the content length (range 0-65534),
the rest are the message content. As a result, the total message length
is between 2 and 65536.
All 16 bit message content lengths are valid, except 0xFFFF. That
length can be used as a marker to trigger an emergency disconnect.

## Layer 1

From this layer onward we assume a REST-like client-server
communication: the client sends a request and the server answers
with a reply. Repeat until terminated.

In other words: for now we ignore server-initiated communication and
other situations that do not fit the _client sends 1 request, 
server sends 1 reply_ model.

The first 4 bytes of the message indicate the message code, playing
a role similar to a route+verb in a REST API. For client -> server
messages you can call it the _request code_, for server -> client
messages the _response code_.

If a server does not recognize a request message code it can indicate
so in its response, either by aborting the connection (using the layer
0 abort message, or terminating the underlying socket connection),
or by sending one of the predefined response codes.

Here are some general purpose message codes:

| code | meaning | payload |
| --- |
| 0x00000204 | OK, no content | (none) |
| 0x00000400 | Error, request not understood | (none) |
| 0x00000000 | Keep-alive request | (none), expect the same as response |
| 0x10000001 | Text | a string, expect a Text response |
| 0x10000002 | Json | a json string |

The 0x10 at the MSB of the Text message indicates that there is further
content, but there is no deeper structure to that message content:
a server or client not recognizing the message code has no way of 
gaining any understanding of how to interpret the message.

The 0 as top nibble for the OK, error and keep-alive messages indicates
there is no content expected.

It is expected that generic message structure will be defined later, using
values other than 0 or 1 for the top nibble.
