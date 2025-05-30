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

using FldVault.Core.Vaults;

using GitVaultLib.GitThings;
using GitVaultLib.VaultThings;

namespace GitVaultLib.Configuration;

/// <summary>
/// Describes a logical repository: combining all information about a
/// repository from a logical point of view. Contains the bundle folder,
/// the vault folder, the encryption key ID (if known), and zero or more
/// physical repositories (<see cref="GitRepoFolder"/>)
/// </summary>
public class LogicalRepository
{
  private readonly BundleRecordCache _bundleRecordCache;

  /// <summary>
  /// Create a new LogicalRepository. This constructor does not scan the vault
  /// and bundle folders for existing bundles/vaults.
  /// </summary>
  public LogicalRepository(
    CentralSettings settings,
    string anchorName,
    string repoName)
  {
    _bundleRecordCache = new BundleRecordCache(settings, anchorName, repoName, null);
    Settings = settings;
    AnchorName = anchorName;
    RepoName = repoName;
    if(!CentralSettings.IsValidName(anchorName, false))
    {
      throw new ArgumentException(
        $"Anchor name '{anchorName}' is not valid. " +
        "Only letters, digits, '-', and '_' are allowed.");
    }
    if(!settings.Anchors.TryGetValue(anchorName, out var anchorFolder))
    {
      throw new ArgumentException(
        $"Anchor '{anchorName}' is not known.");
    }
    VaultFolder = new RepoVaultFolder(anchorFolder, repoName);
    BundleFolder = Path.Combine(
      settings.BundleAnchor,
      AnchorName,
      RepoName);
    if(!Directory.Exists(BundleFolder))
    {
      Directory.CreateDirectory(BundleFolder);
    }
  }

  /// <summary>
  /// The gitvault central settings.
  /// </summary>
  public CentralSettings Settings { get; }

  /// <summary>
  /// The name of the (vault) anchor. This is used to look up the vault anchor folder,
  /// and is used to construct local paths for the bundle folders. This name is local,
  /// so must NOT be used in vault file paths, only in bundle paths.
  /// </summary>
  public string AnchorName { get; }

  /// <summary>
  /// The name identifying the repository. This is the same on all host machines.
  /// </summary>
  public string RepoName { get; }

  /// <summary>
  /// Describes the vault folder for this repository, and provides access to functionality
  /// related to it, such as discovering the repository encryption key descriptor.
  /// </summary>
  public RepoVaultFolder VaultFolder { get; }

  /// <summary>
  /// The bundle folder path for this repository, storing all incoming and outgoing
  /// bundles for this repository.
  /// </summary>
  public string BundleFolder { get; }

  /// <summary>
  /// Given the host name, return the bundle record for this repository,
  /// creating it if it does not yet exist.
  /// </summary>
  public BundleRecord GetBundleRecord(string hostName)
  {
    if(!CentralSettings.IsValidName(hostName, false))
    {
      throw new ArgumentException(
        $"Host name '{hostName}' is not valid. " +
        "Only letters, digits, '-', and '_' are allowed.");
    }
    var key = _bundleRecordCache.MakeBundleKey(hostName: hostName);
    return _bundleRecordCache.GetBundleRecord(key);
  }

  /// <summary>
  /// Given a bundle key, return the bundle record for this repository,
  /// creating it if it does not yet exist.
  /// </summary>
  public BundleRecord GetBundleRecord(BundleKey key)
  {
    if(!key.PartialMatch(key.AnchorName, key.RepoName, null))
    {
      throw new ArgumentException(
        $"Bundle key '{key.KeyToken}' does not match this logical repository " +
        $"(anchor: {AnchorName}, repo: {RepoName}).");
    }
    return _bundleRecordCache.GetBundleRecord(key);
  }

}
