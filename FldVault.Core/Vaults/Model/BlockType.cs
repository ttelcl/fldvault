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
  /// Constants used as block types in generic block files
  /// (including ZVLT V2 files)
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
    /// Plain unauthenticated comment string ('UCMT')
    /// </summary>
    public const int UnauthenticatedComment = 0x544D4355;

  }
}
