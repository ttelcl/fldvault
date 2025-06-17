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

using GitVaultLib.VaultThings;

namespace GitVaultLib.Configuration;

/// <summary>
/// The combination of vault anchor, repository, and host name,
/// together uniquely identifying a bundle+vault file pair.
/// Also stores related information, such as the local source
/// repository folder, key, and direction.
/// </summary>
public class BundleRecord
{
  private string? _sourceRepoFolder;

  /// <summary>
  /// Create a new BundleRecord
  /// </summary>
  public BundleRecord(
    CentralSettings gitvaultSettings,
    BundleKey key)
  {
    Key = key;
    if(!gitvaultSettings.Anchors.TryGetValue(AnchorName, out var vaultAnchorFolder))
    {
      throw new ArgumentException(
        $"Vault anchor '{AnchorName}' not found in central settings.");
    }
    AnchorFolder = vaultAnchorFolder;
    var bundleAnchorFolder = gitvaultSettings.BundleAnchor;
    BundleAnchorFolder = bundleAnchorFolder;
    BundleFolder = Path.Combine(
      bundleAnchorFolder,
      AnchorName,
      RepoName);
    VaultFolder = Path.Combine(
      vaultAnchorFolder,
      RepoName);
    FilePrefix = $"{RepoName}.{HostName}";
    BundleFileShortName = FilePrefix + ".-.bundle";
    BundleFileName = Path.Combine(
      BundleFolder,
      BundleFileShortName);
    SourceFileName = Path.Combine(
      BundleFolder,
      FilePrefix + ".source.json");
    TryGetSourceRepoFolder(); // Load source folder if available and set direction
  }

  /// <summary>
  /// The bundle key, which uniquely identifies the bundle and vault.
  /// It contains the anchor name, repository name, and host name.
  /// </summary>
  public BundleKey Key { get; }

  /// <summary>
  /// The vault anchor name (shortcut for Key.AnchorName).
  /// </summary>
  public string AnchorName => Key.AnchorName;

  /// <summary>
  /// The logical repository name (shortcut for Key.RepoName).
  /// </summary>
  public string RepoName => Key.RepoName;

  /// <summary>
  /// The 'host name', logically distinguishing different bundle sources.
  /// (shortcut for Key.HostName).
  /// </summary>
  public string HostName => Key.HostName;

  /// <summary>
  /// The full path to the bundle anchor folder.
  /// </summary>
  public string BundleAnchorFolder { get; }
  
  /// <summary>
  /// The bundle folder.
  /// </summary>
  public string BundleFolder { get; }

  /// <summary>
  /// The full path to the (vault) anchor folder.
  /// </summary>
  public string AnchorFolder { get; }

  /// <summary>
  /// The vault folder (nested under <see cref="AnchorFolder"/>).
  /// </summary>
  public string VaultFolder { get; }

  /// <summary>
  /// Common prefix for file names related to this record
  /// ("{RepoName}.{HostName}")
  /// </summary>
  public string FilePrefix { get; }

  /// <summary>
  /// The short name of the bundle file (without path). This is
  /// used as to construct the full bundle file name and as part of
  /// the vault file name.
  /// </summary>
  public string BundleFileShortName { get; }

  /// <summary>
  /// The full path to the bundle file.
  /// </summary>
  public string BundleFileName { get; }

  /// <summary>
  /// The full path to the source claim file. If this is a local bundle,
  /// this file exists and contains a JSON serialized <see cref="BundleSource"/> object.
  /// Otherwise, this file does not exist and indicates a remote bundle.
  /// </summary>
  public string SourceFileName { get; }

  /// <summary>
  /// Try to get the source repository folder from the source file.
  /// Upon success, the value is cached. Returns the source repository folder
  /// on success, or null if the source info file does not exist.
  /// </summary>
  public string? TryGetSourceRepoFolder()
  {
    if(!File.Exists(SourceFileName))
    {
      _sourceRepoFolder = null;
      return null;
    }
    if(!String.IsNullOrEmpty(_sourceRepoFolder))
    {
      return _sourceRepoFolder;
    }
    // Load the source folder from the source file.
    var source = BundleSource.TryLoad(SourceFileName);
    if(source == null)
    {
      return null;
    }
    _sourceRepoFolder = source.SourceFolder;
    return _sourceRepoFolder;
  }

  /// <summary>
  /// The cached vault key, if available. Call <see cref="TryGetZkey(out Zkey?)"/>
  /// to fill this property.
  /// </summary>
  public Zkey? CachedZkey { get; private set; }

  /// <summary>
  /// Try to get and cache the vault key from the vault folder, returning null if successful,
  /// or an error message otherwise.
  /// </summary>
  public string? TryGetZkey(out Zkey? zkey)
  {
    if(CachedZkey != null)
    {
      zkey = CachedZkey;
      return null;
    }
    var folderKeys = FolderKey.KeysInFolder(VaultFolder).Values;
    if(folderKeys.Count == 0)
    {
      zkey = null;
      return $"No keys found in vault folder '{VaultFolder}'. Please initialize the vault key first.";
    }
    else if(folderKeys.Count == 1)
    {
      CachedZkey = folderKeys.Single();
      zkey = CachedZkey;
      FolderKey.PutFolderKey(VaultFolder, CachedZkey);
      return null;
    }
    else
    {
      zkey = null;
      return
        $"Multiple keys found in vault folder '{VaultFolder}'. " +
        "This is not supported by GitVault.";
    }
  }

  /// <summary>
  /// Get the vault key descriptor, throwing an exception if it is not known.
  /// </summary>
  public Zkey GetZkeyOrFail()
  {
    var error = TryGetZkey(out var zkey);
    if(error != null)
    {
      throw new InvalidOperationException(
        "Failed to get vault key: " + error);
    }
    return zkey!;
  }

  /// <summary>
  /// Try to get the vault file name for this bundle record. This will fail
  /// if no key can be retrieved from the vault folder.
  /// </summary>
  /// <param name="vaultFileName">
  /// Receives the vault file name if successful, or null on error.
  /// </param>
  /// <returns>
  /// Null if successful, or an error message otherwise.
  /// </returns>
  public string? TryGetVaultFileName(out string? vaultFileName)
  {
    var error = TryGetZkey(out var zkey);
    if(error != null)
    {
      vaultFileName = null;
      return "Failed to get vault key: " + error;
    }
    if(zkey == null)
    {
      vaultFileName = null;
      return "No vault key found (internal error).";
    }
    vaultFileName = Path.Combine(
      VaultFolder,
      $"{BundleFileShortName}.{zkey.KeyTag}.mvlt");
    return null;
  }

  /// <summary>
  /// Get the vault file name, throwing an exception if the key is not known.
  /// Consider using <see cref="TryGetVaultFileName(out string?)"/> instead.
  /// </summary>
  public string GetVaultFileNameOrFail()
  {
    var error = TryGetVaultFileName(out var vaultFileName);
    if(error != null)
    {
      throw new InvalidOperationException(
        "Failed to get vault file name: " + error);
    }
    return vaultFileName!;
  }

  /// <summary>
  /// The time stamp of the bundle file, or null if the bundle file does not exist.
  /// </summary>
  public DateTimeOffset? BundleTime { 
    get {
      if(!File.Exists(BundleFileName))
      {
        return null;
      }
      var fi = new FileInfo(BundleFileName);
      return fi.LastWriteTimeUtc;
    }
  }

  /// <summary>
  /// The time stamp of the vault file, or null if the vault file does not exist
  /// (or the vault key is unknown)
  /// </summary>
  public DateTimeOffset? VaultTime {
    get {
      var _ = TryGetZkey(out var zkey);
      if(zkey == null)
      {
        return null; // No key, no vault file
      }
      var vaultFileName = GetVaultFileNameOrFail();
      if(!File.Exists(vaultFileName))
      {
        return null; // Vault file does not exist
      }
      var fi = new FileInfo(vaultFileName);
      return fi.LastWriteTimeUtc;
    }
  }

  /// <summary>
  /// True if a source folder file exists for this bundle record, even if 
  /// the folder it points to does not exist. This indicates that this bundle
  /// is a local bundle, at some point associated with a source repository.
  /// See <see cref="TryGetSourceRepoFolder"/> to see if the repo folder
  /// still exists.
  /// </summary>
  public bool HasSourceFile { get => File.Exists(SourceFileName); }

  /// <summary>
  /// True if a source repository folder exists, implying that this bundle
  /// is outgoing.
  /// </summary>
  public bool HasSourceRepoFolder {
    get {
      if(!HasSourceFile)
      {
        return false; // No source file, no source folder
      }
      return TryGetSourceRepoFolder() != null;
    }
  }

}
