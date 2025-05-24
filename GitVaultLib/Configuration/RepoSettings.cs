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

using FldVault.Core.Vaults;

using GitVaultLib.GitThings;
using System.Runtime.Serialization;
using GitVaultLib.VaultThings;

namespace GitVaultLib.Configuration;


/// <summary>
/// The repository settings for GitVault.
/// </summary>
public class RepoSettings
{
  /// <summary>
  /// Create a new RepoSettings.
  /// </summary>
  public RepoSettings(
    [JsonProperty("by-anchor")] IDictionary<string, AnchorRepoSettings>? byAnchor = null)
  {
    ByAnchor =
      byAnchor is null
      ? new Dictionary<string, AnchorRepoSettings>(StringComparer.OrdinalIgnoreCase)
      : new Dictionary<string, AnchorRepoSettings>(
        byAnchor,
        StringComparer.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Filled by the deserializer with the anchor names as keys
  /// </summary>
  [JsonProperty("by-anchor")]
  public Dictionary<string, AnchorRepoSettings> ByAnchor { get; }

  /// <summary>
  /// Try to find this repo's settings for the given vault anchor.
  /// </summary>
  /// <param name="vaultAnchorName"></param>
  /// <returns></returns>
  public AnchorRepoSettings? FindAnchor(string vaultAnchorName)
  {
    return ByAnchor.TryGetValue(vaultAnchorName, out var anchorSettings)
      ? anchorSettings
      : null;
  }

  /// <summary>
  /// Save the settings to the .git folder of the repository.
  /// This only happens during initialization of the gitvault repository,
  /// after that these settings are treated as immutable.
  /// </summary>
  /// <param name="gitRepoFolder">
  /// The repository.
  /// </param>
  public void Save(GitRepoFolder gitRepoFolder)
  {
    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
    var settingsFile = gitRepoFolder.GitvaultSettingsFile;
    File.WriteAllText(settingsFile, json);
  }

  /// <summary>
  /// Try to load the gitvault settings for the given repository.
  /// Returns null if the settings file does not exist (when the
  /// repisitory has not been initialized for gitvault use yet).
  /// </summary>
  /// <param name="gitRepoFolder"></param>
  /// <returns></returns>
  public static RepoSettings? TryLoad(
    GitRepoFolder gitRepoFolder)
  {
    var settingsFile = gitRepoFolder.GitvaultSettingsFile;
    if(!File.Exists(settingsFile))
    {
      return null;
    }
    var json = File.ReadAllText(settingsFile);
    return JsonConvert.DeserializeObject<RepoSettings>(json);
  }

  [OnDeserialized]
  internal void OnDeserializedHook(StreamingContext context)
  {
    // This is called by the deserializer after all properties have been set.
    foreach(var kvp in ByAnchor)
    {
      // The vault anchor name is the key of the dictionary entry
      // and must be copied to the VaultAnchor property of the value.
      kvp.Value.VaultAnchor = kvp.Key;
    }
  }
}

/// <summary>
/// GitVault related settings for one repository and one anchor.
/// Serialized as part of RepoSettings.
/// </summary>
public class AnchorRepoSettings
{
  /// <summary>
  /// Create a new RepoSettings
  /// </summary>
  public AnchorRepoSettings(
    string hostname,
    string reponame,
    [JsonProperty("bundle-anchor")] string bundleAnchor)
  {
    HostName = hostname;
    RepoName = reponame;
    VaultAnchor = null!; // to be set by the deserialization hook
    BundleAnchor = bundleAnchor;
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
  /// The tag in the central settings used to identify the full path to the
  /// bundle anchor folder.
  /// </summary>
  [JsonProperty("bundle-anchor")]
  public string BundleAnchor { get; }

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
  /// Get the name of the file storing the tips map for the latest bundle
  /// </summary>
  public string GetTipsFile(CentralSettings centralSettings)
  {
    if(!centralSettings.BundleAnchors.TryGetValue(BundleAnchor, out var bundleAnchorFolder))
    {
      throw new ArgumentException(
        $"Bundle anchor '{BundleAnchor}' not found in central settings.");
    }
    return Path.Combine(
      bundleAnchorFolder,
      VaultAnchor,
      RepoName,
      $"{RepoName}.{HostName}.tips.json");
  }

  /// <summary>
  /// Get the file name for the bundle file for this repository, host and anchor.
  /// </summary>
  public string GetBundleFileName(CentralSettings centralSettings)
  {
    if(!centralSettings.BundleAnchors.TryGetValue(BundleAnchor, out var bundleAnchorFolder))
    {
      throw new ArgumentException(
        $"Bundle anchor '{BundleAnchor}' not found in central settings.");
    }
    var shortBundleName = $"{RepoName}.{HostName}.-.bundle";
    return Path.Combine(
      bundleAnchorFolder,
      VaultAnchor,
      RepoName,
      shortBundleName);
  }

  /// <summary>
  /// Materializes the abstract information in this object to a
  /// concrete outgoing BundleInfo object by looking up the anchor names and constructing
  /// the file names. Throws an exception if the vault folder has no known key.
  /// </summary>
  public BundleInfo ToBundleInfo(CentralSettings centralSettings)
  {
    if(!centralSettings.Anchors.TryGetValue(VaultAnchor, out var vaultAnchorFolder))
    {
      throw new ArgumentException(
        $"Vault anchor '{VaultAnchor}' not found in central settings.");
    }
    if(!Directory.Exists(vaultAnchorFolder))
    {
      throw new ArgumentException(
        $"Vault anchor folder '{vaultAnchorFolder}' does not exist.");
    }

    if(!centralSettings.BundleAnchors.TryGetValue(BundleAnchor, out var bundleAnchorFolder))
    {
      throw new ArgumentException(
        $"Bundle anchor '{BundleAnchor}' not found in central settings.");
    }
    var shortBundleName = $"{RepoName}.{HostName}.-.bundle";
    var bundleFile = Path.Combine(
      bundleAnchorFolder,
      VaultAnchor,
      RepoName,
      shortBundleName);
    var repoVaultFolder = new RepoVaultFolder(vaultAnchorFolder, RepoName);
    var keyInfo = repoVaultFolder.GetVaultKey();
    var keyTag = keyInfo.KeyTag;
    var shortVaultName = $"{shortBundleName}.{keyTag}.mvlt";
    var vaultFile = Path.Combine(
      vaultAnchorFolder,
      shortVaultName);
    return new BundleInfo(
      true,
      HostName,
      RepoName,
      bundleFile,
      vaultFile,
      keyInfo);
  }

}
