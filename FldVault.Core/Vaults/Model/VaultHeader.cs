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

namespace FldVault.Core.Vaults.Model;

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
    int unused = 0)
  {
    BlockHeader = blockHeader;
    Version = version;
    KeyId = keyId;
    Unused = unused;
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
    Span<byte> data = stackalloc byte[hdr.Size-8];
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
    int version = VaultFormat.VaultFileVersion2,
    int unused = 0)
  {
    Span<byte> data = stackalloc byte[24];
    BinaryPrimitives.WriteInt32LittleEndian(data.Slice(0, 4), version);
    BinaryPrimitives.WriteInt32LittleEndian(data.Slice(4, 4), unused);
    keyId.TryWriteBytes(data.Slice(8, 16));
    var bi = BlockInfo.WriteSync(destination, BlockType.ZvltFile, data);
    return bi;
  }

  /// <summary>
  /// The block header
  /// </summary>
  public BlockInfo BlockHeader { get; init; }

  /// <summary>
  /// The format version of the file, expected to be 
  /// <see cref="VaultFormat.VaultFileVersion2"/>
  /// </summary>
  public int Version { get; init; }

  /// <summary>
  /// Extra field in the header, currently unused
  /// </summary>
  public int Unused { get; init; }

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
    if(hdr.Kind != VaultFormat.VaultSignatureV2)
    {
      throw new InvalidOperationException(
        "This is not a ZVLT file");
    }
    if(hdr.Size != 32)
    {
      throw new InvalidOperationException(
        "Expecting header block to be 32 bytes");
    }
    return hdr;
  }

  private static VaultHeader FromRaw(BlockInfo hdr, ReadOnlySpan<byte> data)
  {
    var version = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
    if(version != VaultFormat.VaultFileVersion2)
    {
      throw new InvalidOperationException(
        "Incompatible ZVLT version");
    }
    var unused = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
    if(unused != 0)
    {
      throw new InvalidOperationException(
        "Expecting reserved bytes in header to be 0");
    }
    var keyId = new Guid(data.Slice(8, 16));
    return new VaultHeader(hdr, version, keyId, unused);
  }

}