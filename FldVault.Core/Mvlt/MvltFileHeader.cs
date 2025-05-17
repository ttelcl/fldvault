/*
 * (c) 2025  ttelcl / ttelcl
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

using FldVault.Core.Crypto;
using FldVault.Core.Vaults;

namespace FldVault.Core.Mvlt;

/// <summary>
/// MVLT file header content
/// </summary>
public class MvltFileHeader
{
  private readonly byte[] _preheader = new byte[16];

  /// <summary>
  /// Create a new MvltFileHeader
  /// </summary>
  private MvltFileHeader(
    ushort minorVersion,
    ushort majorVersion,
    long epochTicks,
    PassphraseKeyInfoFile pkif)
  {
    MajorVersion = majorVersion;
    MinorVersion = minorVersion;
    Stamp = epochTicks;
    KeyInfoFile = pkif;
    BinaryPrimitives.WriteUInt32LittleEndian(
      _preheader.AsSpan(0, 4), MvltFormat.MvltSignature);
    BinaryPrimitives.WriteUInt16LittleEndian(
      _preheader.AsSpan(4, 2), minorVersion);
    BinaryPrimitives.WriteUInt16LittleEndian(
      _preheader.AsSpan(6, 2), majorVersion);
    BinaryPrimitives.WriteInt64LittleEndian(
      _preheader.AsSpan(8, 8), epochTicks);
  }

  /// <summary>
  /// Major version of the MVLT file
  /// </summary>
  public ushort MajorVersion { get; }

  /// <summary>
  /// Minor version of the MVLT file
  /// </summary>
  public ushort MinorVersion { get; }

  /// <summary>
  /// Time stamp of the MVLT file, expressed as EpochTicks
  /// </summary>
  public long Stamp { get; }

  /// <summary>
  /// The preheader of the MVLT file, containing the signature, version and time stamp.
  /// </summary>
  public ReadOnlySpan<byte> PreHeader => _preheader;

  /// <summary>
  /// The key info block
  /// </summary>
  public PassphraseKeyInfoFile KeyInfoFile { get; }

  /// <summary>
  /// Create a new MvltReader to continue reading the MVLT file.
  /// </summary>
  public MvltReader CreateReader(
    KeyChain keyChain,
    Stream input,
    bool ownsInput,
    NonceGenerator? nonceGenerator = null)
  {
    return new MvltReader(
      keyChain,
      this,
      input,
      ownsInput,
      nonceGenerator);
  }

  /// <summary>
  /// Read the MVLT file header from a stream.
  /// </summary>
  /// <param name="stream"></param>
  /// <param name="cancellationToken"></param>
  /// <returns>
  /// The MVLT file header.
  /// </returns>
  public static async Task<MvltFileHeader> ReadAsync(
    Stream stream,
    CancellationToken cancellationToken = default)
  {
    if(!stream.CanRead)
    {
      throw new ArgumentException("Stream is not readable", nameof(stream));
    }
    var buffer1 = new byte[8];
    if(buffer1.Length != await stream.ReadAsync(buffer1, cancellationToken))
    {
      throw new EndOfStreamException("End of stream reached while reading header");
    }
    var signature = BinaryPrimitives.ReadUInt32LittleEndian(buffer1.AsSpan(0, 4));
    if(signature != MvltFormat.MvltSignature)
    {
      throw new InvalidDataException("Incorrect MVLT file signature");
    }
    var minorVersion = BinaryPrimitives.ReadUInt16LittleEndian(buffer1.AsSpan(4, 2));
    var majorVersion = BinaryPrimitives.ReadUInt16LittleEndian(buffer1.AsSpan(6, 2));
    if(majorVersion != MvltFormat.MvltMajorVersion)
    {
      throw new InvalidDataException("Unsupported MVLT file major version");
    }
    if(minorVersion > MvltFormat.MvltMinorVersion)
    {
      throw new InvalidDataException("Unsupported MVLT file minor version");
    }
    // reuse buffer1 for stamp
    if(buffer1.Length != await stream.ReadAsync(buffer1, cancellationToken))
    {
      throw new EndOfStreamException("End of stream reached while reading header");
    }
    var epochTicks = BinaryPrimitives.ReadInt64LittleEndian(buffer1.AsSpan(0, 8));
    var buffer2 = new byte[96];
    if(buffer2.Length != await stream.ReadAsync(buffer2, cancellationToken))
    {
      throw new EndOfStreamException("End of stream reached while reading header");
    }
    var pkif = PassphraseKeyInfoFile.ReadFrom(buffer2);
    return new MvltFileHeader(
      minorVersion,
      majorVersion,
      epochTicks,
      pkif);
  }
}

