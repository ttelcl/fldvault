/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Mvlt;

/// <summary>
/// The phases of MVLT file reading or writing.
/// </summary>
public enum MvltPhase
{
  /// <summary>
  /// Before reading or writing the header.
  /// </summary>
  BeforeHeader = 0,

  /// <summary>
  /// After reading or writing the header, but before the preamble.
  /// </summary>
  AfterHeader = 1,

  /// <summary>
  /// While reading or writing data, after the preamble.
  /// </summary>
  Data = 3,

  /// <summary>
  /// After writing the terminator.
  /// </summary>
  End = 4,

  /// <summary>
  /// The writer / reader has been disposed.
  /// </summary>
  Disposed = 5,
}
