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
/// Describes a seed commit of a bundle
/// </summary>
public class BundleSeed: IBundleCommit
{
  private readonly HashSet<string> _refs;

  /// <summary>
  /// (deserialization constructor)
  /// </summary>
  /// <param name="commit">
  /// The commit ID
  /// </param>
  /// <param name="authored">
  /// The timestamp the commit was originally authored
  /// </param>
  /// <param name="committed">
  /// The timestamp the commit object was built. If the commit is a rebased commit,
  /// this may be more recent than <paramref name="authored"/>. Otherwise it is
  /// most likely the same.
  /// </param>
  /// <param name="refs">
  /// One or potentially more full ref names that are declared as seeds in the bundle
  /// that all point to this commit. 
  /// </param>
  public BundleSeed(
    string commit,
    DateTimeOffset authored,
    DateTimeOffset committed,
    IEnumerable<string> refs)
  {
    _refs = [.. refs];
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
  /// The explicit seed ref(s) pointing to this commit in their full form. Does not include
  /// other labels pointing to this commit.
  /// </summary>
  [JsonProperty("refs")]
  public IReadOnlySet<string> Refs => _refs;

  /// <summary>
  /// Add a reference to <see cref="Refs"/>.
  /// </summary>
  /// <param name="reference"></param>
  public void AddRef(string reference)
  {
    _refs.Add(reference);
  }
}
