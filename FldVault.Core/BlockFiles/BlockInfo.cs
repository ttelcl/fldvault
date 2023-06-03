/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FldVault.Core.BlockFiles;


/// <summary>
/// Describes a block in a block file
/// </summary>
public class BlockInfo
{
  /// <summary>
  /// Create a new BlockInfo
  /// </summary>
  public BlockInfo(
    int kind,
    int size = 0,
    long offset = 0L)
  {
    Kind = kind;
    Size = size;  
    Offset = offset;  
  }

  /// <summary>
  /// Try to read the next block header from the stream, returning
  /// null on EOF (or other failure).
  /// </summary>
  /// <param name="source">
  /// The stream to read from.
  /// </param>
  /// <param name="skip">
  /// If false, the caller is responsible for reading the block's content.
  /// If true, this method will skip over the block (positioning the stream
  /// at the start of the next block)
  /// </param>
  /// <returns>
  /// The block header that was read, or null on EOF.
  /// </returns>
  public static BlockInfo? TryReadHeaderSync(Stream source, bool skip)
  {
    Span<byte> buffer = stackalloc byte[8];
    var offset = source.Position;
    if(source.Read(buffer) == 8)
    {
      var bi = new BlockInfo(
        BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(0, 4)),
        BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(4, 4)),
        offset);
      if(skip)
      {
        source.Position = offset + bi.Size;
      }
      return bi;
    }
    else
    {
      return null;
    }
  }

  /// <summary>
  /// The block kind code. Usually a 4CC
  /// </summary>
  public int Kind { get; init; }

  /// <summary>
  /// The block size. Mutable to support creation scenarios.
  /// </summary>
  public int Size { get; set; }

  /// <summary>
  /// The offset of the block in its file. Mutable to support writing scenarios
  /// </summary>
  public long Offset { get; set; }

  /// <summary>
  /// Adjust this BlockInfo to fit the given content and the current
  /// offset of the given stream and synchronously write it to that file
  /// </summary>
  /// <param name="target">
  /// The target stream
  /// </param>
  /// <param name="content">
  /// The content of the block (excluding the block header)
  /// </param>
  public void WriteSync(Stream target, ReadOnlySpan<byte> content)
  {
    Size = content.Length + 8;
    Offset = target.Position;
    Span<byte> header = stackalloc byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(0, 4), Kind);
    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), Size);
    target.Write(header);
    target.Write(content);
  }

  /// <summary>
  /// Write a new block and return the new BlockInfo representing the
  /// newly written block
  /// </summary>
  public static BlockInfo WriteSync(
    Stream target,
    int kind,
    ReadOnlySpan<byte> content)
  {
    var bi = new BlockInfo(kind, content.Length, target.Position);
    bi.WriteSync(target, content);
    return bi;
  }

  /// <summary>
  /// Adjust this BlockInfo to fit the given content and the current
  /// offset of the given stream and asynchronously write it to that file
  /// </summary>
  /// <param name="target">
  /// The target stream
  /// </param>
  /// <param name="content">
  /// The content of the block (excluding the block header)
  /// </param>
  /// <param name="ct">
  /// The cancellation token
  /// </param>
  public async Task WriteAsync(Stream target, ReadOnlyMemory<byte> content, CancellationToken ct = default)
  {
    Size = content.Length + 8;
    Offset = target.Position;
    byte[] header = new byte[8];
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), Kind);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), Size);
    await target.WriteAsync(header, ct);
    await target.WriteAsync(content, ct);
  }
}
