/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UdSocketLib.Framing;

/// <summary>
/// Buffers a message frame to be output.
/// </summary>
public class MessageFrameOut: IDisposable
{
  private readonly byte[] _bytes;
  private bool _disposed;

  /// <summary>
  /// Create a new MessageFrame
  /// </summary>
  /// <param name="capacity">
  /// The buffer capacity, in the range 256 - 65534 (0xFFFE).
  /// </param>
  public MessageFrameOut(int capacity = 0xFFFE)
  {
    if(capacity < 256)
    {
      throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 256");
    }
    if(capacity > 0xFFFE)
    {
      throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at most 64Kb - 2");
    }
    Capacity = capacity;
    _bytes = new byte[capacity];
    Position = 0;
  }

  /// <summary>
  /// The length of the message frame content. That's also the position where to write next.
  /// </summary>
  public int Position { get; private set; }

  /// <summary>
  /// The frame content capacity
  /// </summary>
  public int Capacity { get; init; }

  /// <summary>
  /// The number of unused bytes remaining in the buffer
  /// </summary>
  public int Space => Capacity - Position;

  /// <summary>
  /// Append the specified bytes at the end of the buffer.
  /// To write a blob of bytes together with their length, use 
  /// <see cref="AppendBlob(ReadOnlySpan{byte})"/> instead.
  /// </summary>
  /// <param name="bytes">
  /// The bytes to append. Maximum length is <see cref="Space"/>
  /// </param>
  public MessageFrameOut AppendBytes(ReadOnlySpan<byte> bytes)
  {
    bytes.CopyTo(NextSlice(bytes.Length));
    return this;
  }

  /// <summary>
  /// Return the next slice of <paramref name="count"/> bytes and premptively mark it
  /// as appended (advancing <see cref="Position"/>)
  /// </summary>
  /// <param name="count">
  /// The number of bytes in the slice
  /// </param>
  /// <returns>
  /// The slice of <paramref name="count"/> bytes.
  /// </returns>
  public Span<byte> NextSlice(int count)
  {
    EnsureNotDisposed();
    CheckSpace(count);
    var span = _bytes.AsSpan(Position, count);
    Position += count;
    return span;
  }

  /// <summary>
  /// Append a byte
  /// </summary>
  public MessageFrameOut AppendByte(byte value)
  {
    NextSlice(1)[0] = value;
    return this;
  }

  /// <summary>
  /// Append an unsigned 16 bit integer
  /// </summary>
  public MessageFrameOut AppendU16(ushort value)
  {
    BinaryPrimitives.WriteUInt16LittleEndian(NextSlice(2), value);
    return this;
  }

  /// <summary>
  /// Append an unsigned 32 bit integer
  /// </summary>
  public MessageFrameOut AppendU32(uint value)
  {
    BinaryPrimitives.WriteUInt32LittleEndian(NextSlice(4), value);
    return this;
  }

  /// <summary>
  /// Append a signed 32 bit integer
  /// </summary>
  public MessageFrameOut AppendI32(int value)
  {
    BinaryPrimitives.WriteInt32LittleEndian(NextSlice(4), value);
    return this;
  }

  /// <summary>
  /// Append a signed 64 bit integer
  /// </summary>
  public MessageFrameOut AppendI64(long value)
  {
    BinaryPrimitives.WriteInt64LittleEndian(NextSlice(8), value);
    return this;
  }

  /// <summary>
  /// Append a GUID
  /// </summary>
  public MessageFrameOut AppendGuid(Guid guid)
  {
    var slice = NextSlice(16);
    if(!guid.TryWriteBytes(slice))
    {
      throw new InvalidOperationException( // this should be impossible
        "Internal error: Failed to write GUID");
    }
    return this;
  }

  /// <summary>
  /// Append the UTC datetime in Epoch Ticks form
  /// </summary>
  public MessageFrameOut AppendUtcDateTime(DateTime utc)
  {
    if(utc.Kind != DateTimeKind.Utc)
    {
      throw new ArgumentOutOfRangeException(nameof(utc), "Expecting a UTC DateTime value");
    }
    var et = utc.Ticks - 0x089F7FF5F7B58000L; // ticks since 1970-01-01 00:00:00 Z
    return AppendI64(et);
  }

  /// <summary>
  /// Append an unsigned integer in a variable length form
  /// Values closer to 0 are encoded shorter.
  /// </summary>
  public MessageFrameOut AppendVarInt(ulong value)
  {
    EnsureNotDisposed();
    while(true)
    {
      var b = (byte)(value & 0x7FUL);
      value >>= 7;
      if(value > 0UL)
      {
        AppendByte((byte)(b | 0x80));
      }
      else
      {
        AppendByte(b);
        break;
      }
    }
    return this;
  }

  /// <summary>
  /// Append a signed integer in a variable length form.
  /// Values closer to 0 are encoded shorter.
  /// </summary>
  public MessageFrameOut AppendSignedVarInt(long value)
  {
    var uv = value < 0 ? ((ulong)(-1L - value) << 1) + 1UL : (ulong)value << 1;
    return AppendVarInt(uv);
  }

  /// <summary>
  /// Append a string
  /// </summary>
  public MessageFrameOut AppendString(string value)
  {
    EnsureNotDisposed();
    var byteCount = Encoding.UTF8.GetByteCount(value);
    CheckSpace(byteCount + 2);
    AppendU16((ushort)byteCount);
    if(byteCount > 0)
    {
      var slice = NextSlice(byteCount);
      Encoding.UTF8.GetBytes(value, slice);
    }
    return this;
  }

  /// <summary>
  /// Append the specified bytes at the end of the buffer,
  /// prefixed by their length as a two byte unsigned integer.
  /// </summary>
  public MessageFrameOut AppendBlob(ReadOnlySpan<byte> blob)
  {
    CheckSpace(blob.Length + 2);
    AppendU16((ushort)blob.Length);
    if(blob.Length > 0)
    {
      AppendBytes(blob);
    }
    return this;
  }

  /// <summary>
  /// Write the frame content to a stream and clear the frame to empty
  /// </summary>
  public void Emit(Stream stream)
  {
    Span<byte> header = stackalloc byte[2];
    BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)Position);
    stream.Write(header);
    stream.Write(_bytes, 0, Position);
    Clear();
  }

  /// <summary>
  /// Send the frame content asynchronously to a socket and clear the frame afterward
  /// </summary>
  public async Task EmitAsync(Socket socket, CancellationToken cancellationToken)
  {
    var header = new byte[2];
    BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)Position);
    await socket.TryFullySendAsync(header, cancellationToken);
    if(Position > 0)
    {
      var m = new ReadOnlyMemory<byte>(_bytes, 0, Position);
      await socket.TryFullySendAsync(m, cancellationToken);
    }
    Clear();
  }

  /// <summary>
  /// Send the frame content synchronously to a socket and clear the frame afterward
  /// </summary>
  public void EmitSync(Socket socket)
  {
    var header = new byte[2];
    BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)Position);
    socket.TryFullySendSync(header);
    if(Position > 0)
    {
      socket.TryFullySendSync(_bytes.AsSpan(0, Position));
    }
    Clear();
  }

  /// <summary>
  /// Clear the buffer
  /// </summary>
  public MessageFrameOut Clear()
  {
    Position = 0;
    Array.Clear(_bytes);
    return this;
  }

  private void CheckSpace(int writeSize)
  {
    if(writeSize > Space)
    {
      throw new InvalidOperationException(
        "Unsufficient buffer space in frame");
    }
  }

  private void EnsureNotDisposed()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
  }

  /// <summary>
  /// Erase the buffer
  /// </summary>
  public void Dispose()
  {
    if(!_disposed)
    {
      Clear();
      _disposed = true;
    }
  }
}
