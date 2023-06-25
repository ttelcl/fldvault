﻿/*
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
/// Constants used in ZVLT v2 file blocks (other than block types)
/// </summary>
public static class VaultFormat2
{

  /// <summary>
  /// The vault file version
  /// </summary>
  public const int VaultFileVersion2 = 0x00020000;

  /// <summary>
  /// The chunk size used for chopping up files to be encrypted
  /// into a vault (256 kb)
  /// </summary>
  public const int VaultChunkSize = 0x00040000;
}
