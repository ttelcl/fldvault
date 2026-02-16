/*
 * (c) 2026  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitVaultLib.Bundles;

/// <summary>
/// Describes a commit referenced from a bundle header
/// </summary>
public interface IBundleCommit
{
  /// <summary>
  /// The commit ID (SHA1 hash)
  /// </summary>
  public string Commit { get; }

  /// <summary>
  /// The authoring timestamp of the commit
  /// </summary>
  public DateTimeOffset AuthorDate { get; }

  /// <summary>
  /// The commit timestamp of the commit. Usually the same as <see cref="AuthorDate"/>,
  /// but may be newer in the case of rebasing
  /// </summary>
  public DateTimeOffset CommitDate { get; }
}
