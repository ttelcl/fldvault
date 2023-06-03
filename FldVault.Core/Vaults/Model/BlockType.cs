/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Vaults.Model
{
  /// <summary>
  /// Constants used as block types in ZVLT files
  /// </summary>
  public static class BlockType
  {

    /// <summary>
    /// The generic terminator block type (0)
    /// </summary>
    public const int GenericTerminator = 0x00000000;

    /// <summary>
    /// End of groupmarker for implied groups (')   ')
    /// </summary>
    public const int ImpliedGroupEnd = 0x20202029;

    /// <summary>
    /// Plain comment string
    /// </summary>
    public const int Comment = 0x544D4F43;

    /// <summary>
    /// ZVLT file header block 'Zvlt'
    /// </summary>
    public const int ZvltFile = 0x746C665A;

    /// <summary>
    /// Small file element header block ('FL1(')
    /// </summary>
    public const int SmallFileHeader = 0x28314C46;

    /// <summary>
    /// File name block ('FNAM')
    /// </summary>
    public const int FileName = 0x4D414E46;

    /// <summary>
    /// Small file content ('FCNT')
    /// </summary>
    public const int SmallFileContent = 0x544E4346;

    /// <summary>
    /// Large file element header block ('FLX(')
    /// </summary>
    public const int LargeFileHeader = 0x28584C46;

    /// <summary>
    /// Large file first content block ('FCT1')
    /// </summary>
    public const int LargeFileContent1 = 0x31544346;

    /// <summary>
    /// Large file later content block ('FCTn')
    /// </summary>
    public const int LargeFileContentN = 0x6E544346;

  }
}
