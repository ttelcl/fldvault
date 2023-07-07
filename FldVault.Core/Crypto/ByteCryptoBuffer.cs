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

namespace FldVault.Core.Crypto
{
  /// <summary>
  /// Description of ByteCryptoBuffer
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
    public Stream WriteableStream()
    {
      var stream = new MemoryStream(RawBuffer, true);
      stream.Position = 0L;
      stream.SetLength(0);
      return stream;
    }

    /// <summary>
    /// Returns a read-only stream backed by an initial segment of the underlying buffer.
    /// </summary>
    /// <param name="length">
    /// The length of the initial segment to expose in the returned stream
    /// </param>
    public Stream ReadableStream(int length)
    {
      var stream = new MemoryStream(RawBuffer, 0, length, false);
      return stream;
    }

    /// <summary>
    /// Returns a read-only stream backed by the entire underlying buffer.
    /// </summary>
    public Stream ReadableStream() => ReadableStream(Length);

  }
}
