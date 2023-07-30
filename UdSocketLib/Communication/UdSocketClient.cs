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
using System.Threading.Tasks;

namespace UdSocketLib.Communication
{
  /// <summary>
  /// UnixDomain Socket Client. 
  /// Represents a successfully connected client.
  /// </summary>
  public class UdSocketClient: UdDataSocket
  {

    internal UdSocketClient(UdSocketService service, Socket socket)
      : base(service, socket)
    {
    }

  }
}
