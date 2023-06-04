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
/// Block types used in ZVLT v2 files
/// </summary>
public static class Zvlt2BlockType
{

  /// <summary>
  /// ZVLT file header block ('Zvlt')
  /// </summary>
  public const int ZvltFile = 0x746C765A;

  /// <summary>
  /// Embedded *.pass.key-info file ('PASS')
  /// </summary>
  public const int PassphraseLink = 0x53534150;

  /// <summary>
  /// File name block ('FNAM')
  /// </summary>
  public const int FileName = 0x4D414E46;

  /// <summary>
  /// Large file element header block ('FLX(')
  /// </summary>
  public const int FileHeader = 0x28584C46;

  /// <summary>
  /// Large file first content block ('FCT1')
  /// </summary>
  public const int FileContent1 = 0x31544346;

  /// <summary>
  /// Large file later content block ('FCTn')
  /// </summary>
  public const int FileContentN = 0x6E544346;

}
