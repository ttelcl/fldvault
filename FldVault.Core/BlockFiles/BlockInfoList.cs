﻿/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.BlockFiles;

/// <summary>
/// A collection of blockinfo descriptors for a block file
/// </summary>
public class BlockInfoList
{
  private readonly List<BlockInfo> _blocks;

  /// <summary>
  /// Create a new BlockInfoList and optionally load it from an existing stream
  /// </summary>
  public BlockInfoList(Stream? source = null)
  {
    _blocks = new List<BlockInfo>();
    Blocks = _blocks.AsReadOnly();
    ChangeCounter = 0;
    if(source != null)
    {
      Reload(source);
    }
  }

  /// <summary>
  /// A read-only view on the blocks in this list
  /// </summary>
  public IReadOnlyList<BlockInfo> Blocks { get; init; }

  /// <summary>
  /// Counter for changes, used to detect if there were any changes
  /// </summary>
  public int ChangeCounter { get; private set; }

  /// <summary>
  /// Clear the existing list and load it from the specified stream
  /// </summary>
  public void Reload(Stream source)
  {
    ChangeCounter++;
    _blocks.Clear();
    BlockInfo? bi;
    while((bi = BlockInfo.TryReadHeaderSync(source, true)) != null)
    {
      _blocks.Add(bi);
    }
  }

  /// <summary>
  /// Get the file index after the last block (where the next block would be inserted)
  /// Return 0L if the list is empty.
  /// </summary>
  public long Tail { get => _blocks.Count == 0 ? 0L : (_blocks[^1].Offset + _blocks[^1].Size); }

  /// <summary>
  /// Add the next block to the list. Its size must be valid and offset be contiguous
  /// to the last block
  /// </summary>
  public void Add(BlockInfo block)
  {
    if(block.Size < 8)
    {
      throw new ArgumentException("Expecting a valid block descriptor (with a size of 8 or greater)");
    }
    if(block.Offset != Tail)
    {
      throw new ArgumentException(
        "Incorrect offset for the block being added");
    }
    ChangeCounter++;
    _blocks.Add(block);
  }

  /// <summary>
  /// Check if the block list contains exactly one block with the same offset,
  /// kind and size.
  /// </summary>
  /// <param name="block"></param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if there are multiple blocks with the same offset in the list
  /// (which indicates an internal error)
  /// </exception>
  public bool ContainsBlock(IBlockInfo block)
  {
    var candidate = _blocks.SingleOrDefault(b => b.Offset == block.Offset);
    return
      candidate != null
      && candidate.Kind == block.Kind
      && candidate.Size == block.Size;
  }

  /// <summary>
  /// Build a new element tree wrapping the blocks in the list
  /// </summary>
  public BlockElementContainer BuildElementTree()
  {
    var root = new BlockElementContainer();
    var stack = new Stack<BlockElementContainer>();
    var top = root;
    foreach(var block in _blocks)
    {
      var e = new BlockElement(block);
      top.AddChild(e);
      if(BlockType.IsGroupEnd(block.Kind))
      {
        if(stack.Count == 0)
        {
          throw new InvalidOperationException(
            $"Invalid block grouping detected (too many group end markers)");
        }
        top = stack.Pop();
      }
      if(BlockType.IsGroupStart(block.Kind))
      {
        stack.Push(top);
        top = e;
      }
    }
    if(stack.Count != 0)
    {
      throw new InvalidOperationException(
        $"Invalid block grouping detected (too few group end markers)");
    }
    return root;
  }

}
