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
using System.Threading.Tasks;

using FldVault.Core.BlockFiles;
using FldVault.Core.Utilities;
using FldVault.Core.Vaults;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Models the header of a supported vault file.
/// Used in vault reading scenarios.
/// </summary>
public class VaultHeader
{
  private VaultHeader(
    BlockInfo blockHeader,
    int version,
    Guid keyId,
    DateTime timeStamp,
    int unused1 = 0,
    long unused2 = 0L)
  {
    BlockHeader = blockHeader;
    Version = version;
    KeyId = keyId;
    Unused1 = unused1;
    Unused2 = unused2;
    TimeStamp = timeStamp;
    if(timeStamp.Kind != DateTimeKind.Utc)
    {
      throw new ArgumentOutOfRangeException(nameof(timeStamp), "Expecting timestamp to be in UTC");
    }
  }

  /// <summary>
  /// Synchronously read a vault header from a stream at the current position
  /// </summary>
  /// <param name="vaultStream">
  /// The stream to read from
  /// </param>
  /// <returns>
  /// The newly read header on success
  /// </returns>
  public static VaultHeader ReadSync(Stream vaultStream)
  {
    var hdr = BlockInfo.TryReadHeaderSync(vaultStream, false);
    hdr = CheckHeader(hdr);
    Span<byte> data = stackalloc byte[hdr.Size - 8];
    if(vaultStream.Read(data) != data.Length)
    {
      throw new EndOfStreamException(
        "Unexpected end of stream while reading the header");
    }
    return FromRaw(hdr, data);
  }

  /// <summary>
  /// Write a new VaultHeader to the destination stream and return the
  /// BlockInfo that describes the new block
  /// </summary>
  public static BlockInfo WriteSync(
    Stream destination,
    Guid keyId,
    DateTime? stamp = null,
    int version = VaultFormat2.VaultFileVersion2,
    int unused1 = 0,
    long unused2 = 0L)
  {
    var stamp1 = stamp ?? DateTime.UtcNow;
    Span<byte> data = stackalloc byte[40];
    BinaryPrimitives.WriteInt32LittleEndian(data.Slice(0, 4), version);
    BinaryPrimitives.WriteInt32LittleEndian(data.Slice(4, 4), unused1);
    keyId.TryWriteBytes(data.Slice(8, 16));
    BinaryPrimitives.WriteInt64LittleEndian(data.Slice(24, 8), EpochTicks.FromUtc(stamp1));
    BinaryPrimitives.WriteInt64LittleEndian(data.Slice(32, 8), unused2);
    var bi = BlockInfo.WriteSync(destination, Zvlt2BlockType.ZvltFile, data);
    return bi;
  }

  /// <summary>
  /// The block header
  /// </summary>
  public BlockInfo BlockHeader { get; init; }

  /// <summary>
  /// The format version of the file, expected to be 
  /// <see cref="VaultFormat2.VaultFileVersion2"/>
  /// </summary>
  public int Version { get; init; }

  /// <summary>
  /// Extra field 1 in the header, currently unused
  /// </summary>
  public int Unused1 { get; init; }

  /// <summary>
  /// Extra field 2 in the header, currently unused
  /// </summary>
  public long Unused2 { get; init; }

  /// <summary>
  /// The UTC time stamp the vault file was created
  /// </summary>
  public DateTime TimeStamp { get; init; }

  /// <summary>
  /// The ID of the key used in this file
  /// </summary>
  public Guid KeyId { get; init; }

  private static BlockInfo CheckHeader(BlockInfo? hdr)
  {
    if(hdr == null)
    {
      throw new InvalidOperationException(
        "No content in vault file (missing header)");
    }
    if(hdr.Kind != Zvlt2BlockType.ZvltFile)
    {
      throw new InvalidOperationException(
        "This is not a ZVLT file");
    }
    if(hdr.Size != 48)
    {
      throw new InvalidOperationException(
        "Expecting header block to be 48 bytes");
    }
    return hdr;
  }

  private static VaultHeader FromRaw(BlockInfo hdr, ReadOnlySpan<byte> data)
  {
    var version = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
    if(version != VaultFormat2.VaultFileVersion2)
    {
      throw new InvalidOperationException(
        "Incompatible ZVLT version");
    }
    var unused1 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
    if(unused1 != 0)
    {
      throw new InvalidOperationException(
        "Expecting reserved field 1 in header to be 0");
    }
    var keyId = new Guid(data.Slice(8, 16));
    var stamp = EpochTicks.ToUtc(BinaryPrimitives.ReadInt64LittleEndian(data.Slice(24, 8)));
    var unused2 = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(32, 8));
    if(unused2 != 0L)
    {
      throw new InvalidOperationException(
        "Expecting reserved field 2 in header to be 0");
    }
    return new VaultHeader(hdr, version, keyId, stamp, unused1, unused2);
  }

}
