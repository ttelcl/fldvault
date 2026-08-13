/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.IO;

using Newtonsoft.Json;

using GitVaultLib.VaultThings;

namespace GitVaultLib.Configuration;

/// <summary>
/// GitVault related settings for one repository and one anchor.
/// Serialized as part of <see cref="RepoSettings"/>.
/// </summary>
public class AnchorRepoSettings
{
  /// <summary>
  /// Create a new RepoSettings
  /// </summary>
  public AnchorRepoSettings(
    string hostname,
    string reponame)
  {
    HostName = hostname;
    RepoName = reponame;
    VaultAnchor = null!; // to be set by the deserialization hook
  }

  /// <summary>
  /// The effective "host name" to identify this particular instance
  /// of the repository.
  /// </summary>
  [JsonProperty("hostname")]
  public string HostName { get; }

  /// <summary>
  /// The name of the repository. This must be the same for all instances
  /// of this repository (otherwise repositories are considered unrelated)
  /// </summary>
  [JsonProperty("reponame")]
  public string RepoName { get; }

  /// <summary>
  /// The tag in the central settings used to identify the full path to the
  /// vault anchor folder.
  /// </summary>
  //[JsonProperty("vault-anchor")]
  [JsonIgnore]
  public string VaultAnchor { get; internal set; }

  /// <summary>
  /// Check if the vault folder has a known key. Returns null on success,
  /// or an error message otherwise.
  /// </summary>
  public string? CanGetKey(CentralSettings centralSettings)
  {
    if(!centralSettings.Anchors.TryGetValue(VaultAnchor, out var vaultAnchorFolder))
    {
      return
        $"Vault anchor '{VaultAnchor}' not found in central settings.";
    }
    var repoVaultFolder = new RepoVaultFolder(vaultAnchorFolder, RepoName);
    return repoVaultFolder.CanGetKey();
  }

  /// <summary>
  /// Get the RepoVaultFolder for this repository and anchor.
  /// </summary>
  public RepoVaultFolder GetRepoVaultFolder(CentralSettings centralSettings)
  {
    if(!centralSettings.Anchors.TryGetValue(VaultAnchor, out var vaultAnchorFolder))
    {
      throw new ArgumentException(
        $"Vault anchor '{VaultAnchor}' not found in central settings.");
    }
    return new RepoVaultFolder(vaultAnchorFolder, RepoName);
  }

  /// <summary>
  /// Get the folder where the bundle files and other local files for this repository live
  /// </summary>
  public string GetBundleFolder(CentralSettings centralSettings)
  {
    return Path.Combine(
      centralSettings.BundleAnchor,
      VaultAnchor,
      RepoName);
  }

  /// <summary>
  /// Get the name of the file storing the tips map for the latest bundle
  /// </summary>
  public string GetTipsFile(CentralSettings centralSettings)
  {
    var bundleFolder = GetBundleFolder(centralSettings);
    return Path.Combine(
      bundleFolder,
      $"{RepoName}.{HostName}.tips.json");
  }

  /// <summary>
  /// Get the file name for the bundle file for this repository, host and anchor.
  /// </summary>
  public string GetBundleFileName(CentralSettings centralSettings)
  {
    var bundleFolder = GetBundleFolder(centralSettings);
    return Path.Combine(
      bundleFolder,
      $"{RepoName}.{HostName}.-.bundle");
  }

  /// <summary>
  /// Get the file name for the delta bundle file for this repository, host, anchor, and recipe.
  /// </summary>
  public string GetDeltaBundleFileName(string recipeName, CentralSettings centralSettings)
  {
    var bundleFolder = GetBundleFolder(centralSettings);
    return Path.Combine(
      bundleFolder,
      $"{RepoName}.{HostName}.{recipeName}.dbundle");
  }

  /// <summary>
  /// Get the file name for the source folder file for this repository, host and anchor.
  /// If this file does not exists, or points to a different repository, that means that
  /// this repository must not push to the bundle (because it is not the 'owner')
  /// </summary>
  public string GetSourceFileName(CentralSettings centralSettings)
  {
    var bundleFolder = GetBundleFolder(centralSettings);
    return Path.Combine(
      bundleFolder,
      $"{RepoName}.{HostName}.source.json");
  }

  /// <summary>
  /// Try to load the BundleSource for this repository, host and anchor. Returns null if
  /// not found.
  /// </summary>
  public BundleSource? GetBundleSource(CentralSettings centralSettings)
  {
    var sourceFile = GetSourceFileName(centralSettings);
    return BundleSource.TryLoad(sourceFile);
  }

  /// <summary>
  /// Get or create the BundleRecord for this repository, host and anchor from
  /// the given BundleRecordCache.
  /// </summary>
  public BundleRecord GetBundleRecord(BundleRecordCache cache)
  {
    var key = cache.MakeBundleKey(
      anchorName: VaultAnchor,
      repoName: RepoName,
      hostName: HostName);
    var record = cache.GetBundleRecord(key);
    return record;
  }

}
