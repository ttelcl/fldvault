using System;
using System.IO;

using UdSocketLib;
using UdSocketLib.Communication;

namespace FldVault.KeyServer.LightweightClient;

/// <summary>
/// Implements a small subset of FldVault client API.
/// (for the full API use the FldVault.KeyServer library instead, which implements the
/// full client and full server API)
/// </summary>
public sealed class LightweightClient
{

  /// <summary>
  /// Create a new <see cref="LightweightClient"/>
  /// </summary>
  public LightweightClient(string? socketName = null)
  {
    var socketPath = ResolveSocketPath(socketName);
    SocketService = new UdSocketService(socketPath);
  }

  /// <summary>
  /// The underlying fldvault client socket service
  /// </summary>
  public UdSocketService SocketService { get; }

  /// <summary>
  /// Check if the key server app is available (or more precisely:
  /// if the server socket exists).
  /// This may give a false positive if a stale socket is present.
  /// </summary>
  public bool ServerAvailable { get => File.Exists(SocketPath); }

  /// <summary>
  /// The path to the socket (pseudo-)file.
  /// </summary>
  public string SocketPath => SocketService.SocketPath;

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
    if(socketName.IndexOfAny(__pathIndicatorChars)>=0)
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

  private static readonly char[] __pathIndicatorChars = new[] { '/', '\\', ':' };

}
