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
/// Describes a gitremote (name, mode and target).
/// </summary>
public class GitRemoteTarget
{
  /// <summary>
  /// Create a new GitRemoteTarget
  /// </summary>
  public GitRemoteTarget(
    string remoteName,
    string mode,
    string target)
  {
    RemoteName = remoteName;
    Mode = mode;
    Target = target;
  }

  /// <summary>
  /// The name of the remote (e.g. "origin").
  /// </summary>
  public string RemoteName { get; }

  /// <summary>
  /// The access mode for the remote target
  /// (e.g. "push" or "fetch").
  /// </summary>
  public string Mode { get; }

  /// <summary>
  /// The target of the remote. A URL or a local path.
  /// </summary>
  public string Target { get; }

}
