/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using UdSocketLib.Framing;

namespace UdSocketLib.Communication;

/// <summary>
/// Specialization of <see cref="UdSocketBase"/> that exposes
/// reading and writing functionality. Base class for both
/// the client side and server side of a connected socket
/// </summary>
public abstract class UdDataSocket: UdSocketBase
{
  /// <summary>
  /// Create a new UdDataSocket
  /// </summary>
  internal UdDataSocket(UdSocketService service, Socket socket)
    : base(service, socket)
  {
  }

  /// <summary>
  /// Try to fill an input message frame from this data socket (read 1 message).
  /// Returns true on success, false on EOF.
  /// </summary>
  public Task<bool> TryFillFrameAsync(MessageFrameIn frame, CancellationToken cancellationToken)
  {
    return frame.FillAsync(Socket, cancellationToken);
  }

  /// <summary>
  /// Send an output message frame to the socket
  /// </summary>
  public Task SendFrameAsync(MessageFrameOut frame, CancellationToken cancellationToken)
  {
    return frame.EmitAsync(Socket, cancellationToken);
  }

  /// <summary>
  /// Try to fill an input message frame from this data socket (read 1 message).
  /// Returns true on success, false on EOF.
  /// </summary>
  public bool TryFillFrameSync(MessageFrameIn frame)
  {
    return frame.FillSync(Socket);
  }

  /// <summary>
  /// Send an output message frame to the socket
  /// </summary>
  public void SendFrameSync(MessageFrameOut frame)
  {
    frame.EmitSync(Socket);
  }

}
