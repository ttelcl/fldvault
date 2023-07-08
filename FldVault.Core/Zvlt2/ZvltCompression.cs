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
/// Options for compression in Zvlt files
/// </summary>
public enum ZvltCompression
{
  /// <summary>
  /// Automatically choose between <see cref="On"/> and <see cref="Off"/> based
  /// on the compressability of the first block.
  /// </summary>
  Auto = -1,

  /// <summary>
  /// Don't compress blocks
  /// </summary>
  Off = 0,

  /// <summary>
  /// Compress any blocks that are compressable
  /// </summary>
  On = 1,
}
