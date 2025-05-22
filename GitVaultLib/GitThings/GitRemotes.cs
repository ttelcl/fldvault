/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GitVaultLib.GitThings;

/// <summary>
/// The collection of git remotes for a repository.
/// </summary>
public class GitRemotes
{
  private readonly Dictionary<string, GitRemoteInfo> _remotes;

  /// <summary>
  /// Create a new GitRemotes
  /// </summary>
  public GitRemotes()
  {
    _remotes = new(StringComparer.OrdinalIgnoreCase);
  }

  /// <summary>
  /// The mapping of remote names to their information.
  /// </summary>
  public IReadOnlyDictionary<string, GitRemoteInfo> Remotes {
    get => _remotes;
  }

  /// <summary>
  /// Find a remote by its name.
  /// </summary>
  public GitRemoteInfo? this[string name] {
    get {
      return _remotes.TryGetValue(name, out var remote) ? remote : null;
    }
  }

  /// <summary>
  /// Add a new remote record
  /// </summary>
  public GitRemoteInfo Add(string name, string target, string mode)
  {
    if(!_remotes.TryGetValue(name, out var remote))
    {
      remote = new GitRemoteInfo(name);
      _remotes.Add(name, remote);
    }
    remote.AddTarget(mode, target);
    return remote;
  }

  /// <summary>
  /// Add a new remote record from a line of the output of
  /// 'git remote -v'
  /// </summary>
  public GitRemoteInfo? AddFromLine(string line)
  {
    var match = Regex.Match(
      line,
      @"^(?<name>[^ ]+)\s+(?<target>.*\S)[\s+]\((?<mode>[a-z]+)\)$");
    if(!match.Success)
    {
      return null;
    }
    var name = match.Groups["name"].Value;
    var target = match.Groups["target"].Value;
    var mode = match.Groups["mode"].Value;
    return Add(name, target, mode);
  }

  /// <summary>
  /// Create a new GitRemotes object from the lines output
  /// by 'git remote -v'
  /// </summary>
  public static GitRemotes FromLines(
    IEnumerable<string> lines)
  {
    var remotes = new GitRemotes();
    foreach(var line in lines)
    {
      remotes.AddFromLine(line);
    }
    return remotes;
  }

}
