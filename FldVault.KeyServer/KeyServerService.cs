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
using FldVault.Core.Vaults;

using UdSocketLib.Communication;
using UdSocketLib.Framing;
using UdSocketLib.Framing.Layer1;

namespace FldVault.KeyServer;

/// <summary>
/// The top level key server API, used by clients to communicate
/// with an instance of the key server.
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
  /// Try to synchronously get the key from the key server. Fails if the key is not
  /// present or if there is no key server. Clients use this to synchronously
  /// (and blocking) retrieve a key from the server when they are not interested
  /// in telling the server what file the key is for. Use <see cref="RegisterFileSync"/>
  /// for the case where the server should associate the key with a file.
  /// On success, the key is inserted in <paramref name="keyChain"/>.
  /// </summary>
  /// <param name="keyId">
  /// The key to retrieve
  /// </param>
  /// <param name="keyChain">
  /// The buffer where the key is stored if found.
  /// </param>
  /// <returns></returns>
  public bool LookupKeySync(Guid keyId, KeyChain keyChain)
  {
    if(!ServerAvailable)
    {
      return false;
    }
    using(var client = SocketService.ConnectClientSync())
    {
      if(client == null)
      {
        return false;
      }
      var frameOut = new MessageFrameOut();
      frameOut.WriteKeyRequest(keyId);
      client.SendFrameSync(frameOut);
      var frameIn = new MessageFrameIn();
      var receiveOk = client.TryFillFrameSync(frameIn);
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
          frameIn.ReadKeyResponse(keyChain);
          return true;
        default:
          throw new InvalidOperationException(
            $"Unexpected response from server: 0x{messageCode:X08}");
      }
    }
  }

  /// <summary>
  /// Asynchronously look up a key in the key server. This is the async version of
  /// <see cref="LookupKeySync"/>. The key is inserted in <paramref name="keyChain"/>
  /// if successful.
  /// </summary>
  /// <param name="keyId">
  /// The ID of the key to look up
  /// </param>
  /// <param name="keyChain">
  /// The keychain where the key is stored if found.
  /// </param>
  /// <param name="cancellationToken">
  /// Cancellation token
  /// </param>
  /// <returns>
  /// True if the key was found, false if not. An exception is thrown in
  /// error conditions.
  /// </returns>
  public async Task<bool> LookupKeyAsync(
    Guid keyId, KeyChain keyChain, CancellationToken cancellationToken)
  {
    if(!ServerAvailable)
    {
      return false;
    }
    using(var client = await SocketService.ConnectClientAsync(cancellationToken))
    {
      var frameOut = new MessageFrameOut();
      frameOut.WriteKeyRequest(keyId);
      await client.SendFrameAsync(frameOut, cancellationToken);
      var frameIn = new MessageFrameIn();
      var receiveOk = await client.TryFillFrameAsync(frameIn, cancellationToken);
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
          frameIn.ReadKeyResponse(keyChain);
          return true;
        default:
          throw new InvalidOperationException(
            $"Unexpected response from server: 0x{messageCode:X08}");
      }
    }
  }

  /// <summary>
  /// Register a *.zvlt or *.pass.key-info file in the server, associating it
  /// with its key. Also looks up that key if available (adding it to the key chain).
  /// To look up a key by ID, use <see cref="LookupKeySync"/> instead.
  /// </summary>
  /// <param name="fileName">
  /// The name of the file to register
  /// </param>
  /// <param name="keyChain">
  /// The key chain to add the key in case it is already available in the server
  /// </param>
  /// <returns>
  /// The key ID if the key already existed, null if not (even if the file was already
  /// registered).
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the server rejected the file name.
  /// </exception>
  public Guid? RegisterFileSync(string fileName, KeyChain keyChain)
  {
    if(!ServerAvailable)
    {
      return null;
    }
    using(var client = SocketService.ConnectClientSync())
    {
      if(client == null)
      {
        return null;
      }
      var frameOut = new MessageFrameOut();
      frameOut.WriteKeyForFileRequest(fileName);
      client.SendFrameSync(frameOut);
      var frameIn = new MessageFrameIn();
      var receiveOk = client.TryFillFrameSync(frameIn);
      if(!receiveOk)
      {
        return null;
      }
      var messageCode = frameIn.MessageCode();
      switch(messageCode)
      {
        case KeyServerMessages.KeyNotFoundCode:
          return null;
        case KeyServerMessages.KeyResponseCode:
          return frameIn.ReadKeyResponse(keyChain);
        case MessageCodes.ErrorText:
          var error = frameIn.ReadError() ?? "Unknown error";
          throw new InvalidOperationException(
            $"Server rejected the registration: {error}");
        default:
          throw new InvalidOperationException(
            $"Unexpected response from server: 0x{messageCode:X08}");
      }
    }
  }

  /// <summary>
  /// Asynchronously register a vault file or key info file in the server,
  /// returning the key for it into the key chain if known.
  /// </summary>
  /// <param name="fileName">
  /// The name of the file to register
  /// </param>
  /// <param name="keyChain">
  /// The key chain that will receive the key if it is found.
  /// </param>
  /// <param name="cancellationToken"></param>
  /// <returns>
  /// If not found (or if the server is not available), returns null.
  /// On success, returns the key ID.
  /// </returns>
  /// <exception cref="InvalidOperationException"></exception>
  public async Task<Guid?> RegisterFileAsync(
    string fileName, KeyChain keyChain, CancellationToken cancellationToken)
  {
    if(!ServerAvailable)
    {
      return null;
    }
    using(var client = await SocketService.ConnectClientAsync(cancellationToken))
    {
      var frameOut = new MessageFrameOut();
      frameOut.WriteKeyForFileRequest(fileName);
      await client.SendFrameAsync(frameOut, cancellationToken);
      var frameIn = new MessageFrameIn();
      var receiveOk = await client.TryFillFrameAsync(frameIn, cancellationToken);
      if(!receiveOk)
      {
        return null;
      }
      var messageCode = frameIn.MessageCode();
      switch(messageCode)
      {
        case KeyServerMessages.KeyNotFoundCode:
          return null;
        case KeyServerMessages.KeyResponseCode:
          return frameIn.ReadKeyResponse(keyChain);
        case MessageCodes.ErrorText:
          var error = frameIn.ReadError() ?? "Unknown error";
          throw new InvalidOperationException(
            $"Server rejected the registration: {error}");
        default:
          throw new InvalidOperationException(
            $"Unexpected response from server: 0x{messageCode:X08}");
      }
    }
  }

  /// <summary>
  /// Check which of the keys in <paramref name="keyIds"/> are present in the server,
  /// and return a HashSet of those that are.
  /// Returns an empty list if no key server was detected.
  /// </summary>
  /// <param name="keyIds">
  /// The key IDs to check
  /// </param>
  /// <returns>
  /// A HashSet containing a subset of the keys in <paramref name="keyIds"/>
  /// </returns>
  public HashSet<Guid> CheckKeyPresenceSync(IEnumerable<Guid> keyIds)
  {
    var result = new HashSet<Guid>();
    if(!ServerAvailable)
    {
      return result;
    }
    using(var frameOut = new MessageFrameOut())
    {
      var keyCount = frameOut.WriteKeyPresence(keyIds);
      if(keyCount == 0)
      {
        return result;
      }
      using(var client = SocketService.ConnectClientSync())
      {
        if(client != null)
        {
          client.SendFrameSync(frameOut);
          using(var frameIn = new MessageFrameIn())
          {
            var receiveOk = client.TryFillFrameSync(frameIn);
            if(!receiveOk)
            {
              return result;
            }
            var messageCode = frameIn.MessageCode();
            switch(messageCode)
            {
              case KeyServerMessages.KeyPresenceListCode:
                var r2 = frameIn.ReadKeyPresence().ToHashSet();
                return r2;
              default:
                throw new InvalidOperationException(
                  $"Unexpected response from server: 0x{messageCode:X08}");
            }
          }
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
  public Dictionary<Guid, bool>? MapKeyPresenceSync(IEnumerable<Guid> keyIds)
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
    var list = CheckKeyPresenceSync(map.Keys);
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
