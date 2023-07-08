/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Utilities;

namespace FldVault.Core.Crypto;

/// <summary>
/// A specialized CryptoBuffer{byte} that adds the functionality to
/// create a readable or writeable stream of the buffer
/// </summary>
public class ByteCryptoBuffer: CryptoBuffer<byte>
{
  /// <summary>
  /// Create a new ByteCryptoBuffer
  /// </summary>
  public ByteCryptoBuffer(int size)
    : base(size)
  {
  }

  /// <summary>
  /// Returns a writable stream backed by the underlying buffer.
  /// The initial position is 0. The initial length is 0.
  /// The capacity (maximum length) is the size of the buffer.
  /// </summary>
  public ByteArrayStream WriteableStream()
  {
    return new ByteArrayStream(RawBuffer, true);
  }

  /// <summary>
  /// Returns a read-only stream backed by an initial segment of the underlying buffer.
  /// </summary>
  /// <param name="length">
  /// The length of the initial segment to expose in the returned stream
  /// </param>
  public ByteArrayStream ReadableStream(int length)
  {
    var stream = new ByteArrayStream(RawBuffer, false);
    stream.SetLength(length);
    return stream;
  }

  /// <summary>
  /// Returns a read-only stream backed by the entire underlying buffer.
  /// </summary>
  public ByteArrayStream ReadableStream()
  {
    return new ByteArrayStream(RawBuffer, false);
  }

}
