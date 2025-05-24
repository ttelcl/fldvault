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

using GitVaultLib.Configuration;

using Newtonsoft.Json;

namespace GitVaultLib.GitThings;

/// <summary>
/// Stores tip information for branches and tags for a Git repository,
/// in the form of a mapping of ref names to commit IDs.
/// </summary>
public class GitTips
{
  /// <summary>
  /// Create a new GitTips
  /// </summary>
  public GitTips(Dictionary<string, string> tips)
  {
    TipMap = new Dictionary<string, string>(tips, StringComparer.Ordinal);
  }

  /// <summary>
  /// The mapping from ref names (branches, tags) to commit IDs.
  /// This mapping is case sensitive, as that seems to be the lesser of two evils
  /// in this case.
  /// </summary>
  [JsonProperty("tips")]
  public IReadOnlyDictionary<string, string> TipMap { get; }

  /// <summary>
  /// Create a new GitTips from a collection of response lines
  /// (from the command git show-ref --branches --tags)
  /// </summary>
  public static GitTips FromLines(
    IEnumerable<string> responseLines)
  {
    var tipMap = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach(var line in responseLines)
    {
      var parts = line.Split(' ', 2);
      if(parts.Length == 2 && parts[0].Length == 40)
      {
        tipMap[parts[1]] = parts[0];
      }
    }
    return new GitTips(tipMap);
  }

  /// <summary>
  /// Load GitTips from a JSON file, if it exists
  /// </summary>
  public static GitTips? FromFile(string fileName)
  {
    if(!File.Exists(fileName))
    {
      return null;
    }
    var content = File.ReadAllText(fileName);
    return JsonConvert.DeserializeObject<GitTips>(content);
  }

  /// <summary>
  /// Calculate the GitTips for the repository containing the given folder.
  /// </summary>
  /// <param name="witnessFolder"></param>
  /// <returns></returns>
  public static GitTips ForRepository(string? witnessFolder = null)
  {
    witnessFolder ??= Environment.CurrentDirectory;
    var result = GitRunner.EnumTips(witnessFolder);
    if(result.StatusCode != 0)
    {
      throw new InvalidOperationException(
        $"Failed to get tips for repository at '{witnessFolder}': \n{string.Join("\n", result.ErrorLines)}");
    }
    return FromLines(result.OutputLines);
  }

  /// <summary>
  /// Calculate the GitTips for a git bundle file. This method returns an empty
  /// GitTips object if the bundle file does not exist.
  /// </summary>
  public static GitTips ForBundleFile(string bundleFile)
  {
    if(!File.Exists(bundleFile))
    {
      return new GitTips([]);
    }
    var result = GitRunner.TipsFromBundle(bundleFile);
    if(result.StatusCode != 0)
    {
      throw new InvalidOperationException(
        $"Failed to get tips from bundle file '{bundleFile}': \n{string.Join("\n", result.ErrorLines)}");
    }
    return FromLines(result.OutputLines);
  }

  /// <summary>
  /// Test if this GitTips instance has the same content as another one.
  /// </summary>
  public bool AreSame(GitTips? other)
  {
    if(other == null || TipMap.Count != other.TipMap.Count)
    {
      return false;
    }
    foreach(var kvp in TipMap)
    {
      if(!other.TipMap.TryGetValue(kvp.Key, out var otherCommitId) || otherCommitId != kvp.Value)
      {
        return false;
      }
    }
    return true;
  }

  /// <summary>
  /// Try to load the GitTips from the *.tips.json file in the bundle folder for the
  /// given settings.
  /// </summary>
  public static GitTips? FromBundleFolder(
    CentralSettings centralSettings,
    AnchorRepoSettings settings)
  {
    var file = settings.GetTipsFile(centralSettings);
    return FromFile(file);
  }

  /// <summary>
  /// Save the GitTips to a JSON file.
  /// </summary>
  public void SaveToFile(string fileName)
  {
    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
    File.WriteAllText(fileName, json);
  }

  /// <summary>
  /// Save the GitTips to the *.tips.json file in the bundle folder for the
  /// given settings.
  /// </summary>
  public void SaveToBundleFolder(
    CentralSettings centralSettings,
    AnchorRepoSettings settings)
  {
    var file = settings.GetTipsFile(centralSettings);
    SaveToFile(file);
  }

}
