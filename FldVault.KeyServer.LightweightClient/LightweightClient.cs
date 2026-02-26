using System;
using System.Diagnostics;
using System.IO;

using UdSocketLib;
using UdSocketLib.Communication;
using UdSocketLib.Framing;
using UdSocketLib.Framing.Layer1;

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
  /// <param name="socketName">
  /// Either the short name for the socket (without any path separators),
  /// or the full path to the socket, or null to use the default.
  /// If null the name used is <see cref="DefaultSocketName"/>.
  /// If the folder part is missing (null case included) it defaults to <see cref="DefaultSocketFolder"/>.
  /// </param>
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
  /// Send a dummy message to the server. If this attempt throws an exception
  /// (because there actually is no server - the socket is stale), this returns
  /// false. Upon success this returns true.
  /// </summary>
  /// <returns></returns>
  public bool TryPingSync()
  {
    try
    {
      PingSync();
    }
    catch(InvalidOperationException ex)
    {
      Trace.TraceError(
        $"Ping failed: {ex.GetType().Name}: {ex.Message}");
      return false;
    }
    catch(IOException ex)
    {
      Trace.TraceError(
        $"Ping failed: {ex.GetType().Name}: {ex.Message}");
      return false;
    }
    return true;
  }

  /// <summary>
  /// Sends a no-op message to the server, to check if it is actually alive.
  /// If it isn't, an exception is thrown.
  /// </summary>
  public void PingSync()
  {
    if(!ServerAvailable)
    {
      throw new InvalidOperationException(
        "No key server is running");
    }
    using var frameOut = new MessageFrameOut();
    frameOut.Clear().AppendI32(MessageCodes.KeepAlive);
    using var client = SocketService.ConnectClientSync();
    client.SendFrameSync(frameOut);
    using var frameIn = new MessageFrameIn();
    var receiveOk = client.TryFillFrameSync(frameIn);
    if(!receiveOk)
    {
      // This is unlikely to happen. It is more likely that ConnectClientSync() already threw an exception
      throw new InvalidOperationException(
        "No response from key server");
    }
    var messageCode = frameIn.MessageCode();
    if(messageCode != MessageCodes.KeepAlive)
    {
      throw new InvalidOperationException(
        "Unexpected ping response from key server");
    }
    // Nothing else to do now but return.
  }

  /// <summary>
  /// Synchronously upload a key to the key server
  /// </summary>
  /// <param name="rawKey">
  /// The 32 bytes raw key to upload
  /// </param>
  /// <exception cref="ArgumentOutOfRangeException"></exception>
  /// <exception cref="InvalidOperationException"></exception>
  public void UploadKeySync(ReadOnlySpan<byte> rawKey)
  {
    if(rawKey.Length != 32)
    {
      throw new ArgumentOutOfRangeException(
        nameof(rawKey),
        "Invalid key: expecting exactly 32 bytes");
    }
    using var frameOut = new MessageFrameOut();
    frameOut
      .Clear()
      .AppendI32(KeyServerMessageCodes.KeyUploadCode)
      .AppendBlob(rawKey);
    using var client = SocketService.ConnectClientSync();
    client.SendFrameSync(frameOut);
    using var frameIn = new MessageFrameIn();
    var receiveOk = client.TryFillFrameSync(frameIn);
    if(!receiveOk)
    {
      // This is unlikely to happen. It is more likely that ConnectClientSync() already threw an exception
      throw new InvalidOperationException(
        "Key server shut down");
    }
    var messageCode = frameIn.MessageCode();
    if(messageCode != KeyServerMessageCodes.KeyUploadedCode)
    {
      throw new InvalidOperationException(
        "Unexpected key upload response from key server");
    }
  }

  /// <summary>
  /// The default short name for the key server Unix Domain socket
  /// (<c>zvlt-keyserver.sock</c>)
  /// </summary>
  public const string DefaultSocketName = "zvlt-keyserver.sock";

  /// <summary>
  /// The folder where the key server socket is created by default
  /// (<c>%LocalApplicationData%/.zvlt/sockets</c>)
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
