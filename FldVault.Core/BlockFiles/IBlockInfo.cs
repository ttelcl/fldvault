/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FldVault.Core.BlockFiles;

/// <summary>
/// A read-only view on a <see cref="BlockInfo"/> 
/// </summary>
public interface IBlockInfo
{
  /// <summary>
  /// The block kind code. Usually a 4CC
  /// </summary>
  int Kind { get; }

  /// <summary>
  /// The block's total size (header included)
  /// </summary>
  int Size { get; }

  /// <summary>
  /// The block's offset in its container file
  /// </summary>
  long Offset { get; }
}

/// <summary>
/// Extension methods for IBlockInfo
/// </summary>
public static class BlockInfoExtensions
{
  /// <summary>
  /// Return the raw content length of the block. If <paramref name="expectedLength"/>
  /// is provided, ensure it is equal. The raw content length is the number of bytes
  /// in the block after the eight header bytes.
  /// </summary>
  /// <exception cref="InvalidOperationException">
  /// Thrown if <paramref name="expectedLength"/> was provided and it did not match
  /// </exception>
  public static int ContentLength(this IBlockInfo info, int? expectedLength = null)
  {
    var length = info.Size - 8;
    if(expectedLength.HasValue && length != expectedLength.Value)
    {
      throw new InvalidOperationException(
        $"Unexpected content length in '{BlockType.ToText(info.Kind)}' block: {info.Size-8} instead of {expectedLength.Value}");
    }
    return length;
  }

  /// <summary>
  /// Validate that the content length is the expected value
  /// </summary>
  public static IBlockInfo ExpectContentLength(this IBlockInfo info, int expectedLength)
  {
    var length = info.Size - 8;
    if(length != expectedLength)
    {
      throw new InvalidOperationException(
        $"Unexpected content length in '{BlockType.ToText(info.Kind)}' block: {info.Size-8} instead of {expectedLength}");
    }
    return info;
  }

  /// <summary>
  /// Validate that the total block length is the expected value
  /// </summary>
  public static IBlockInfo ExpectBlockLength(this IBlockInfo info, int expectedLength)
  {
    if(info.Size != expectedLength)
    {
      throw new InvalidOperationException(
        $"Unexpected content length in '{BlockType.ToText(info.Kind)}' block: {info.Size} instead of {expectedLength}");
    }
    return info;
  }

  /// <summary>
  /// Verify that the current stream position exactly matches the end of the block
  /// </summary>
  public static IBlockInfo VerifyBlockEnd(this IBlockInfo info, Stream stream)
  {
    var written = (int)(stream.Position - info.Offset);
    if(written != info.Size)
    {
      throw new InvalidOperationException(
        $"Serialization error: expecting {info.Size} bytes but observed {written}");
    }
    return info;
  }

}

