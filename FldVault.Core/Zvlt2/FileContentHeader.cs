/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.BlockFiles;
using FldVault.Core.Utilities;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Information from a V3 file content block excluding the actual
/// content
/// </summary>
public class FileContentHeader
{
  /// <summary>
  /// Create a new FileContentHeader. Alternatively use the 
  /// <see cref="Create(Stream, int, int, out BlockFiles.BlockInfo)"/> or 
  /// <see cref="Read(VaultFileReader, IBlockInfo)"/> factory methods.
  /// </summary>
  public FileContentHeader(
    IBlockInfo bi,
    int contentLength)
  {
    BlockInfo = bi;
    ContentLength = contentLength;

    if(IsCompressed)
    {
      throw new NotImplementedException(
        "Block compression is not yet implemented");
    }
  }

  /// <summary>
  /// Create a new FileContentHeader and a new BlockInfo
  /// </summary>
  /// <param name="target">
  /// The target stream, providing the Offset for the block info.
  /// This method repositions the stream to its end.
  /// </param>
  /// <param name="contentLength">
  /// The decompressed size of the content stored in this block
  /// </param>
  /// <param name="blockSize">
  /// The total block size for this block, including the compressed content.
  /// </param>
  /// <param name="blockInfo">
  /// Receives the create BlockInfo instance
  /// </param>
  /// <returns></returns>
  /// <remarks>
  /// <para>
  /// If the block is not compressed, <paramref name="blockSize"/> is
  /// <paramref name="contentLength"/> + 40. The reverse is also true:
  /// if <paramref name="blockSize"/> is <paramref name="contentLength"/> + 40, 
  /// then the block is not compressed, otherwise it is compressed.
  /// </para>
  /// </remarks>
  public static FileContentHeader Create(
    Stream target,
    int contentLength,
    int blockSize,
    out BlockInfo blockInfo)
  {
    target.Position = target.Length;
    blockInfo = new BlockInfo(Zvlt2BlockType.FileContentV3, blockSize, target.Position);
    return new FileContentHeader(blockInfo, contentLength);
  }

  /// <summary>
  /// Position the reader on the specified block, read the
  /// header field, and use it to create a new FileContentHeader instance.
  /// </summary>
  public static FileContentHeader Read(
    VaultFileReader reader,
    IBlockInfo ibi)
  {
    if(ibi.Kind != Zvlt2BlockType.FileContentV3)
    {
      throw new ArgumentException(
        "Expecting a ZVLT V3 file content block");
    }
    reader.SeekBlock(ibi);
    Span<byte> buffer = stackalloc byte[4];
    reader.ReadSpan(buffer);
    new SpanReader()
      .ReadI32(buffer, out var contentLength)
      .CheckEmpty(buffer);
    return new FileContentHeader(ibi, contentLength);
  }

  /// <summary>
  /// The block header
  /// </summary>
  public IBlockInfo BlockInfo { get; init; }

  /// <summary>
  /// The content length
  /// </summary>
  public int ContentLength { get; init; }

  /// <summary>
  /// True if the block content is compressed.
  /// </summary>
  public bool IsCompressed { get => BlockInfo.Size != ContentLength + 40; }

  /// <summary>
  /// Write the information in this header record into the
  /// given buffer
  /// </summary>
  /// <param name="span">
  /// The 12 byte buffer to write to
  /// </param>
  /// <exception cref="ArgumentOutOfRangeException">
  /// The buffer isn't 12 bytes in size
  /// </exception>
  public void WriteToSpan(Span<byte> span)
  {
    if(span.Length != 12)
    {
      throw new ArgumentOutOfRangeException(nameof(span), "Expecting a 12 byte span as argument");
    }
    new SpanWriter()
      .WriteI32(span, BlockInfo.Kind)
      .WriteI32(span, BlockInfo.Size)
      .WriteI32(span, ContentLength)
      .CheckFull(span);
  }

  /// <summary>
  /// Write this header of the file content block to stream
  /// </summary>
  /// <param name="stream">
  /// The stream to write to. The position must match the Offset of the BlockInfo
  /// </param>
  public void Write(Stream stream)
  {
    if(stream.Position != BlockInfo.Offset)
    {
      throw new ArgumentOutOfRangeException(nameof(stream), "Stream is not positioned as expected");
    }
    Span<byte> bytes = stackalloc byte[12];
    WriteToSpan(bytes);
    stream.Write(bytes);
  }
}
