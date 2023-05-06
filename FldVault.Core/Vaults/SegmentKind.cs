/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Vaults
{
  /// <summary>
  /// Known segment kinds
  /// </summary>
  public enum SegmentKind
  {
    /// <summary>
    /// Terminator pseudo-segment
    /// </summary>
    Terminator = 0,

    /// <summary>
    /// A file name
    /// </summary>
    Name = 1,

    /// <summary>
    /// The main content of a file
    /// </summary>
    File = 2,

    /// <summary>
    /// The main content of a non-file
    /// </summary>
    SecretBlob = 3,

  }
}
