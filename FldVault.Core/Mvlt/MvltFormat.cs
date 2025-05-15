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
using System.Threading.Tasks;

using FldVault.Core.Vaults;

namespace FldVault.Core.Mvlt;

/// <summary>
/// Static class for MVLT format functionality and constants.
/// </summary>
public static class MvltFormat
{

  /// <summary>
  /// Minor file format version.
  /// </summary>
  public const ushort MvltMinorVersion = 0x0000;

  /// <summary>
  /// Major file format version.
  /// </summary>
  public const ushort MvltMajorVersion = 0x0001;

  /// <summary>
  /// 'MVLT'
  /// </summary>
  public const uint MvltSignature = 0x544C564D;

  /// <summary>
  /// 'PREM'
  /// </summary>
  public const uint Preamble4CC = 0x4D455250;

  /// <summary>
  /// 'DCMP'
  /// </summary>
  public const uint CompressedBlock4CC = 0x504D4344;

  /// <summary>
  /// 'DUNC'
  /// </summary>
  public const uint UncompressedBlock4CC = 0x434E5544;

  /// <summary>
  /// 'POST'
  /// </summary>
  public const uint Terminator4CC = 0x54534F50;

  /// <summary>
  /// The maximum / normal content size of a chunk in an MVLT file.
  /// </summary>
  public const int MvltChunkSize = 0x000D0000;

  /// <summary>
  /// Read the key info from an MVLT stream that is positioned at the start of the file.
  /// Also validates signature and major version.
  /// </summary>
  public static PassphraseKeyInfoFile ReadKeyInfo(Stream stream)
  {
    Span<byte> bytes = stackalloc byte[96];
    stream.Read(bytes.Slice(0,16)); // skip header
    var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
    if(signature != MvltSignature)
    {
      throw new InvalidDataException("Incorrect MVLT file signature");
    }
    var version = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(6, 2));
    if(version != MvltMajorVersion)
    {
      throw new InvalidDataException("Unsupported MVLT major version");
    }
    stream.Read(bytes);
    return PassphraseKeyInfoFile.ReadFrom(bytes);
  }

  /// <summary>
  /// Read the key info from an MVLT file.
  /// </summary>
  public static PassphraseKeyInfoFile ReadKeyInfo(string fileName)
  {
    using var stream = File.OpenRead(fileName);
    return ReadKeyInfo(stream);
  }
}
