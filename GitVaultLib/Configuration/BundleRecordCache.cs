/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

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

}
