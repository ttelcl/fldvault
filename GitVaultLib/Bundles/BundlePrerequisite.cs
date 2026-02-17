/*
 * (c) 2026  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace GitVaultLib.Bundles;

/// <summary>
/// Describes a commit that is a preprequisite of a bundle
/// </summary>
public class BundlePrerequisite: IBundleCommit
{
  private readonly HashSet<string> _labels;

  /// <summary>
  /// Create a new BundlePrerequisite
  /// </summary>
  public BundlePrerequisite(
    string commit,
    DateTimeOffset authored,
    DateTimeOffset committed,
    IEnumerable<string> labels)
  {
    _labels = [.. labels];
    Commit = commit;
    AuthorDate = authored;
    CommitDate = committed;
  }

  /// <summary>
  /// The commit ID (SHA1 hash)
  /// </summary>
  [JsonProperty("commit")]
  public string Commit { get; }

  /// <summary>
  /// The authoring timestamp of the commit
  /// </summary>
  [JsonProperty("authored")]
  public DateTimeOffset AuthorDate { get; }

  /// <summary>
  /// The commit timestamp of the commit. Usually the same as <see cref="AuthorDate"/>,
  /// but may be newer in the case of rebasing
  /// </summary>
  [JsonProperty("committed")]
  public DateTimeOffset CommitDate { get; }

  /// <summary>
  /// Zero or more labels (references) pointing to this commit in their full form.
  /// </summary>
  [JsonProperty("labels")]
  public IReadOnlySet<string> Labels => _labels;

  /// <summary>
  /// Add a label
  /// </summary>
  /// <param name="label"></param>
  public void AddLabel(string label)
  {
    _labels.Add(label);
  }
}
