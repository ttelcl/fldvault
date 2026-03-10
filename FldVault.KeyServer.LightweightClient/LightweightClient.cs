using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

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
  /// If null the name used is <see cref="LightweightClientHelpers.DefaultSocketName"/>.
  /// If the folder part is missing (null case included) the folder part defaults to
  /// <see cref="LightweightClientHelpers.DefaultSocketFolder"/>.
  /// </param>
  public LightweightClient(string? socketName = null)
  {
    var socketPath = LightweightClientHelpers.ResolveSocketPath(socketName);
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
  /// Send a no-op message to the server.
  /// If this attempt throws an exception (because there actually is no server - the socket is stale),
  /// this returns false. Upon success this returns true.
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
  /// Synchronously upload a raw key to the key server.
  /// Throws an exception if the server is not reachable.
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
  /// Synchronously upload a key to the key server.
  /// Throws an exception if the server is not reachable.
  /// </summary>
  /// <param name="zkey">
  /// The buffer holding the raw key to upload
  /// </param>
  /// <exception cref="ArgumentOutOfRangeException"></exception>
  /// <exception cref="InvalidOperationException"></exception>
  public void UploadKeySync(ZkeyBuffer zkey)
  {
    UploadKeySync(zkey.Key);
  }
}
