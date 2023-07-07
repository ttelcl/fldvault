/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Constants used in ZVLT v2+ file blocks (other than block types)
/// </summary>
public static class VaultFormat
{

  /// <summary>
  /// The vault file version supported by this library
  /// </summary>
  public const int VaultFileVersion = 0x00030000;

  /// <summary>
  /// The chunk size used for chopping up files to be encrypted
  /// into a vault (832 kb, 0x0D0000 bytes). That is: the maximum number
  /// of bytes to be encrypted or decrypted at once, and the maximum
  /// number of bytes to be compressed or decompressed at once for
  /// compressed content.
  /// </summary>
  public const int VaultChunkSize = 0x000D0000;
}
