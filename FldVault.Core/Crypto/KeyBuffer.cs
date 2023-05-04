/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto
{
  /// <summary>
  /// Basic key functionality (intended for derivation, but usable as-is as well)
  /// </summary>
  public class KeyBuffer: IDisposable
  {
    private readonly CryptoBuffer<byte> _buffer;

    /// <summary>
    /// Create a new KeyBase, copying the source span into an equal-sized
    /// key buffer. Once created the key buffer is immutable until disposed.
    /// </summary>
    /// <param name="source">
    /// The source for the bytes to copy into this KeyBuffer
    /// </param>
    public KeyBuffer(ReadOnlySpan<byte> source)
    {
      _buffer = new CryptoBuffer<byte>(source.Length);
      source.CopyTo(_buffer.Span());
    }

    /// <summary>
    /// Return a view on the stored key bytes
    /// </summary>
    public ReadOnlySpan<byte> Bytes { get => _buffer.Span(); }

    /// <summary>
    /// Erase the buffer
    /// </summary>
    public virtual void Dispose()
    {
      _buffer.Dispose();
    }
  }
}
