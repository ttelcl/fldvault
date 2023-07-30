/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UdSocketLib.Communication;

/// <summary>
/// The server side of a connected Unix Domain Socket
/// </summary>
public class UdSocketServer: UdDataSocket
{
  /// <summary>
  /// Create a new UdSocketServer
  /// </summary>
  internal UdSocketServer(
    UdSocketService service,
    Socket socket)
    : base(service, socket)
  {
  }

}
