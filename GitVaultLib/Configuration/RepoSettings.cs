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

using Newtonsoft.Json;

namespace GitVaultLib.Configuration;

/// <summary>
/// GitVault related settings for one repository.
/// Serialized in the repository's .git folder.
/// </summary>
public class RepoSettings
{
  /// <summary>
  /// Create a new RepoSettings
  /// </summary>
  public RepoSettings(
    string hostname,
    string reponame,
    [JsonProperty("vault-anchor")] string vaultAnchor,
    [JsonProperty("bundle-anchor")] string bundleAnchor,
    Zkey keyinfo)
  {
    HostName = hostname;
    RepoName = reponame;
    VaultAnchor = vaultAnchor;
    BundleAnchor = bundleAnchor;
    KeyInfo = keyinfo;
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
  [JsonProperty("vault-anchor")] 
  public string VaultAnchor { get; }

  /// <summary>
  /// The tag in the central settings used to identify the full path to the
  /// bundle anchor folder.
  /// </summary>
  [JsonProperty("bundle-anchor")]
  public string BundleAnchor { get; }

  /// <summary>
  /// The key information for the encryption key of this repository's vault.
  /// </summary>
  [JsonProperty("keyinfo")]
  public Zkey KeyInfo { get; }

  /// <summary>
  /// The first 8 characters of the key ID. This is used as a hint to
  /// identify the key used for this repository (and is part of the vault file
  /// name).
  /// </summary>
  [JsonIgnore]
  public string KeyTag {
    get {
      return KeyInfo.KeyId.Substring(0, 8);
    }
  }

  /// <summary>
  /// Materializes the abstract information in this object to a
  /// concrete outgoing BundleInfo object by looking up the anchor names and constructing
  /// the file names.
  /// </summary>
  public BundleInfo ToBundleInfo(CentralSettings centralSettings)
  {
    if(!centralSettings.Anchors.TryGetValue(VaultAnchor, out var vaultAnchorFolder))
    {
      throw new ArgumentException(
        $"Vault anchor '{VaultAnchor}' not found in central settings.");
    }
    if(!centralSettings.BundleAnchors.TryGetValue(BundleAnchor, out var bundleAnchorFolder))
    {
      throw new ArgumentException(
        $"Bundle anchor '{BundleAnchor}' not found in central settings.");
    }
    var vaultFile = Path.Combine(
      vaultAnchorFolder,
      $"{RepoName}.{HostName}.-.bundle.{KeyTag}.mvlt");
    var bundleFile = Path.Combine(
      bundleAnchorFolder,
      $"{RepoName}.{HostName}.-.bundle");
    return new BundleInfo(
      true,
      HostName,
      RepoName,
      bundleFile,
      vaultFile,
      KeyInfo);
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
    var settingsFile = Path.Combine(
      gitRepoFolder.GitFolder,
      "gitvault-settings.json");
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
    var settingsFile = Path.Combine(
      gitRepoFolder.GitFolder,
      "gitvault-settings.json");
    if(!File.Exists(settingsFile))
    {
      return null;
    }
    var json = File.ReadAllText(settingsFile);
    return JsonConvert.DeserializeObject<RepoSettings>(json);
  }

}
