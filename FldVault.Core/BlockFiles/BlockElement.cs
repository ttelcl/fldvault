/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.BlockFiles;

/// <summary>
/// A node in a hierarchy of BlockInfo wrappers
/// </summary>
public class BlockElement: BlockElementContainer
{
  /// <summary>
  /// Create a new BlockElement and register it as child of its owner
  /// </summary>
  public BlockElement(BlockInfo block)
  {
    Block = block;
  }

  /// <summary>
  /// The block wrapped by this element
  /// </summary>
  public BlockInfo Block { get; init; }

}
