/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GitVaultLib.GitThings;

/// <summary>
/// A list of git root commit IDs (JSON serializable)
/// </summary>
public class GitRoots
{
  /// <summary>
  /// Create a new GitRoots
  /// </summary>
  public GitRoots(
    IEnumerable<string> roots)
  {
    Roots = roots.Where(r => r.Length == 40).ToHashSet(StringComparer.OrdinalIgnoreCase);
  }

  /// <summary>
  /// The set of root commit IDs (40 hex characters each, case insensitive).
  /// </summary>
  [JsonProperty("roots")]
  public IReadOnlySet<string> Roots { get; }

  /// <summary>
  /// Load GitRoots from a JSON file.
  /// </summary>
  public static GitRoots FromFile(string fileName)
  {
    if(!File.Exists(fileName))
    {
      throw new FileNotFoundException($"Git roots file '{fileName}' does not exist.");
    }
    var content = File.ReadAllText(fileName);
    return JsonConvert.DeserializeObject<GitRoots>(content) ?? new GitRoots(Enumerable.Empty<string>());
  }

  /// <summary>
  /// Load GitRoots from the git-roots.json file in the given vault folder, returning
  /// an empty set if the file does not exist.
  /// </summary>
  public static GitRoots ForVaultFolder(string folder) 
  {
    folder = Path.GetFullPath(folder);
    var fileName = Path.Combine(folder, "git-roots.json");
    if(!File.Exists(fileName))
    {
      return new GitRoots([]);
    }
    return FromFile(fileName);
  }

  /// <summary>
  /// Calculate the GitRoots for the repository containing the given folder.
  /// </summary>
  /// <param name="witnessFolder">
  /// Any folder that is part of the repository. If null or empty,
  /// Environment.CurrentDirectory is used as the witness folder.
  /// </param>
  public static GitRoots ForRepository(string? witnessFolder)
  {
    var result = GitRunner.EnumRoots(witnessFolder);
    if(result.StatusCode != 0)
    {
      throw new InvalidOperationException(
        $"Failed to enumerate roots in repository at '{witnessFolder}': \n{string.Join("\n", result.ErrorLines)}");
    }
    return new GitRoots(result.OutputLines);
  }

  /// <summary>
  /// Save the GitRoots to a JSON file.
  /// </summary>
  public void SaveToFile(string fileName)
  {
    var content = JsonConvert.SerializeObject(this, Formatting.Indented);
    File.WriteAllText(fileName, content);
  }

  /// <summary>
  /// Save the GitRoots to the git-roots.json file in the given vault folder.
  /// </summary>
  public void SaveToVaultFolder(string vaultFolder)
  {
    vaultFolder = Path.GetFullPath(vaultFolder);
    var fileName = Path.Combine(vaultFolder, "git-roots.json");
    SaveToFile(fileName);
  }

  /// <summary>
  /// True if the set of roots is empty (indicating a pristine repository without any commits at all)
  /// </summary>
  public bool IsEmpty()
  {
    return Roots.Count == 0;
  }

  /// <summary>
  /// True if the sets of roots of this and the other GitRoots have any roots in common.
  /// </summary>
  public bool Overlaps(GitRoots other)
  {
    return Roots.Overlaps(other.Roots);
  }

  /// <summary>
  /// True if the two sets of roots are compatible: either set is empty,
  /// or they have at least one root in common.
  /// </summary>
  public bool AreCompatible(GitRoots other)
  {
    // Two sets of roots are compatible if they have at least one root in common
    // or either set is empty.
    return IsEmpty() || other.IsEmpty() || Overlaps(other);
  }

  /// <summary>
  /// Return a new GitRoots that is the union of this and the other GitRoots.
  /// </summary>
  public GitRoots Merge(GitRoots other)
  {
    var mergedRoots = new HashSet<string>(Roots, StringComparer.OrdinalIgnoreCase);
    mergedRoots.UnionWith(other.Roots);
    return new GitRoots(mergedRoots);
  }

  /// <summary>
  /// Test if this GitRoots has the same elements as the other GitRoots.
  /// </summary>
  public bool AreSame(GitRoots other)
  {
    // Two sets of roots are the same if they have the same roots, ignoring case.
    return Roots.SetEquals(other.Roots);
  }

}
