/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FldVault.Core.BlockFiles;

/// <summary>
/// A read-only view of a BlockElementContainer
/// </summary>
public interface IBlockElementContainer
{
  /// <summary>
  /// The child elements
  /// </summary>
  IReadOnlyList<IBlockElement> Children { get; }
}

