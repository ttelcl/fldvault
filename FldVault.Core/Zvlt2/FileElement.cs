/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.BlockFiles;

namespace FldVault.Core.Zvlt2
{
  /// <summary>
  /// Gathers the information about an encrypted file inside a side.
  /// </summary>
  public class FileElement
  {
    /// <summary>
    /// Create a new FileElement
    /// </summary>
    public FileElement(
      IBlockElement rootElement)
    {
      if(rootElement.Block.Kind != Zvlt2BlockType.FileHeader)
      {
        throw new ArgumentOutOfRangeException(nameof(rootElement), 
          "Expecting a file header block");
      }
      if(rootElement.Children[^1].Block.Kind != BlockType.ImpliedGroupEnd)
      {
        throw new ArgumentOutOfRangeException(nameof(rootElement), 
          "Unrecognized file element structure: missing element group terminator");
      }
      RootElement = rootElement;
      var nameElement = RootElement.Children.FirstOrDefault(b => b.Block.Kind == Zvlt2BlockType.FileName);
      NameBlock = nameElement?.Block ?? throw new InvalidOperationException(
        "Missing file name block");
      var firstContentElement = RootElement.Children.FirstOrDefault(b => b.Block.Kind == Zvlt2BlockType.FileContent1);
      FirstContentBlock = firstContentElement?.Block ?? throw new InvalidOperationException(
        "No file content in file element??");
    }

    /// <summary>
    /// The root "FLX(" element
    /// </summary>
    public IBlockElement RootElement { get; }

    /// <summary>
    /// The file element header block
    /// </summary>
    public IBlockInfo HeaderBlock { get => RootElement.Block; }

    /// <summary>
    /// The file element name block
    /// </summary>
    public IBlockInfo NameBlock { get; init; }

    public IBlockInfo FirstContentBlock { get; init; }

    public IReadOnlyList<IBlockInfo> RemainingContentBlocks { get; init; }
  }
}
