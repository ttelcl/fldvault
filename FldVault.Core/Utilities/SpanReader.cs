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
/// Cursor-like utilty class for reading binary values
/// from a span of bytes. Unless specified otherwise values
/// are read in Little Endian order.
/// The calls support a fluent style. To enable that, the values
/// read are returned as output parameters.
/// </summary>
/// <remarks>
/// <para>
/// This class keeps track of the
/// read position. It of course can not keep track
/// of the span to read from, so that has to be passed
/// to each call.
/// </para>
/// </remarks>
public class SpanReader
{
  /// <summary>
  /// Create a new SpanReader
  /// </summary>
  public SpanReader()
  {
  }

  /// <summary>
  /// The current position
  /// </summary>
  public int Position { get; set; }

  /// <summary>
  /// Read a signed 32 bit integer
  /// </summary>
  public SpanReader ReadI32(ReadOnlySpan<byte> span, out int i32)
  {
    i32 = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(Position, 4));
    Position += 4;
    return this;
  }

  /// <summary>
  /// Read a signed 64 bit integer
  /// </summary>
  public SpanReader ReadI64(ReadOnlySpan<byte> span, out long i64)
  {
    i64 = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(Position, 8));
    Position += 8;
    return this;
  }

  /// <summary>
  /// Read a (128 bit) GUID
  /// </summary>
  public SpanReader ReadGuid(ReadOnlySpan<byte> span, out Guid guid)
  {
    guid = new Guid(span.Slice(Position, 16));
    Position += 16;
    return this;
  }

  /// <summary>
  /// Fill the destination span with bytes from <paramref name="span"/>
  /// at the current position (and advance the postion by the length of 
  /// <paramref name="destination"/>)
  /// </summary>
  public SpanReader ReadSpan(ReadOnlySpan<byte> span, Span<byte> destination)
  {
    span.Slice(Position, destination.Length).CopyTo(destination);
    Position += destination.Length;
    return this;
  }

  /// <summary>
  /// Read a 64-bit epoch-ticks value and return its corresponding
  /// UTC DateTime value.
  /// </summary>
  public SpanReader ReadEpochTicks(ReadOnlySpan<byte> span, out DateTime utcStamp)
  {
    ReadI64(span, out var et);
    utcStamp = EpochTicks.ToUtc(et);
    return this;
  }

  /// <summary>
  /// Return the next <paramref name="count"/> bytes as a slice
  /// (and advance the position by that many bytes)
  /// </summary>
  public ReadOnlySpan<byte> TakeSlice(ReadOnlySpan<byte> span, int count)
  {
    var slice = span.Slice(Position, count);
    Position += count;
    return slice;
  }

  /// <summary>
  /// Check that there are no more bytes in <paramref name="span"/>:
  /// that the length of <paramref name="span"/> equals <see cref="Position"/>.
  /// An <see cref="ArgumentOutOfRangeException"/> is thrown otherwise
  /// </summary>
  /// <exception cref="ArgumentOutOfRangeException">
  /// Thrown if the span length does not exactly match the current position
  /// </exception>
  public void CheckEmpty(ReadOnlySpan<byte> span)
  {
    if(Position == span.Length)
    {
      return;
    }
    if(Position < span.Length)
    {
      throw new ArgumentOutOfRangeException(nameof(span), $"There are {span.Length - Position} bytes remaining in the span");
    }
    if(Position > span.Length)
    {
      // Unlikely to happen, unless you pass the wrong span
      throw new ArgumentOutOfRangeException(nameof(span), $"The reader overshot the span capacity by {span.Length - Position} bytes");
    }
  }
}
