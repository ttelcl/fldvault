/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitVaultLib.GitThings;

/// <summary>
/// Captures the result of a git command.
/// </summary>
public class GitRunResult
{
  /// <summary>
  /// Create a new GitRunResult
  /// </summary>
  public GitRunResult()
  {
  }

  /// <summary>
  /// The status code of the command.
  /// </summary>
  public int StatusCode { get; set; } = -1;

  /// <summary>
  /// The standard output of the command.
  /// </summary>
  public List<string> OutputLines { get; } = [];

  /// <summary>
  /// The standard error output of the command.
  /// </summary>
  public List<string> ErrorLines { get; } = [];

  /// <summary>
  /// The command line arguments passed to git
  /// </summary>
  public List<string> Arguments { get; } = [];

  /// <summary>
  /// True if the command was successful (status code 0).
  /// </summary>
  public bool Success => StatusCode == 0;
}
