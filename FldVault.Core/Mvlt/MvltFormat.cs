/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}
