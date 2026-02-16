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

using LibGit2Sharp;
using GitVaultLib.Delta;

namespace GitVaultLib.Bundles;

/// <summary>
/// Description of BundleInfo
/// </summary>
public class BundleInfo
{
  private readonly Dictionary<string, BundleSeed> _seeds;
  private readonly Dictionary<string, BundlePrerequisite> _prerequisites;

  /// <summary>
  /// Create a new BundleInfo
  /// </summary>
  public BundleInfo(
    IReadOnlyDictionary<string, BundleSeed> seeds,
    IReadOnlyDictionary<string, BundlePrerequisite> prerequisites)
  {
    _seeds = new Dictionary<string, BundleSeed>(seeds);
    _prerequisites = new Dictionary<string, BundlePrerequisite>(prerequisites);
  }

  /// <summary>
  /// The seed commits of the bundle, indexed by commit id (and collapsed where
  /// multiple seeds have the same commit)
  /// </summary>
  [JsonProperty("seeds")]
  public IReadOnlyDictionary<string, BundleSeed> Seeds => _seeds;

  /// <summary>
  /// The prerequisite commits of the bundle, indexed by commit id, and annotated
  /// by label(s), if any
  /// </summary>
  [JsonProperty("prerequisites")]
  public IReadOnlyDictionary<string, BundlePrerequisite> Prerequisites => _prerequisites;

  /// <summary>
  /// Build a new <see cref="BundleInfo"/> given the source repository
  /// and the bundle file
  /// </summary>
  /// <param name="repoPath">
  /// The path to the git repository, either the root directory or the .git folder
  /// in it (or the bare repository folder)
  /// </param>
  /// <param name="bundlePath">
  /// The path to the bundle file to describe
  /// </param>
  /// <returns></returns>
  public static BundleInfo Build(string repoPath, string bundlePath)
  {
    using var repo = new Repository(repoPath);
    var bundleHeader = BundleHeader.FromFile(bundlePath);
    return Build(repo, bundleHeader);
  }

  /// <summary>
  /// Build a new <see cref="BundleInfo"/> given the source repository
  /// and the bundle file
  /// </summary>
  /// <param name="repo"></param>
  /// <param name="bundlePath"></param>
  /// <returns></returns>
  public static BundleInfo Build(Repository repo, string bundlePath)
  {
    return Build(repo, BundleHeader.FromFile(bundlePath));
  }

  /// <summary>
  /// Build a new <see cref="BundleInfo"/> given the source repository
  /// and the bundle file
  /// </summary>
  /// <param name="repoPath">
  /// The path to the git repository, either the root directory or the .git folder
  /// in it (or the bare repository folder)
  /// </param>
  /// <param name="bundleHeader"></param>
  /// <returns></returns>
  public static BundleInfo Build(string repoPath, BundleHeader bundleHeader)
  {
    using var repo = new Repository(repoPath);
    return Build(repo, bundleHeader);
  }

  /// <summary>
  /// Build a new <see cref="BundleInfo"/> given the source repository
  /// and the bundle file
  /// </summary>
  /// <param name="repo"></param>
  /// <param name="bundleHeader"></param>
  /// <returns></returns>
  /// <exception cref="NotImplementedException"></exception>
  public static BundleInfo Build(Repository repo, BundleHeader bundleHeader)
  {
    throw new NotImplementedException();
  }

}
