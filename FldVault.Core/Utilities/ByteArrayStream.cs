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

namespace FldVault.Core.Utilities;

/// <summary>
/// A simple wrapper around a byte memory block that allows writing to that
/// array or reading from that array, similar but simpler than MemoryStream.
/// </summary>
/// <remarks>
/// <para>
/// This implementation includes a workaround helper to fix a bug
/// in ICSharpCode.SharpZipLib.BZip2.BZip2OutputStream. That bug causes
/// exceptions thrown by the destination stream (presumably this stream)
/// sometimes to be silently eaten by that compressor. The hack here allows
/// detecting that case, by setting the <see cref="OverflowDetected"/>
/// property when a write overflows the buffer.
/// </para>
/// </remarks>
public class ByteArrayStream: Stream
{
  private int _position;
  private int _length;

  /// <summary>
  /// Create a new ByteArrayWriteStream
  /// </summary>
  /// <param name="buffer">
  /// The buffer to write to or read from
  /// </param>
  /// <param name="write">
  /// Determines the initial <see cref="Length"/> of this stream.
  /// When true, it is assumed the buffer is empty, intended for writing
  /// and Length is set to 0.
  /// When false, it is assumed the buffer is full, intended for reading,
  /// and Length is set to Capacity.
  /// </param>
  public ByteArrayStream(byte[] buffer, bool write)
  {
    Buffer = buffer;
    _length = write ? 0 : Buffer.Length;
  }

  /// <summary>
  /// The buffer being filled
  /// </summary>
  public byte[] Buffer { get; init; }

  /// <summary>
  /// The capacity of this stream
  /// </summary>
  public int Capacity { get => Buffer.Length; }

  /// <summary>
  /// This value is set to true when an attempt is made to write
  /// to this buffer when it is full.
  /// </summary>
  public bool OverflowDetected { get; set; }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override bool CanRead { get => true; }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override bool CanSeek { get => true; }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override bool CanWrite { get => true; }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override long Length { get => _length; }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override void SetLength(long value)
  {
    if(value < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(value), "Length cannot be negative");
    }
    if(value > Capacity)
    {
      throw new ArgumentOutOfRangeException(nameof(value), "Length cannot exceed the Capacity");
    }
    var length = (int)value;
    var old = _length;
    _length = length;
    if(old > length)
    {
      Array.Clear(Buffer, length, old - length);
    }
  }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override long Position {
    get => _position;
    set {
      if(value < 0)
      {
        throw new ArgumentOutOfRangeException(nameof(value), "Cannot set negative position");
      }
      if(value > _length)
      {
        throw new ArgumentOutOfRangeException(nameof(value), "Cannot set the position beyond the current length");
      }
      else
      {
        _position = (int)value;
      }
    }
  }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override void Flush()
  {
    // do nothing
  }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override long Seek(long offset, SeekOrigin origin)
  {
    Position = origin switch {
      SeekOrigin.Begin => offset,
      SeekOrigin.Current => offset + Position,
      SeekOrigin.End => offset + Length,
      _ => throw new InvalidOperationException("Invalid SeekOrigin"),
    };
    return Position;
  }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override void Write(byte[] buffer, int offset, int count)
  {
    while(count > 0)
    {
      WriteByte(buffer[offset++]);
      count--;
    }
  }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override int Read(byte[] buffer, int offset, int count)
  {
    var available = _length - _position;
    count = count <= available ? count : available;
    var n = count;
    while(n > 0)
    {
      buffer[offset++] = (byte)ReadByte();
      n--;
    }
    return count;
  }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override void WriteByte(byte value)
  {
    if(_position == _length)
    {
      if(_length >= Capacity)
      {
        OverflowDetected = true;
        throw new InvalidOperationException(
          "The buffer is full");
      }
      Buffer[_length++] = value;
      _position = _length;
    }
    else
    {
      Buffer[_position++] = value;
    }
  }

  /// <summary>
  /// <inheritdoc/>
  /// </summary>
  public override int ReadByte()
  {
    if(Position >= Length)
    {
      return -1;
    }
    else
    {
      return Buffer[Position++];
    }
  }
}
