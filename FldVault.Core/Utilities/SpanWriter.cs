/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Utilities;

/// <summary>
/// Cursor-like utilty class for writing binary values
/// into a span of bytes. Unless specified otherwise values
/// are written in Little Endian order.
/// The calls support a fluent style.
/// </summary>
/// <remarks>
/// <para>
/// This class keeps track of the
/// write position. It of course can not keep track
/// of the span to write to, so that has to be passed
/// to each call.
/// </para>
/// </remarks>
public class SpanWriter
{
  /// <summary>
  /// Create a new SpanWriter
  /// </summary>
  public SpanWriter()
  {
  }

  /// <summary>
  /// The current position
  /// </summary>
  public int Position { get; set; }

  /// <summary>
  /// Write a byte
  /// </summary>
  public SpanWriter WriteByte(Span<byte> span, byte b)
  {
    span[Position] = b;
    Position++; // explicitly *after* the assignment so it is NOT called if the assignment fails
    return this;
  }

  /// <summary>
  /// Write an unsigned 16 bit integer
  /// </summary>
  public SpanWriter WriteU16(Span<byte> span, ushort u16)
  {
    BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(Position, 2), u16);
    Position += 2;
    return this;
  }

  /// <summary>
  /// Write an signed 16 bit integer
  /// </summary>
  public SpanWriter WriteI16(Span<byte> span, short i16)
  {
    BinaryPrimitives.WriteInt16LittleEndian(span.Slice(Position, 2), i16);
    Position += 2;
    return this;
  }

  /// <summary>
  /// Write an unsigned 32 bit integer
  /// </summary>
  public SpanWriter WriteU32(Span<byte> span, uint u32)
  {
    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(Position, 4), u32);
    Position += 4;
    return this;
  }

  /// <summary>
  /// Write an signed 32 bit integer
  /// </summary>
  public SpanWriter WriteI32(Span<byte> span, int i32)
  {
    BinaryPrimitives.WriteInt32LittleEndian(span.Slice(Position, 4), i32);
    Position += 4;
    return this;
  }

  /// <summary>
  /// Write an unsigned 64 bit integer
  /// </summary>
  public SpanWriter WriteU64(Span<byte> span, ulong u64)
  {
    BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(Position, 8), u64);
    Position += 8;
    return this;
  }

  /// <summary>
  /// Write an signed 64 bit integer
  /// </summary>
  public SpanWriter WriteI64(Span<byte> span, long i64)
  {
    BinaryPrimitives.WriteInt64LittleEndian(span.Slice(Position, 8), i64);
    Position += 8;
    return this;
  }

  /// <summary>
  /// Write a Guid (as a 128 bit binary value)
  /// </summary>
  public SpanWriter WriteGuid(Span<byte> span, Guid guid)
  {
    if(!guid.TryWriteBytes(span.Slice(Position, 16)))
    {
      throw new ArgumentOutOfRangeException(nameof(guid), "Not enough space to write the 16 bytes of a GUID");
    }
    Position += 16;
    return this;
  }

  /// <summary>
  /// Write the content of another span to the targte span.
  /// </summary>
  /// <param name="span">
  /// The target span to write to
  /// </param>
  /// <param name="bytes">
  /// The source span providing the content to write
  /// </param>
  /// <returns></returns>
  public SpanWriter WriteSpan(Span<byte> span, ReadOnlySpan<byte> bytes)
  {
    bytes.CopyTo(span.Slice(Position, bytes.Length));
    Position += bytes.Length;
    return this;
  }

  /// <summary>
  /// Return the slice of the span that starts at the current position
  /// of the specified length and advance the position
  /// </summary>
  public Span<byte> TakeSlice(Span<byte> span, int byteCount)
  {
    var ret = span.Slice(Position, byteCount);
    Position += byteCount;
    return ret;
  }

  /// <summary>
  /// Test if the position of this writer exactly matches the end of the span.
  /// </summary>
  public bool IsFull(Span<byte> span)
  {
    if(Position > span.Length)
    {
      throw new ArgumentOutOfRangeException(nameof(span), "This SpanWriter already points beyond the span");
    }
    return Position == span.Length;
  }

  /// <summary>
  /// Test if the position of this writer exactly matches the end of the span,
  /// and throw an exception if it doesn't. This method is intended to be used to
  /// terminate chains of fluent calls.
  /// </summary>
  public void CheckFull(Span<byte> span)
  {
    if(Position == span.Length)
    {
      return;
    }
    if(Position < span.Length)
    {
      throw new ArgumentOutOfRangeException(nameof(span), $"There are {span.Length - Position} unfilled bytes in the span");
    }
    if(Position > span.Length)
    {
      // Unlikely to happen, unless you pass the wrong span
      throw new ArgumentOutOfRangeException(nameof(span), $"The writer overshot the span capacity by {span.Length - Position} bytes");
    }
  }

  /// <summary>
  /// Write an UTC time stamp as a the (64 bit) number of ticks
  /// since the Unix Epoch.
  /// </summary>
  public SpanWriter WriteEpochTicks(Span<byte> span, DateTime utcTime)
  {
    if(utcTime.Kind != DateTimeKind.Utc)
    {
      throw new ArgumentOutOfRangeException(nameof(utcTime), "Expecting an UTC time as argument");
    }
    var et = utcTime.Ticks - EpochTicks;
    return WriteI64(span, et);
  }

  /// <summary>
  /// The number of .NET ticks at 1970-01-01 00:00:00 UTC
  /// </summary>
  public const long EpochTicks = 0x089F7FF5F7B58000L;


}