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
/// Wraps a Unix Domain Socket that is listening for incoming connections
/// </summary>
public class UdSocketListener: UdSocketBase, IStopRequest
{

  /// <summary>
  /// Create a new UdSocketListener
  /// </summary>
  internal UdSocketListener(UdSocketService service, Socket listeningSocket)
    : base(service, listeningSocket)
  {
  }

  /// <summary>
  /// Asynchronously wait for the next client to arrive and return
  /// the corresponding server side connected socket
  /// </summary>
  /// <returns></returns>
  public async Task<UdSocketServer> AcceptAsync(
    CancellationToken cancellationToken)
  {
    var listeningSocket = Socket;
    var dataSocket = await listeningSocket.AcceptAsync(cancellationToken);
    // dataSocket.LingerState = new LingerOption(true, 1);
    return new UdSocketServer(Service, dataSocket);
  }

  /// <inheritdoc/>
  public bool StopRequested { get; private set; }

  /// <inheritdoc/>
  public void RequestStop()
  {
    StopRequested = true;
  }

  /// <summary>
  /// Clean up (including deletion of the socket "file").
  /// </summary>
  protected override void Dispose(bool disposing)
  {
    if(!IsDisposed)
    {
      base.Dispose(disposing);
      File.Delete(Service.SocketPath);
    }
  }

}
