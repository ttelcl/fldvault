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
