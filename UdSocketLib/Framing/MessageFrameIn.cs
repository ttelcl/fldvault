/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UdSocketLib.Framing;

/// <summary>
/// A message frame buffer to store a message frame read from a stream
/// </summary>
/// <remarks>
/// <para>
/// The reading API comes in two variants: a series of ReadXXX() methods
/// that return the value that was read from the frame and a series of
/// TakeXXX() methods that return the value in an out parameter, and return
/// this MessageFrameIn instance, enabling a Fluent API.
/// </para>
/// </remarks>
public class MessageFrameIn: IDisposable
{
  private byte[] _bytes;
  private bool _disposed;

  /// <summary>
  /// Create a new MessageFrameIn. Call <see cref="Fill(Stream)"/> to fill it
  /// </summary>
  /// <param name="capacity">
  /// The maximum content size (in the range 256 to 0xFFFE).
  /// Note that if necessary, <see cref="Fill(Stream)"/> can resize the buffer.
  /// </param>
  public MessageFrameIn(int capacity = 0xFFFE)
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
  /// The position where to read next.
  /// </summary>
  public int Position { get; private set; }

  /// <summary>
  /// The filled length of the buffer
  /// </summary>
  public int Length { get; private set; }

  /// <summary>
  /// The frame content capacity (maximum for <see cref="Length"/>)
  /// If necessary, <see cref="Fill(Stream)"/> can increase this.
  /// </summary>
  public int Capacity { get; private set; }

  /// <summary>
  /// The number of unused bytes remaining in the buffer
  /// </summary>
  public int Space => Length - Position;

  /// <summary>
  /// Make sure that <see cref="Space"/> is 0. If not, an
  /// exception is thrown.
  /// </summary>
  public void EnsureFullyRead()
  {
    EnsureNotDisposed();
    if(Space != 0)
    {
      throw new InvalidOperationException(
        $"Expecting the buffer to have been fully read, but there are {Space} bytes left.");
    }
  }

  /// <summary>
  /// Return the next slice of <paramref name="count"/> bytes.
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
  /// Return the next slice of <paramref name="count"/> bytes in
  /// <paramref name="span"/> (Fluent API)
  /// </summary>
  public MessageFrameIn TakeSlice(int count, out Span<byte> span)
  {
    EnsureNotDisposed();
    CheckSpace(count);
    span = _bytes.AsSpan(Position, count);
    Position += count;
    return this;
  }

  /// <summary>
  /// Read the next byte from this buffer
  /// </summary>
  public byte ReadByte()
  {
    CheckSpace(1);
    return _bytes[Position++];
  }

  /// <summary>
  /// Read the next byte from this buffer
  /// </summary>
  public MessageFrameIn TakeByte(out byte value)
  {
    CheckSpace(1);
    value = _bytes[Position++];
    return this;
  }

  /// <summary>
  /// Read the next unsigned 16 bit integer
  /// </summary>
  public ushort ReadU16()
  {
    return BinaryPrimitives.ReadUInt16LittleEndian(NextSlice(2));
  }

  /// <summary>
  /// Read the next unsigned 16 bit integer
  /// </summary>
  public MessageFrameIn TakeU16(out ushort value)
  {
    value = BinaryPrimitives.ReadUInt16LittleEndian(NextSlice(2));
    return this;
  }

  /// <summary>
  /// Read the next unsigned 32 bit integer
  /// </summary>
  public uint ReadU32()
  {
    return BinaryPrimitives.ReadUInt32LittleEndian(NextSlice(4));
  }

  /// <summary>
  /// Read the next unsigned 32 bit integer
  /// </summary>
  public MessageFrameIn TakeU32(out uint value)
  {
    value = BinaryPrimitives.ReadUInt32LittleEndian(NextSlice(4));
    return this;
  }

  /// <summary>
  /// Read the next signed 32 bit integer
  /// </summary>
  public int ReadI32()
  {
    return BinaryPrimitives.ReadInt32LittleEndian(NextSlice(4));
  }

  /// <summary>
  /// Read the next signed 32 bit integer.
  /// </summary>
  public MessageFrameIn TakeI32(out int value)
  {
    value = BinaryPrimitives.ReadInt32LittleEndian(NextSlice(4));
    return this;
  }

  /// <summary>
  /// Read the next signed 32 bit integer and run the validator on it.
  /// </summary>
  public MessageFrameIn ValidateI32(Action<int> validator)
  {
    var value = BinaryPrimitives.ReadInt32LittleEndian(NextSlice(4));
    validator.Invoke(value);
    return this;
  }

  /// <summary>
  /// Read the next signed 32 bit integer and validate that the value
  /// equals <paramref name="expectedValue"/>. If not, throw an exception
  /// using the specified error message.
  /// </summary>
  public MessageFrameIn ValidateI32(int expectedValue, string errorMessage)
  {
    var value = BinaryPrimitives.ReadInt32LittleEndian(NextSlice(4));
    if(value != expectedValue)
    {
      throw new InvalidOperationException(errorMessage);
    }
    return this;
  }

  /// <summary>
  /// Read the next signed 64 bit integer
  /// </summary>
  public long ReadI64()
  {
    return BinaryPrimitives.ReadInt64LittleEndian(NextSlice(8));
  }

  /// <summary>
  /// Read the next signed 64 bit integer
  /// </summary>
  public MessageFrameIn TakeI64(out long value)
  {
    value = BinaryPrimitives.ReadInt64LittleEndian(NextSlice(8));
    return this;
  }

  /// <summary>
  /// Read the next GUID
  /// </summary>
  public Guid ReadGuid()
  {
    return new Guid(NextSlice(16));
  }

  /// <summary>
  /// Read the next GUID
  /// </summary>
  public MessageFrameIn TakeGuid(out Guid value)
  {
    value = new Guid(NextSlice(16));
    return this;
  }

  /// <summary>
  /// Read the next UTC DateTime (via its EpochTicks value)
  /// </summary>
  public DateTime ReadUtcDateTime()
  {
    var et = ReadI64();
    var ticks = et + 0x089F7FF5F7B58000L; // ticks at 1970-01-01 00:00:00 Z
    return new DateTime(ticks, DateTimeKind.Utc);
  }

  /// <summary>
  /// Read the next UTC DateTime (via its EpochTicks value)
  /// </summary>
  public MessageFrameIn TakeUtcDateTime(out DateTime value)
  {
    value = ReadUtcDateTime();
    return this;
  }

  /// <summary>
  /// Read a variable length unsigned integer
  /// </summary>
  public ulong ReadVarInt()
  {
    ulong value = 0UL;
    int shift = 0;
    byte b;
    do
    {
      b = ReadByte();
      var bb = (ulong)(b & 0x7F);
      value |= bb << shift;
      shift += 7;
    } while((b & 0x80) == 0x80);
    return value;
  }

  /// <summary>
  /// Read a variable length unsigned integer
  /// </summary>
  public MessageFrameIn TakeVarInt(out ulong value)
  {
    value = ReadVarInt();
    return this;
  }

  /// <summary>
  /// Read a variable length signed integer
  /// </summary>
  public long ReadSignedVarInt()
  {
    var ul = ReadVarInt();
    var l0 = (long)(ul >> 1);
    return (ul & 0x1UL) == 0 ? l0 : -1L - l0;
  }

  /// <summary>
  /// Read a variable length signed integer
  /// </summary>
  public MessageFrameIn TakeSignedVarInt(out long value)
  {
    value = ReadSignedVarInt();
    return this;
  }

  /// <summary>
  /// Read the next string
  /// </summary>
  public string ReadString()
  {
    int length = ReadU16();
    if(length == 0)
    {
      return string.Empty;
    }
    return Encoding.UTF8.GetString(NextSlice(length));
  }

  /// <summary>
  /// Read the next string
  /// </summary>
  public MessageFrameIn TakeString(out string value)
  {
    value = ReadString();
    return this;
  }

  /// <summary>
  /// Read the next blob from the frame, by first reading
  /// the two length bytes, then return a reference to the blob
  /// of that length. Beware that unlike other ReadXXX methods,
  /// the returned value is only valid while this Frame buffer is
  /// valid and then only until the next <see cref="Fill(Stream)"/>
  /// or <see cref="Clear()"/>.
  /// </summary>
  public ReadOnlySpan<byte> ReadBlob()
  {
    var size = ReadU16();
    return NextSlice(size);
  }

  /// <summary>
  /// Read the next blob from the frame, by first reading
  /// the two length bytes, then return a reference to the blob
  /// of that length. Beware that unlike other TakeXXX methods,
  /// the returned value is only valid while this Frame buffer is
  /// valid and then only until the next <see cref="Fill(Stream)"/>
  /// or <see cref="Clear()"/>.
  /// </summary>
  public MessageFrameIn TakeBlob(out ReadOnlySpan<byte> blob)
  {
    var size = ReadU16();
    blob = NextSlice(size);
    return this;
  }

  /// <summary>
  /// Clear the buffer and try to fill it from a Stream.
  /// Returns true on success, false on EOF (or upon reading the termination marker).
  /// </summary>
  /// <param name="stream">
  /// The stream to read from. This may be a non-file stream: partial
  /// reads are handled gracefully by this method.
  /// </param>
  /// <returns>
  /// True if a frame was successfully read from the stream,
  /// false on true EOF or on reading the termination marker.
  /// </returns>
  public bool Fill(Stream stream)
  {
    EnsureNotDisposed();
    Clear();
    var lsb = stream.ReadByte();
    if(lsb < 0)
    {
      return false;
    }
    var msb = stream.ReadByte();
    if(msb < 0)
    {
      throw new EndOfStreamException(
        "Unexpected end of input");
    }
    var size = (msb << 8) + lsb;
    if(size > 0xFFFE)
    {
      // In other words: size == 0xFFFF.
      // That's the one invalid langth value, used as termination marker.
      // Expect no more input.
      return false;
    }
    if(size > Capacity)
    {
      // resize!
      Array.Clear(_bytes);
      Capacity = size;
      _bytes = new byte[Capacity];
    }
    Length = size;
    var pending = size;
    while(pending > 0)
    {
      // Expect the reading to *not* complete in one read call, as it would
      // do for normal files.
      var remainingSlice = _bytes.AsSpan(Length - pending, pending);
      var n = stream.Read(remainingSlice);
      if(n == 0)
      {
        throw new EndOfStreamException(
          "Unexpected end of input");
      }
      pending -= n;
    }
    return true;
  }

  /// <summary>
  /// Clear the buffer and try to fill it asynchronously from a Socket.
  /// Returns true on success, false on EOF (or upon reading the termination marker).
  /// </summary>
  /// <param name="socket">
  /// The socket to read from
  /// </param>
  /// <param name="cancellationToken">
  /// The cancellation token
  /// </param>
  public async Task<bool> FillAsync(Socket socket, CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    Clear();

    var lengthBuffer = new byte[2];
    if(!await socket.TryFullyReceiveAsync(lengthBuffer, cancellationToken))
    {
      // the other side closed the socket
      return false;
    }
    var length = BinaryPrimitives.ReadUInt16LittleEndian(lengthBuffer);
    if(length > 0xFFFE)
    {
      // abort marker
      return false;
    }
    if(length > Capacity)
    {
      // resize!
      Array.Clear(_bytes);
      Capacity = length;
      _bytes = new byte[Capacity];
    }
    if(length > 0)
    {
      var m = new Memory<byte>(_bytes, 0, length);
      var ok = await socket.TryFullyReceiveAsync(m, cancellationToken);
      if(ok)
      {
        Length = length;
        return true;
      }
      else
      {
        throw new EndOfStreamException("Unexpected end of socket stream");
      }
    }
    return true; // empty message
  }

  /// <summary>
  /// Clear the buffer and try to fill it asynchronously from a Socket.
  /// Returns true on success, false on EOF (or upon reading the termination marker).
  /// </summary>
  /// <param name="socket">
  /// The socket to read from
  /// </param>
  public bool FillSync(Socket socket)
  {
    EnsureNotDisposed();
    Clear();

    Span<byte> lengthBuffer = stackalloc byte[2];
    if(!socket.TryFullyReceiveSync(lengthBuffer))
    {
      // the other side closed the socket
      return false;
    }
    var length = BinaryPrimitives.ReadUInt16LittleEndian(lengthBuffer);
    if(length > 0xFFFE)
    {
      // abort marker
      return false;
    }
    if(length > Capacity)
    {
      // resize!
      Array.Clear(_bytes);
      Capacity = length;
      _bytes = new byte[Capacity];
    }
    if(length > 0)
    {
      var ok = socket.TryFullyReceiveSync(_bytes.AsSpan(0, length));
      if(ok)
      {
        Length = length;
        return true;
      }
      else
      {
        throw new EndOfStreamException("Unexpected end of socket stream");
      }
    }
    return true; // empty message
  }

  /// <summary>
  /// Rewind this buffer, leaving the content and length as-is,
  /// but reseting the position to the start.
  /// </summary>
  public MessageFrameIn Rewind()
  {
    Position = 0;
    return this;
  }

  /// <summary>
  /// Clear the buffer
  /// </summary>
  public void Clear()
  {
    Position = 0;
    Length = 0;
    Array.Clear(_bytes);
  }

  private void CheckSpace(int readSize)
  {
    if(readSize > Space)
    {
      throw new InvalidOperationException(
        "Unsufficient bytes present in frame buffer");
    }
  }

  private void EnsureNotDisposed()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
  }

  /// <summary>
  /// Clear the buffer
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
