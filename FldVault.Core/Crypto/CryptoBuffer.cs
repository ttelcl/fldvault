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

  /// <summary>
  /// Create a new CryptoBuffer
  /// </summary>
  public CryptoBuffer(int size)
  {
    _buffer = new T[size];
  }

  /// <summary>
  /// Erase the buffer to all zeros
  /// </summary>
  public void Clear()
  {
    Array.Clear(_buffer);
  }

  /// <summary>
  /// Expose the buffer as a Span{T}
  /// </summary>
  public Span<T> Span()
  {
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
    return new Span<T>(_buffer, start, length);
  }

  /// <summary>
  /// Clear the buffer
  /// </summary>
  public void Dispose()
  {
    Clear();
  }
}
