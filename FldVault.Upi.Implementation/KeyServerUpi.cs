/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FldVault.KeyServer;

using UdSocketLib.Communication;

namespace FldVault.Upi.Implementation;

/// <summary>
/// Default implementation for <see cref="IKeyServerUpi"/>
/// </summary>
public class KeyServerUpi: IKeyServerUpi
{
  private readonly KeyServerService _keyServerService;
  private UdSocketListener? _listener;
  private KeyServerLogic? _server;
  private object _lock = new Object();

  /// <summary>
  /// Create a new KeyServerUpi
  /// </summary>
  public KeyServerUpi(string? socketName = null)
  {
    _keyServerService = new KeyServerService(socketName);
  }

  /// <inheritdoc/>
  public void Dispose()
  {
    if(_server != null)
    {
      Trace.TraceWarning("Server disposal without preparing cleanup.");
      _server.RequestStop();
      _server.Dispose();
      _server = null;
    }
    if(_listener != null)
    {
      if(!_listener.StopRequested)
      {
        Trace.TraceWarning("Server disposal without shutdown.");
        _listener.RequestStop();
      }
      _listener.Dispose();
      _listener = null;
    }
  }

  /// <inheritdoc/>
  public ServerStatus ServerState {
    get {
      lock(_lock)
      {
        if(_server == null)
        {
          return _keyServerService.ServerAvailable
            ? ServerStatus.Blocked // another server is running already
            : ServerStatus.CanStart;
        }
        else
        {
          return _server.ServerState == ServerStatus.Running
            ? ServerStatus.Running
            : ServerStatus.Stopping;
        }
      }
    }
  }

  /// <inheritdoc/>
  public ServerStatus StartServer(IKeyServerHost hostCallbacks)
  {
    lock(_lock)
    {
      var status = ServerState;
      if(status != ServerStatus.CanStart)
      {
        return status;
      }
      throw new NotImplementedException();
    }
    throw new NotImplementedException("TODO start server thread");
  }

  /// <inheritdoc/>
  public void StopServer()
  {
    lock(_lock)
    {
      if(_server != null)
      {
        _server.RequestStop();
      }
      else
      {
        if(_listener != null)
        {
          _listener.RequestStop();
        }
      }
    }
  }

  /// <inheritdoc/>
  public KeyStatus ChangeKeyStatus(Guid keyId, KeyStatus status)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public KeyStatus GetKeyStatus(Guid keyId)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public VaultInfo? IsVault(string vaultFile)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public IReadOnlyList<IKeyInfo> ListKeys()
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public KeyStatus PrepareKey(Guid keyId, string targetFile)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public KeyStatus TryUnlockKey(Guid keyId, SecureString passphrase, bool publish)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public bool WaitForServerStop(int timeout)
  {
    if(_server != null)
    {
      var result = _server.WaitForStop(timeout);
      if(result)
      {
        _server = null;
        _listener?.Dispose();
        _listener = null;
      }
      return result;
    }
    return true; // There was no server running at all
  }

}
