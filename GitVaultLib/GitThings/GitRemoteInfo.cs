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
/// Information about a GIT remote.
/// </summary>
public class GitRemoteInfo
{
  private readonly List<GitRemoteTarget> _targets;

  /// <summary>
  /// Create a new GitRemoteInfo
  /// </summary>
  public GitRemoteInfo(
    string name)
  {
    Name = name;
    _targets = [];
    Targets = _targets.AsReadOnly();
  }

  /// <summary>
  /// The name of the remote.
  /// </summary>
  public string Name { get; }

  /// <summary>
  /// Target(s) of the remote including their modes.
  /// </summary>
  public IReadOnlyList<GitRemoteTarget> Targets { get; }

  /// <summary>
  /// Add a new target to the remote (silently ignore if it
  /// already exists).
  /// </summary>
  public GitRemoteTarget AddTarget(string mode, string target)
  {
    var existing = _targets.FirstOrDefault(
      t => t.Target == target && t.Mode == mode);
    if(existing != null)
    {
      return existing;
    }
    else
    {
      var newTarget = new GitRemoteTarget(Name, mode, target);
      _targets.Add(newTarget);
      return newTarget;
    }
  }
}
