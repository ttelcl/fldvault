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

using FileUtilities;

using GitVaultLib.GitThings;

namespace GitVaultLib.Configuration;

/// <summary>
/// A collection of bundle records accessible by their bundle key.
/// </summary>
public class BundleRecordCache
{
  private Dictionary<BundleKey, BundleRecord> _bundleRecords;

  /// <summary>
  /// Create a new BundleRecordCache
  /// </summary>
  /// <param name="settings">
  /// The central gitvault settings, used to create new BundleRecords
  /// given their key.
  /// </param>
  /// <param name="anchorRestriction">
  /// If not null, only bundle keys with this anchor name are allowed
  /// </param>
  /// <param name="repoRestriction">
  /// If not null, only bundle keys with this repository name are allowed
  /// </param>
  /// <param name="hostRestriction">
  /// If not null, only bundle keys with this host name are allowed
  /// </param>
  public BundleRecordCache(
    CentralSettings settings,
    string? anchorRestriction = null,
    string? repoRestriction = null,
    string? hostRestriction = null)
  {
    _bundleRecords = [];
    Settings = settings;
    AnchorRestriction = anchorRestriction;
    RepoRestriction = repoRestriction;
    HostRestriction = hostRestriction;
  }

  /// <summary>
  /// The central settings for this cache. This is used to create new
  /// BundleRecords given their key.
  /// </summary>
  public CentralSettings Settings { get; }

  /// <summary>
  /// If not null, only bundle keys with this anchor name are allowed
  /// in this cache
  /// </summary>
  public string? AnchorRestriction { get; }

  /// <summary>
  /// If not null, only bundle keys with this repository name are allowed
  /// in this cache
  /// </summary>
  public string? RepoRestriction { get; }

  /// <summary>
  /// If not null, only bundle keys with this host name are allowed
  /// in this cache
  /// </summary>
  public string? HostRestriction { get; }

  /// <summary>
  /// Test if the bundle key appears in this cache.
  /// </summary>
  public bool HasKey(BundleKey key)
  {
    // no need to test restrictions, since restricted keys won't already be in the cache
    return _bundleRecords.ContainsKey(key);
  }

  /// <summary>
  /// Create a BundleKey with the given fields complementing the key restrictions of this cache.
  /// </summary>
  public BundleKey MakeBundleKey(
    string? anchorName = null,
    string? repoName = null,
    string? hostName = null)
  {
    anchorName ??= AnchorRestriction;
    repoName ??= RepoRestriction;
    hostName ??= HostRestriction;
    if(anchorName == null)
    {
      throw new ArgumentNullException(
        nameof(anchorName), "Anchor name must not be null for this cache.");
    }
    if(repoName == null)
    {
      throw new ArgumentNullException(
        nameof(repoName), "Repository name must not be null for this cache.");
    }
    if(hostName == null)
    {
      throw new ArgumentNullException(
        nameof(hostName), "Host name must not be null for this cache.");
    }
    return new BundleKey(anchorName, repoName, hostName);
  }

  /// <summary>
  /// Get the BundleRecord for the given key (creating it if it does not exist yet).
  /// Equivalent to <see cref="GetBundleRecord(BundleKey)"/>
  /// </summary>
  public BundleRecord this[BundleKey key] {
    get => GetBundleRecord(key);
  }

  /// <summary>
  /// The mapping from bundle keys to bundle records
  /// </summary>
  public IReadOnlyDictionary<BundleKey, BundleRecord> Records => _bundleRecords;

  /// <summary>
  /// Get the BundleRecord for the given key (creating it if it does not exist yet).
  /// Equivalent to the indexer <see cref="this[BundleKey]"/>.
  /// </summary>
  public BundleRecord GetBundleRecord(BundleKey key)
  {
    if(!_bundleRecords.TryGetValue(key, out var record))
    {
      if(!key.PartialMatch(
        AnchorRestriction,
        RepoRestriction,
        HostRestriction))
      {
        throw new ArgumentException(
          $"Bundle key {key} does not match the key restrictions on this cache.");
      }
      record = new BundleRecord(Settings, key);
      _bundleRecords[key] = record;
    }
    return record;
  }

  /// <summary>
  /// Put a BundleRecord into the cache. This fails if another BundleRecord
  /// instance is already known for the same key, or if its key does not
  /// meet the key restrictions of this cache.
  /// </summary>
  public void PutBundleRecord(BundleRecord record)
  {
    if(_bundleRecords.TryGetValue(record.Key, out var existingRecord))
    {
      if(!Object.ReferenceEquals(existingRecord, record))
      {
        throw new InvalidOperationException(
          $"This cache already contains a different BundleRecord instance for key {record.Key}");
      }
      return; // already is there, nothing to do
    }
    if(!record.Key.PartialMatch(
      AnchorRestriction,
      RepoRestriction,
      HostRestriction))
    {
      throw new ArgumentException(
        $"Bundle key {record.Key} does not match the key restrictions on this cache.");
    }
    _bundleRecords[record.Key] = record;
  }

  /// <summary>
  /// Register a Bundle Record in this cache based on a vault file name.
  /// </summary>
  /// <param name="vaultFile">
  /// The name of the vault file. The file does not need to exist.
  /// </param>
  /// <param name="anchor">
  /// The anchor name to use for this vault file. Optional if <see cref="AnchorRestriction"/>
  /// is not null, otherwise required.
  /// </param>
  public BundleRecord RegisterVaultFile(string vaultFile, string? anchor = null)
  {
    if(anchor != null 
      && AnchorRestriction != null 
      && !AnchorRestriction.Equals(anchor, StringComparison.OrdinalIgnoreCase))
    {
      throw new ArgumentException(
        $"Anchor name '{anchor}' does not match the anchor restriction '{AnchorRestriction}' " +
        "for this cache.");
    }
    anchor ??= AnchorRestriction;
    if(anchor == null)
    {
      throw new ArgumentNullException(
        nameof(anchor), "Anchor name must not be null for this cache.");
    }
    var key = BundleKey.FromVaultFileName(anchor, vaultFile);
    return GetBundleRecord(key);
  }

  /// <summary>
  /// Register a Bundle Record in this cache based on a bundle file name.
  /// </summary>
  /// <param name="bundleFile">
  /// The name of the bundle file. The file does not need to exist.
  /// </param>
  /// <param name="anchor">
  /// The anchor name to use for this bundle file. Optional if <see cref="AnchorRestriction"/>
  /// is not null, otherwise required.
  /// </param>
  public BundleRecord RegisterBundleFile(string bundleFile, string? anchor = null)
  {
    if(anchor != null
      && AnchorRestriction != null
      && !AnchorRestriction.Equals(anchor, StringComparison.OrdinalIgnoreCase))
    {
      throw new ArgumentException(
        $"Anchor name '{anchor}' does not match the anchor restriction '{AnchorRestriction}' " +
        "for this cache.");
    }
    anchor ??= AnchorRestriction;
    if(anchor == null)
    {
      throw new ArgumentNullException(
        nameof(anchor), "Anchor name must not be null for this cache.");
    }
    var key = BundleKey.FromBundleFileName(anchor, bundleFile);
    return GetBundleRecord(key);
  }

  /// <summary>
  /// Register all vault files in the given vault folder, returning a set of
  /// their bundle keys.
  /// </summary>
  /// <param name="vaultFolder">
  /// The folder to scan for vault files.
  /// </param>
  /// <param name="anchor">
  /// The anchor name to use for the vault files. Optional if <see cref="AnchorRestriction"/>
  /// is not null, otherwise required.
  /// </param>
  public HashSet<BundleKey> RegisterVaultFolder(
    string vaultFolder,
    string? anchor = null)
  {
    if(anchor != null
      && AnchorRestriction != null
      && !AnchorRestriction.Equals(anchor, StringComparison.OrdinalIgnoreCase))
    {
      throw new ArgumentException(
        $"Anchor name '{anchor}' does not match the anchor restriction '{AnchorRestriction}' " +
        "for this cache.");
    }
    var keys = new HashSet<BundleKey>();
    anchor ??= AnchorRestriction;
    if(anchor == null)
    {
      throw new ArgumentNullException(
        nameof(anchor), "Anchor name must not be null for this cache.");
    }
    if(Directory.Exists(vaultFolder))
    {
      var di = new DirectoryInfo(vaultFolder);
      foreach(var vaultFile in di.EnumerateFiles("*.mvlt"))
      {
        keys.Add(RegisterVaultFile(vaultFile.FullName, anchor).Key);
      }
    }
    return keys;
  }

  /// <summary>
  /// Register all bundle files in the given bundle folder, returning a set of
  /// their bundle keys.
  /// </summary>
  /// <param name="bundleFolder">
  /// The folder to scan for bundle files.
  /// </param>
  /// <param name="anchor">
  /// The anchor name to use for the bundle files. Optional if <see cref="AnchorRestriction"/>
  /// is not null, otherwise required.
  /// </param>
  public HashSet<BundleKey> RegisterBundleFolder(
    string bundleFolder,
    string? anchor = null)
  {
    if(anchor != null
      && AnchorRestriction != null
      && !AnchorRestriction.Equals(anchor, StringComparison.OrdinalIgnoreCase))
    {
      throw new ArgumentException(
        $"Anchor name '{anchor}' does not match the anchor restriction '{AnchorRestriction}' " +
        "for this cache.");
    }
    var keys = new HashSet<BundleKey>();
    anchor ??= AnchorRestriction;
    if(anchor == null)
    {
      throw new ArgumentNullException(
        nameof(anchor), "Anchor name must not be null for this cache.");
    }
    if(Directory.Exists(bundleFolder))
    {
      var di = new DirectoryInfo(bundleFolder);
      foreach(var bundleFile in di.EnumerateFiles("*.bundle"))
      {
        keys.Add(RegisterBundleFile(bundleFile.FullName, anchor).Key);
      }
    }
    return keys;
  }

  /// <summary>
  /// Ensure the anchor-specific source repository setting has a record in this cache
  /// and validate it.
  /// </summary>
  /// <param name="gitRepo">
  /// The <see cref="GitRepoFolder"/> instance describing the source repository
  /// </param>
  /// <param name="repoSettings">
  /// The anchor segment of the repo's gitvault settings to create a record for
  /// </param>
  /// <returns>
  /// The <see cref="BundleKey"/> identifying the created (or existing) bundle record.
  /// </returns>
  public BundleKey RegisterSourceRepository(
    GitRepoFolder gitRepo,
    AnchorRepoSettings repoSettings)
  {
    if(AnchorRestriction != null
      && !AnchorRestriction.Equals(repoSettings.VaultAnchor, StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException(
        "Attempt to register a source repo for a different gitvault anchor");
    }
    var key = MakeBundleKey(
      repoSettings.VaultAnchor, repoSettings.RepoName, repoSettings.HostName);
    var record = GetBundleRecord(key);
    var recordSource = record.TryGetSourceRepoFolder();
    if(recordSource is null)
    {
      throw new InvalidOperationException(
        "Expecting the bundle record for the source repository to have a source folder");
    }
    if(!FileIdentifier.AreSame(recordSource, gitRepo.Folder))
    {
      throw new InvalidOperationException(
        "Mismatch between actual repo source folder and the registered one: " + 
        $"{gitRepo.Folder} vs {recordSource}");
    }
    return key;
  }
}
