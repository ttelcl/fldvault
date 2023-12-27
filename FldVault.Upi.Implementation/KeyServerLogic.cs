/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FldVault.KeyServer;
using FldVault.Upi.Implementation.Keys;

using UdSocketLib.Communication;
using UdSocketLib.Framing;
using UdSocketLib.Framing.Layer1;

namespace FldVault.Upi.Implementation;

/// <summary>
/// Provides an opiniated implementation of the server side of a
/// vault key server. This runs on background threads.
/// </summary>
public class KeyServerLogic: IDisposable
{
  private readonly TaskCompletionSource _serverStarted;
  private readonly TaskCompletionSource _serverCompleted;
  private readonly UdSocketListener _listener;
  private readonly CancellationTokenSource _stopRequest;
  private Task? _serverTask;
  private readonly MessageFrameIn _frameIn;
  private readonly MessageFrameOut _frameOut;
  private readonly Dictionary<int, Func<MessageFrameIn, MessageFrameOut, Task>> _handlers;

  /// <summary>
  /// Create a new KeyServerLogic
  /// </summary>
  /// <param name="host">
  /// The host API providing UI callbacks
  /// </param>
  /// <param name="api">
  /// The server API implementation
  /// </param>
  /// <param name="listener">
  /// The already-opened and listening server socket
  /// </param>
  /// <param name="keyStates">
  /// The key state storage
  /// </param>
  internal KeyServerLogic(
    IKeyServerHost host,
    KeyServerService api,
    UdSocketListener listener,
    KeyStateStore keyStates)
  {
    Host = host;
    Api = api;
    KeyStates = keyStates;
    _listener = listener;
    _serverStarted = new TaskCompletionSource();
    _serverCompleted = new TaskCompletionSource();
    _stopRequest = new CancellationTokenSource();
    _frameIn = new MessageFrameIn();
    _frameOut = new MessageFrameOut();
    _handlers = new() {
      [MessageCodes.KeepAlive] = HandleKeepAlive,
    };
  }

  /// <summary>
  /// Dispose the message buffers
  /// </summary>
  public void Dispose()
  {
    _frameIn.Dispose();
    _frameOut.Dispose();
  }

  /// <summary>
  /// The host interface
  /// </summary>
  public IKeyServerHost Host { get; }

  /// <summary>
  /// The low level message API implementation
  /// </summary>
  public KeyServerService Api { get; }

  /// <summary>
  /// The key state store
  /// </summary>
  public KeyStateStore KeyStates { get; }

  /// <summary>
  /// A cancellation token that is in "cancellation requested" state after
  /// server stop is requested
  /// </summary>
  public CancellationToken StopRequested { get => _stopRequest.Token; }

  /// <summary>
  /// The server status as far as affected by this object, and slightly reinterpreted.
  /// <see cref="ServerStatus.CanStart"/> is reinterpreted as "stopped".
  /// This will never return <see cref="ServerStatus.Blocked"/>, because that would
  /// mean this object cannot exist.
  /// </summary>
  public ServerStatus ServerState {
    get {
      if(_serverCompleted.Task.IsCompleted)
      {
        return ServerStatus.CanStart; // meaning "Stopped"
      }
      if(_stopRequest.IsCancellationRequested)
      {
        return ServerStatus.Stopping;
      }
      return ServerStatus.Running;
    }
  }

  /// <summary>
  /// Start the server on a background thread, and return if it was successfully started 
  /// </summary>
  /// <returns>
  /// True if the server was successfully started and is running. False if startup failed.
  /// </returns>
  public async Task<bool> Start()
  {
    if(_serverTask != null)
    {
      throw new InvalidOperationException(
        "Server start was already triggered");
    }
    // var serverTask = Task.Factory.StartNew(Run, _stopRequest.Token).Unwrap();
    var serverTask = Task.Run(Run, _stopRequest.Token);
    _serverTask = serverTask;
    await _serverStarted.Task;
    return true;
  }

  /// <summary>
  /// Initiate the process of stopping the server
  /// </summary>
  public void RequestStop()
  {
    _listener.RequestStop();
    _stopRequest.Cancel();
  }

  /// <summary>
  /// Wait for the server to stop (requesting a stop first, if not done so)
  /// </summary>
  /// <param name="milliseconds">
  /// The maximum time to wait
  /// </param>
  /// <returns>
  /// True if the server stopped before the timeout
  /// </returns>
  public bool WaitForStop(int milliseconds)
  {
    if(!_listener.StopRequested)
    {
      RequestStop();
    }
    var result = _serverCompleted.Task.Wait(milliseconds);
    return result;
  }

  private async Task Run()
  {
    _serverStarted.SetResult();
    try
    {
      while(!_stopRequest.IsCancellationRequested)
      {
        var connection = await _listener.AcceptAsync(_stopRequest.Token);
        if(!_stopRequest.IsCancellationRequested)
        {
          try
          {
            await HandleRequest(connection);
          }
          catch(Exception ex)
          {
            Trace.TraceError($"Exception while handling request: {ex}");
            throw;
          }
        }
      }
    }
    catch(OperationCanceledException oce)
    {
      Trace.TraceInformation($"Caught OperationCanceledException. {oce.Message}");
    }
    finally
    {
      _serverCompleted.SetResult();
    }
  }

  private async Task HandleRequest(UdSocketServer connection)
  {
    var ok = await connection.TryFillFrameAsync(_frameIn, _stopRequest.Token);
    if(ok) // otherwise client has disconnected
    {
      if(!_stopRequest.IsCancellationRequested)
      {
        var messageCode = _frameIn.MessageCode();
        try
        {
          Trace.TraceInformation($"Received request code {messageCode:X08}");
          var handler = _handlers[messageCode];
          if(handler != null)
          {
            _frameOut.Clear();
            await handler(_frameIn, _frameOut);
            if(_frameOut.Position == 0)
            {
              throw new InvalidOperationException(
                $"Internal error: handler for {messageCode:X08} did not fill message buffer");
            }
          }
          else
          {
            _frameOut.WriteNoContentMessage(MessageCodes.Unrecognized);
          }
        }
        catch(OperationCanceledException)
        {
          Trace.TraceWarning("Operation Canceled Exception!");
        }
        catch(Exception ex)
        {
          Trace.TraceError($"Turning exception into error response: {ex}");
          _frameOut.WriteErrorResponse(ex);
        }
        if(!_stopRequest.IsCancellationRequested)
        {
          await connection.SendFrameAsync(_frameOut, _stopRequest.Token);
        }
      }
    }
  }

  private Task HandleKeepAlive(MessageFrameIn frameIn, MessageFrameOut frameOut)
  {
    frameOut.WriteNoContentMessage(MessageCodes.KeepAlive);
    return Task.CompletedTask;
  }
}
