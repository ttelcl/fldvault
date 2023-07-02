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

/// <summary>
/// Extension methods on IBlockElement
/// </summary>
public static class BlockElementExtensions
{
  /// <summary>
  /// Validate that the root block of the element has the specified content length
  /// </summary>
  public static IBlockElement ExpectContentLength(
    this IBlockElement blockElement, int expectedLength)
  {
    blockElement.Block.ExpectContentLength(expectedLength);
    return blockElement;
  }

  /// <summary>
  /// Validate that the root block of the element has the specified block length
  /// </summary>
  public static IBlockElement ExpectBlockLength(
    this IBlockElement blockElement, int expectedLength)
  {
    blockElement.Block.ExpectBlockLength(expectedLength);
    return blockElement;
  }

}
