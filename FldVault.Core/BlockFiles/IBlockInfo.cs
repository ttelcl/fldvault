/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FldVault.Core.BlockFiles;

/// <summary>
/// A read-only view on a <see cref="BlockInfo"/> 
/// </summary>
public interface IBlockInfo
{
  /// <summary>
  /// The block kind code. Usually a 4CC
  /// </summary>
  int Kind { get; }

  /// <summary>
  /// The block's total size (header included)
  /// </summary>
  int Size { get; }

  /// <summary>
  /// The block's offset in its container file
  /// </summary>
  long Offset { get; }
}

