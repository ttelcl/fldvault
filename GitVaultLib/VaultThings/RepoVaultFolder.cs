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

using GitVaultLib.Configuration;

namespace GitVaultLib.VaultThings;

/// <summary>
/// Information about the vault folder for a repository including its key.
/// </summary>
public class RepoVaultFolder
{
  private Zkey? _cachedKey;

  /// <summary>
  /// Create a new RepoVaultFolder
  /// </summary>
  public RepoVaultFolder(
    string anchorFolder,
    string repoName)
  {
    anchorFolder = Path.GetFullPath(anchorFolder).TrimEnd('/', '\\');
    if(!CentralSettings.IsValidName(repoName, true))
    {
      throw new ArgumentException(
        $"Repository name '{repoName}' is not valid for use with GitVault. " +
        "Only letters, digits, '-', '_' and '.' are allowed.");
    }
    AnchorFolder = anchorFolder;
    RepoName = repoName;
    VaultFolder = Path.Combine(anchorFolder, repoName);
  }

  /// <summary>
  /// The anchor folder in which the repo vault folder is located.
  /// </summary>
  public string AnchorFolder { get; }

  /// <summary>
  /// The logical repository name (same on all hosts). This uniquely
  /// identifies the repository on all hosts.
  /// </summary>
  public string RepoName { get; }

  /// <summary>
  /// The full path to the vault folder for this repository.
  /// (<see cref="AnchorFolder"/>/<see cref="RepoName"/>)
  /// </summary>
  public string VaultFolder { get; }

  /// <summary>
  /// Get the vault key for this repository. The vault key is cached
  /// upon the first call to this method. If the vault folder does not
  /// have a key, or has multiple ones, an exception is thrown.
  /// </summary>
  public Zkey GetVaultKey()
  {
    if(_cachedKey == null)
    {
      var error = TryCacheKey();
      if(error != null)
      {
        throw new InvalidOperationException(
          $"Error while looking for repo vault key: {error}.");
      }
    }
    return _cachedKey!;
  }

  /// <summary>
  /// Check if the vault key can be retrieved for this repository.
  /// Returns null if the key can be retrieved, or an error message
  /// otherwise. This method can be used to check if
  /// <see cref="GetVaultKey"/> can be called without throwing an
  /// exception.
  /// </summary>
  /// <returns>
  /// Null to indicate that the key can be retrieved, or an error
  /// message if the key cannot be retrieved.
  /// </returns>
  public string? CanGetKey()
  {
    if(_cachedKey != null)
    {
      return null; // already cached
    }
    return TryCacheKey();
  }

  private string? TryCacheKey()
  {
    var folderKeys = FolderKey.KeysInFolder(VaultFolder).Values;
    if(folderKeys.Count == 0)
    {
      return $"No keys found in vault folder '{VaultFolder}'. Please initialize the vault key first.";
    }
    else if(folderKeys.Count == 1)
    {
      _cachedKey = folderKeys.Single();
      return null;
    }
    else
    {
      return
        $"Multiple keys found in vault folder '{VaultFolder}'. " +
        "This is not supported by GitVault.";
    }
  }
}
