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

namespace UdSocketLib.Framing;

/// <summary>
/// Utility methods for sending and receiving full buffers to/from sockets
/// </summary>
public static class SocketUtility
{

  /// <summary>
  /// Asynchronously receive data from the socket onto the buffer, repeatedly
  /// until the buffer is filled. 
  /// </summary>
  /// <param name="socket">
  /// The socket to read from
  /// </param>
  /// <param name="buffer">
  /// The buffer to write to
  /// </param>
  /// <param name="cancellationToken">
  /// A cancellation token to mark cancellation of the operation
  /// </param>
  /// <returns>
  /// True if the buffer was completely filled.
  /// False if the first read attempt indicated that there is nothing more to read.
  /// </returns>
  /// <exception cref="EndOfStreamException">
  /// Thrown when some data was read, and subsequently the socket indicated that the
  /// end of the stream was reached.
  /// </exception>
  public static async Task<bool> TryFullyReceiveAsync(
    this Socket socket, Memory<byte> buffer, CancellationToken cancellationToken)
  {
    var offset = 0;
    while(offset < buffer.Length)
    {
      var m = buffer[offset..];
      var n = await socket.ReceiveAsync(m, SocketFlags.None, cancellationToken);
      if(n == 0)
      {
        if(offset > 0)
        {
          throw new EndOfStreamException("Unexpected end of socket stream");
        }
        else
        {
          return false;
        }
      }
      offset += n;
    }
    return true;
  }

  /// <summary>
  /// Try to fully send the <paramref name="buffer"/> to the
  /// <paramref name="socket"/>.
  /// </summary>
  /// <param name="socket">
  /// The socket to send to
  /// </param>
  /// <param name="buffer">
  /// The buffer holding the contents to send
  /// </param>
  /// <param name="cancellationToken">
  /// The cancellation token
  /// </param>
  public static async Task TryFullySendAsync(
    this Socket socket, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
  {
    var offset = 0;
    while(offset < buffer.Length)
    {
      var m = buffer[offset..];
      var n = await socket.SendAsync(m, SocketFlags.None, cancellationToken);
      if(n == 0)
      {
        throw new EndOfStreamException("Cannot write to socket");
      }
      offset += n;
    }
  }

}
