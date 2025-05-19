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
    [JsonProperty("vault-folder")] string vaultFolder,
    [JsonProperty("bundle-folder")] string bundleFolder,
    Zkey keyinfo)
  {
    HostName = hostname;
    RepoName = reponame;
    VaultFolder = vaultFolder;
    BundleFolder = bundleFolder;
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
  /// The full path to the folder where the bundle vault is stored.
  /// </summary>
  [JsonProperty("vault-folder")] 
  public string VaultFolder { get; }

  /// <summary>
  /// The full path to the folder where the bundle is stored.
  /// </summary>
  [JsonProperty("bundle-folder")]
  public string BundleFolder { get; }

  /// <summary>
  /// The key information for the encryption key of this repository's vault.
  /// </summary>
  [JsonProperty("keyinfo")]
  public Zkey KeyInfo { get; }

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
