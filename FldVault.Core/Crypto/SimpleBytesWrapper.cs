/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto;

/// <summary>
/// Implements IBytesWrapper for a simple byte array (for non-secret data)
/// </summary>
public class SimpleBytesWrapper: IBytesWrapper
{
  private readonly byte[] _buffer;

  /// <summary>
  /// Create a new SimpleBytesWrapper
  /// </summary>
  /// <param name="buffer">
  /// The buffer to wrap. Warning: the buffer is not copied.
  /// </param>
  public SimpleBytesWrapper(byte[] buffer)
  {
    _buffer = buffer;
  }

  /// <summary>
  /// Create a new SimpleBytesWrapper wrapping an empty buffer
  /// </summary>
  public SimpleBytesWrapper()
    : this([])
  {
  }

  /// <summary>
  /// A singleton instance of SimpleBytesWrapper wrapping an empty buffer
  /// </summary>
  public static SimpleBytesWrapper Empty { get; } = new SimpleBytesWrapper();

  /// <inheritdoc/>
  public ReadOnlySpan<byte> Bytes => _buffer;

}
