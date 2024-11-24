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

using FldVault.Core.Vaults;
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
  /// <param name="callbacks">
  /// The host API providing UI callbacks
  /// </param>
  /// <param name="owner">
  /// The UPI owning this server logic
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
    IKeyServerHost callbacks,
    IKeyServerUpi owner,
    KeyServerService api,
    UdSocketListener listener,
    KeyStateStore keyStates)
  {
    Callbacks = callbacks;
    Api = api;
    KeyStates = keyStates;
    Owner = owner;
    _listener = listener;
    _serverStarted = new TaskCompletionSource();
    _serverCompleted = new TaskCompletionSource();
    _stopRequest = new CancellationTokenSource();
    _frameIn = new MessageFrameIn();
    _frameOut = new MessageFrameOut();
    _handlers = new() {
      [MessageCodes.KeepAlive] = HandleKeepAlive,
      [KeyServerMessages.KeyForFileCode] = HandleKeyForFile,
      [KeyServerMessages.KeyUploadCode] = HandleKeyUpload,
      [KeyServerMessages.KeyRequestCode] = HandleKeyRequest,
      [KeyServerMessages.KeyPresenceListCode] = HandleKeyPresenceList,
      [KeyServerMessages.ServerDiagnosticsCode] = HandleServerDiagnostics,
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
  public IKeyServerHost Callbacks { get; }

  /// <summary>
  /// The UPI handling the lifetime of this object
  /// </summary>
  public IKeyServerUpi Owner { get; }

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
          if(_handlers.TryGetValue(messageCode, out var handler))
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

  /// <summary>
  /// Handle a "keep alive" message. This message is mostly used for
  /// debugging, since there is no network connection involved.
  /// </summary>
  private Task HandleKeepAlive(MessageFrameIn frameIn, MessageFrameOut frameOut)
  {
    frameOut.WriteNoContentMessage(MessageCodes.KeepAlive);
    return Task.CompletedTask;
  }

  /// <summary>
  /// Handle a "key for file" request. This request has two purposes: it
  /// acts as a key request using a target file as argument instead of a key ID.
  /// It is also used for the side effect of associating a file name with
  /// a key. And if the key isn't available yet, tagging the key in the UI as
  /// requiring attention.
  /// </summary>
  private async Task HandleKeyForFile(MessageFrameIn frameIn, MessageFrameOut frameOut)
  {
    var fileName = frameIn.ReadKeyForFileRequest();
    if(String.IsNullOrEmpty(fileName))
    {
      frameOut.WriteErrorResponse("Missing file name");
      return;
    }
    if(!File.Exists(fileName))
    {
      frameOut.WriteErrorResponse("No such file");
      return;
    }
    if(!Path.IsPathFullyQualified(fileName))
    {
      frameOut.WriteErrorResponse("Expecting an absolute file name");
      return;
    }
    var pkif = PassphraseKeyInfoFile.TryFromFile(fileName);
    if(pkif == null)
    {
      frameOut.WriteErrorResponse("Unrecognized file format");
      return;
    }
    var keyId = pkif.KeyId;
    var state = KeyStates.GetKey(keyId);
    state.AssociateFile(fileName, true);
    var status = state.Status;
    switch(status)
    {
      case KeyStatus.Published:
        {
          state.UseKey(frameOut.WriteKeyResponse);
          // The callback is also used for server notification!
          await Callbacks.KeyLoadRequest(Owner, keyId, status, fileName);
          break;
        }
      case KeyStatus.Seeded:
        {
          state.UseKey(frameOut.WriteKeyResponse); // equivalent to frameOut.WriteKeyResponse(null) plus tracking
          await Callbacks.KeyLoadRequest(Owner, keyId, status, fileName);
          break;
        }
      case KeyStatus.Hidden:
        {
          state.UseKey(frameOut.WriteKeyResponse); // triggers special response (cloaked state)
          await Callbacks.KeyLoadRequest(Owner, keyId, status, fileName);
          break;
        }
      default:
        {
          frameOut.WriteErrorResponse("Internal error - unexpected key status");
          break;
        }
    }
    await Callbacks.KeyStatusChanged(Owner, keyId, status);
  }

  private async Task HandleKeyRequest(MessageFrameIn frameIn, MessageFrameOut frameOut)
  {
    var keyId = frameIn.ReadKeyRequest();
    var state = KeyStates.GetKey(keyId);
    var found = false;
    state.UseKey(bw => {
      found = bw != null;
      frameOut.WriteKeyResponse(bw);
    });
    // sent not only when not found ("load request") but also when served ("serve notification")
    await Callbacks.KeyLoadRequest(Owner, keyId, state.Status, null);
  }

  private async Task HandleKeyUpload(MessageFrameIn frameIn, MessageFrameOut frameOut)
  {
    var keyId = frameIn.ReadKeyUpload(KeyStates.KeyChain);
    var state = KeyStates.GetKey(keyId);
    frameOut.WriteNoContentMessage(KeyServerMessages.KeyUploadedCode);
    await Callbacks.KeyStatusChanged(Owner, keyId, state.Status);
  }

  private async Task HandleKeyPresenceList(MessageFrameIn frameIn, MessageFrameOut frameOut)
  {
    var keysRequested = frameIn.ReadKeyPresence();
    var foundList = new List<Guid>();
    foreach(var key in keysRequested)
    {
      var state = KeyStates.FindKey(key);
      if(state != null)
      {
        if(state.Status == KeyStatus.Published)
        {
          foundList.Add(key);
        }
        else
        {
          await Callbacks.KeyLoadRequest(Owner, key, state.Status, null);
        }
      }
    }
    frameOut.WriteKeyPresence(foundList);
  }

  private Task HandleServerDiagnostics(MessageFrameIn frameIn, MessageFrameOut frameOut)
  {
    frameOut.WriteNoContentMessage(MessageCodes.OkNoContent);
    var states = KeyStates.AllStates;
    Trace.TraceInformation($"Diag: {states.Count} states.");
    foreach(var state in states)
    {
      Trace.TraceInformation($"{state.KeyId} ({state.Status})");
    }
    return Task.CompletedTask;
  }
}
