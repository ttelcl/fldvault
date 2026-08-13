using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Constants for use in the <see cref="VaultHeader.Purpose"/> field
/// </summary>
public static class ZvltPurpose
{
  /// <summary>
  /// The file is a normal ZVLT files
  /// </summary>
  public const int Default = 0;

  /// <summary>
  /// The file is a master key file containing <see cref="Zvlt2BlockType.KeyTransform"/>
  /// blocks (and no files)
  /// </summary>
  public const int Master = 0x5453414D;

}
