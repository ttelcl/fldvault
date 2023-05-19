/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto;

/// <summary>
/// Wraps a primitive array that is erased when disposed
/// </summary>
public class CryptoBuffer<T>: IDisposable where T : struct
{
  private readonly T[] _buffer;
  private bool _disposed;

  /// <summary>
  /// Create a new CryptoBuffer of the specified size
  /// </summary>
  public CryptoBuffer(int size)
  {
    _buffer = new T[size];
    _disposed = false;
  }

  
  /// <summary>
  /// Create a new CryptoBuffer and copy the source as its content
  /// </summary>
  public CryptoBuffer(ReadOnlySpan<T> source)
    : this(source.Length)
  {
    source.CopyTo(_buffer);
  }

  /// <summary>
  /// Create a new CryptoBuffer copying the content of the source
  /// subsequently clears the source buffer
  /// </summary>
  public static CryptoBuffer<T> FromSpanClear(Span<T> source)
  {
    var b = new CryptoBuffer<T>(source);
    source.Clear();
    return b;
  }

  /// <summary>
  /// Erase the buffer to all zeros
  /// </summary>
  public void Clear()
  {
    // allow even if disposed
    Array.Clear(_buffer);
  }

  /// <summary>
  /// Expose the buffer as a Span{T}
  /// </summary>
  public Span<T> Span()
  {
    if(_disposed)
    {
      throw new ObjectDisposedException(GetType().Name);
    }
    return _buffer; 
  }

  /// <summary>
  /// The number of elements in the buffer
  /// </summary>
  public int Length { get =>  _buffer.Length; }

  /// <summary>
  /// Expose a slice of the buffer as a Span{T}
  /// </summary>
  public Span<T> Span(int start, int length)
  {
    if(_disposed)
    {
      throw new ObjectDisposedException(GetType().Name);
    }
    return new Span<T>(_buffer, start, length);
  }

  /// <summary>
  /// Clear the buffer
  /// </summary>
  public void Dispose()
  {
    Clear();
    _disposed = true;
  }
}
