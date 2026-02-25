/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UdSocketLib.Communication;

/// <summary>
/// Unix Domain Socket server outer logic
/// </summary>
public class UdSocketService
{
  /// <summary>
  /// Create a new UdSocketServer
  /// </summary>
  public UdSocketService(
    string socketPath)
  {
    SocketPath = Path.GetFullPath(socketPath);
  }

  /// <summary>
  /// The path to the socket in the filesystem
  /// </summary>
  public string SocketPath { get; init; }

  /// <summary>
  /// Create a client connected to the socket (asynchronously).
  /// </summary>
  public async Task<UdSocketClient> ConnectClientAsync(CancellationToken cancellationToken)
  {
    var endPoint = new UnixDomainSocketEndPoint(SocketPath);
    Socket? socket = null;
    try
    {
      socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
      await socket.ConnectAsync(endPoint, cancellationToken);
      return new UdSocketClient(this, socket);
    }
    catch(Exception)
    {
      socket?.Dispose();
      throw;
    }
  }

  /// <summary>
  /// Create a client connected to the socket (synchronously, blocking).
  /// </summary>
  public UdSocketClient ConnectClientSync()
  {
    var endPoint = new UnixDomainSocketEndPoint(SocketPath);
    Socket? socket = null;
    try
    {
      socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
      socket.Connect(endPoint);
      return new UdSocketClient(this, socket);
    }
    catch(Exception)
    {
      socket?.Dispose();
      throw;
    }
  }

  /// <summary>
  /// Create a new listening (server) socket at <see cref="SocketPath"/>.
  /// Expect failure if another server is alive at the same path.
  /// </summary>
  /// <param name="backlog">
  /// The listen backlog: the number of clients that can simultaneously be waiting
  /// to be served.
  /// </param>
  /// <returns>
  /// A listening server, in the form of a <see cref="UdSocketListener"/>.
  /// </returns>
  public UdSocketListener StartServer(int backlog = 10)
  {
    File.Delete(SocketPath);
    var endPoint = new UnixDomainSocketEndPoint(SocketPath);
    Socket? socket = null;
    try
    {
      socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
      socket.Bind(endPoint);
      socket.Listen(backlog);
      return new UdSocketListener(this, socket);
    }
    catch(Exception)
    {
      socket?.Dispose();
      throw;
    }
  }

}
