/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FldVault.Core.BlockFiles;

/// <summary>
/// Description of IBlockElement
/// </summary>
public interface IBlockElement: IBlockElementContainer
{
  /// <summary>
  /// The block wrapped by this element
  /// </summary>
  IBlockInfo Block { get; }
}
