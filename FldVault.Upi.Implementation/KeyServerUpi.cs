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
  private IKeyServerHost? _host;
  private bool _serverLoopRunning;
  private bool _serverLoopStopping;
  private object _lock = new Object();
  private Mutex _serverLoopMutex = new Mutex();
  private ManualResetEvent _serverLoopIdleEvent = new(true); // set while NOT running
  private ManualResetEvent _serverLoopRunningEvent = new(false);
  private CancellationTokenSource? _cancelServerSource;
  private Thread? _serverThread;
  
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
    if(_cancelServerSource != null)
    {
      _cancelServerSource.Cancel();
      _cancelServerSource.Dispose();
      _cancelServerSource = null;
    }
    if(_serverThread != null)
    {
      Trace.TraceWarning("Server thread is still alive at shutdown time");
      _serverThread.Join(3000);
      _serverThread = null;
    }
  }

  /// <inheritdoc/>
  public ServerStatus ServerState {
    get {
      lock(_lock)
      {
        if(_serverLoopStopping)
        {
          return ServerStatus.Stopping;
        }
        if(_serverLoopRunning)
        {
          return ServerStatus.Running;
        }
        if(_keyServerService.ServerAvailable)
        {
          return ServerStatus.Blocked;
        }
        return ServerStatus.CanStart;
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
      if(_serverThread != null)
      {
        throw new InvalidOperationException(
          "Internal error: attempting to start a new server thread when the previous one is still alive");
      }
      if(_cancelServerSource == null)
      {
        _cancelServerSource = new CancellationTokenSource();
      }
      else
      {
        Trace.TraceWarning("Cancellation Source already existed???");
      }
      //_serverThread = new Thread();
      throw new NotImplementedException();
    }
    throw new NotImplementedException("TODO start server thread");
  }

  /// <inheritdoc/>
  public void StopServer()
  {
    lock(_lock)
    {
      _cancelServerSource?.Cancel();
      if(_listener != null)
      {
        _listener.RequestStop();
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
    throw new NotImplementedException();
  }

  private async Task<bool> ServerLoop()
  {
    CancellationToken ct;
    lock(_lock)
    {
      if(!_serverLoopMutex.WaitOne(0))
      {
        Trace.TraceWarning("Unexpected: cannot acquire server loop mutex");
        return false;
      }
      _serverLoopRunning = true;
      _serverLoopIdleEvent.Reset();
      _cancelServerSource ??= new CancellationTokenSource();
      ct = _cancelServerSource.Token;
    }
    try
    {
      using(_listener = _keyServerService.SocketService.StartServer(10))
      {
        while(!ct.IsCancellationRequested)
        {
          var socket = await _listener.AcceptAsync(ct);
          await RunOneCommand(socket, ct);
        }
      }
      _listener = null;
    }
    finally
    {
      lock(_lock)
      {
        _serverLoopRunning = false;
        _serverLoopStopping = false;
        _serverLoopMutex.ReleaseMutex();
      }
      _serverLoopIdleEvent.Set();
    }
    return true;
  }

  private async Task RunOneCommand(UdSocketServer socket, CancellationToken ct)
  {
    throw new NotImplementedException();
  }
}
