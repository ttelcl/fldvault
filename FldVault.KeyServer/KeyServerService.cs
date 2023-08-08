/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
  /// Check if the key server app is available (or more precisely:
  /// if the server socket exists)
  /// </summary>
  public bool ServerAvailable { get => File.Exists(SocketPath); }

  /// <summary>
  /// Try to get the key from the key server. Fails if the key is not
  /// present or if there is no key server. On success, the key is
  /// inserted in <paramref name="temporaryChain"/>.
  /// IMPORTANT! In the current implementation KeyChain is not thread
  /// safe. Therefore this must be a temporary key chain.
  /// </summary>
  /// <param name="keyId">
  /// The key to retrieve
  /// </param>
  /// <param name="temporaryChain">
  /// The buffer where the key is stored if found.
  /// IMPORTANT! In the current implementation KeyChain is not thread
  /// safe. Therefore this must be a temporary key chain.
  /// </param>
  /// <param name="ct">
  /// The cancellation token
  /// </param>
  /// <returns></returns>
  public async Task<bool> LookupKeyAsync(Guid keyId, KeyChain temporaryChain, CancellationToken ct)
  {
    if(!ServerAvailable)
    {
      return false;
    }
    using(var client = await SocketService.ConnectClientAsync(ct))
    {
      if(client == null)
      {
        return false;
      }
      var frameOut = new MessageFrameOut();
      frameOut.WriteKeyRequest(keyId);
      await client.SendFrameAsync(frameOut, ct);
      var frameIn = new MessageFrameIn();
      var receiveOk = await client.TryFillFrameAsync(frameIn, ct);
      if(!receiveOk)
      {
        return false;
      }
      var messageCode = frameIn.MessageCode();
      switch(messageCode)
      {
        case KeyServerMessages.KeyNotFoundCode:
          return false;
        case KeyServerMessages.KeyResponseCode:
          frameIn.ReadKeyResponse(temporaryChain);
          return true;
        default:
          throw new InvalidOperationException(
            $"Unexpected response from server: 0x{messageCode:X08}");
      }
    }
  }

  /// <summary>
  /// Check which of the keys in <paramref name="keyIds"/> are present in the server,
  /// and return a list of those that are.
  /// Returns an empty list if no key server was detected.
  /// </summary>
  /// <param name="keyIds">
  /// The key IDs to check
  /// </param>
  /// <param name="ct">
  /// The cancellation token
  /// </param>
  /// <returns>
  /// A list containing a subset of the keys in <paramref name="keyIds"/>
  /// </returns>
  public async Task<List<Guid>> CheckKeyPresenceAsync(IEnumerable<Guid> keyIds, CancellationToken ct)
  {
    var result = new List<Guid>();
    if(!ServerAvailable)
    {
      return result;
    }
    var frameOut = new MessageFrameOut();
    var keyCount = frameOut.WriteKeyPresence(keyIds);
    if(keyCount == 0)
    {
      return result;
    }
    using(var client = await SocketService.ConnectClientAsync(ct))
    {
      if(client != null)
      {
        await client.SendFrameAsync(frameOut, ct);
        var frameIn = new MessageFrameIn();
        var receiveOk = await client.TryFillFrameAsync(frameIn, ct);
        if(!receiveOk)
        {
          return result;
        }
        var messageCode = frameIn.MessageCode();
        switch(messageCode)
        {
          case KeyServerMessages.KeyPresenceListCode:
            var r2 = frameIn.ReadKeyPresence();
            return r2;
          default:
            throw new InvalidOperationException(
              $"Unexpected response from server: 0x{messageCode:X08}");
        }
      }
    }
    return result;
  }

  /// <summary>
  /// Check the presence of the keys in the key server, returning a mapping from
  /// those keys to a boolean that is false if the key is missing, true if found.
  /// If no key server is detected, null is returned.
  /// </summary>
  public async Task<Dictionary<Guid, bool>?> MapKeyPresenseAsync(IEnumerable<Guid> keyIds, CancellationToken ct)
  {
    if(!ServerAvailable)
    {
      return null;
    }
    var map = new Dictionary<Guid, bool>();
    foreach(var key in keyIds)
    {
      map[key] = false;
    }
    var list = await CheckKeyPresenceAsync(map.Keys, ct);
    foreach(var key in list)
    {
      map[key] = true;
    }
    return map;
  }

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
