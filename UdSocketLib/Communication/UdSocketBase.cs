/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UdSocketLib.Communication;

/// <summary>
/// Wraps a Unix Domain socket (base class used for any
/// of the three socket roles)
/// </summary>
public abstract class UdSocketBase: IDisposable
{
  private readonly Socket _socket;
  private bool _disposed;

  /// <summary>
  /// Create a new <see cref="UdSocketBase"/>
  /// </summary>
  internal UdSocketBase(UdSocketService service, Socket socket)
  {
    Service = service;
    _disposed = false;
    _socket = socket;
  }

  /// <summary>
  /// The path to the socket in the filesystem
  /// </summary>
  public UdSocketService Service { get; init; }

  /// <summary>
  /// Get the socket
  /// </summary>
  protected Socket Socket { get => _socket; }

  /// <summary>
  /// True after disposal
  /// </summary>
  public bool IsDisposed => _disposed;  

  /// <summary>
  /// Clean up
  /// </summary>
  protected virtual void Dispose(bool disposing)
  {
    if(!_disposed)
    {
      if(disposing)
      {
        _socket.Close();
      }
      _disposed=true;
    }
  }

  /// <summary>
  /// Cleanup (using the Dispose pattern)
  /// </summary>
  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }
}
