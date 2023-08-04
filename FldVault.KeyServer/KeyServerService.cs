/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Crypto;

using UdSocketLib.Communication;
using UdSocketLib.Framing;
using UdSocketLib.Framing.Layer1;

namespace FldVault.KeyServer;

/// <summary>
/// The top level key server API.
/// This does not include the actual running instance object. 
/// </summary>
public class KeyServerService
{
  /// <summary>
  /// Create a new KeyServerService
  /// </summary>
  public KeyServerService(
    KeyChain keyChain,
    string? socketName = null)
  {
    SocketPath = ResolveSocketPath(socketName);
    SocketService = new UdSocketService(SocketPath);
  }

  /// <summary>
  /// The full path to the socket
  /// </summary>
  public string SocketPath { get; init; }

  /// <summary>
  /// The socket service that is part of this KeyServerService
  /// </summary>
  public UdSocketService SocketService { get; init; }

  /// <summary>
  /// The default short name for the key server Unix Domain socket
  /// </summary>
  public const string DefaultSocketName = "zvlt-keyserver.sock";

  /// <summary>
  /// The folder where the key server socket is created by default
  /// </summary>
  public static string DefaultSocketFolder { get; } =
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".zvlt", "sockets");

  /// <summary>
  /// Get the full path to the socket pseudo-file
  /// </summary>
  /// <param name="socketName">
  /// Either the short name for the socket (without any path separators),
  /// or the full path to the socket, or null to use the default.
  /// </param>
  public static string ResolveSocketPath(string? socketName)
  {
    socketName ??= DefaultSocketName;
    if(socketName.IndexOfAny(pathIndicatorChars)>=0)
    {
      return Path.GetFullPath(socketName);
    }
    else
    {
      if(!Directory.Exists(DefaultSocketFolder))
      {
        Directory.CreateDirectory(DefaultSocketFolder);
      }
      return Path.Combine(DefaultSocketFolder, socketName);
    }
  }

  private static readonly char[] pathIndicatorChars = new[] { '/', '\\', ':' };

}
