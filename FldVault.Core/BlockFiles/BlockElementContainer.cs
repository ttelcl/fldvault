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
/// An object that holds zero or more BlockElements.
/// BlockElement is a subclass of this.
/// </summary>
public class BlockElementContainer: IBlockElementContainer
{
  private readonly List<BlockElement> _children;

  /// <summary>
  /// Create a new BlockElementContainer
  /// </summary>
  public BlockElementContainer()
  {
    _children = new List<BlockElement>();
    Children = _children.AsReadOnly();
  }

  /// <summary>
  /// A read-only view on the child elements
  /// </summary>
  public IReadOnlyList<BlockElement> Children { get; init; }

  IReadOnlyList<IBlockElement> IBlockElementContainer.Children { get => Children; }

  /// <summary>
  /// Add a child BlockElement
  /// </summary>
  public void AddChild(BlockElement child)
  {
    _children.Add(child);
  }

  /// <summary>
  /// Enumerate the BlockInfo objects in all descendants
  /// </summary>
  public IEnumerable<BlockInfo> ChildBlocks()
  {
    foreach(var child in Children)
    {
      yield return child.Block;
      foreach(var block in child.ChildBlocks())
      {
        yield return block;
      }
    }
  }

}
