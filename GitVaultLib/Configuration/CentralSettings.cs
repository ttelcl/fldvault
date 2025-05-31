/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using FileUtilities;

using FldVault.Core.Vaults;

using GitVaultLib.VaultThings;

using Newtonsoft.Json;

namespace GitVaultLib.Configuration;

/// <summary>
/// Central settings for gitvault.
/// </summary>
public class CentralSettings
{
  private readonly Dictionary<string, string> _anchors;
  private readonly Dictionary<string, string> _bundleAnchors;

  /// <summary>
  /// Create a new CentralSettings
  /// </summary>
  public CentralSettings(
    Dictionary<string, string> anchors,
    string hostname,
    [JsonProperty("bundle-anchor")] string? bundleAnchor = null,
    [JsonProperty("bundle-anchors")] Dictionary<string, string>? bundleAnchors = null)
  {
    _anchors = new Dictionary<string, string>(
      StringComparer.OrdinalIgnoreCase);
    Anchors = _anchors;
    foreach(var kv in anchors)
    {
      _anchors[kv.Key] = kv.Value;
    }

    _bundleAnchors = new Dictionary<string, string>(
      StringComparer.OrdinalIgnoreCase);
#pragma warning disable CS0618 // Type or member is obsolete
    BundleAnchors = _bundleAnchors;
#pragma warning restore CS0618 // Type or member is obsolete
    if(bundleAnchors != null)
    {
      foreach(var kv in bundleAnchors)
      {
        _bundleAnchors[kv.Key] = kv.Value;
      }
    }
    if(String.IsNullOrEmpty(bundleAnchor))
    {
      _bundleAnchors.TryGetValue("default", out bundleAnchor);
    }
    if(String.IsNullOrEmpty(bundleAnchor))
    {
      var defaultBundleAnchor = Path.Combine(
        DefaultCentralSettingsFolder,
        "bundles");
      if(!Directory.Exists(defaultBundleAnchor))
      {
        Directory.CreateDirectory(defaultBundleAnchor);
      }
      bundleAnchor = defaultBundleAnchor;
      Modified = true;
    }
    _bundleAnchors["default"] = bundleAnchor;
    BundleAnchor = bundleAnchor;

    DefaultHostname = hostname;
  }

  /// <summary>
  /// Load the default settings file (creating it if not found)
  /// </summary>
  /// <returns></returns>
  /// <exception cref="InvalidDataException"></exception>
  public static CentralSettings Load()
  {
    if(!Directory.Exists(DefaultCentralSettingsFolder))
    {
      Directory.CreateDirectory(DefaultCentralSettingsFolder);
    }
    CentralSettings settings;
    if(!File.Exists(CentralSettingsFileName))
    {
      settings = new CentralSettings(
        [],
        Environment.MachineName);
      settings.Modified = true;
      settings.SaveIfModified();
      return settings;
    }
    var json = File.ReadAllText(CentralSettingsFileName);
    settings =
      JsonConvert.DeserializeObject<CentralSettings>(json)
      ?? throw new InvalidDataException(
        $"failed to parse {CentralSettingsFileName}");
    return settings;
  }

  /// <summary>
  /// The default folder for the central settings and bundles.
  /// </summary>
  public static string DefaultCentralSettingsFolder { get; } =
    Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "GitVault");

  /// <summary>
  /// The default file name for the central settings.
  /// </summary>
  public static string CentralSettingsFileName { get; } =
    Path.Combine(
      DefaultCentralSettingsFolder,
      "gitvault-settings.json");

  /// <summary>
  /// Maps anchor names to vault anchor folders. Vault anchor folders should
  /// be inside a cloud-backed folder. Folder names will always end with
  /// "GitVault" as leaf folder name.
  /// </summary>
  [JsonProperty("anchors")]
  public IReadOnlyDictionary<string, string> Anchors { get; }

  /// <summary>
  /// DEPRECATED - retained for backward compatibility.
  /// Replaced by <see cref="BundleAnchor"/>; the only valid entry here is "default".
  /// Folders elegible as bundle anchors. Normally this only
  /// contains the anchor named "default".
  /// Bundle anchor folder must be NOT inside a cloud-backed folder, but in
  /// a local disk folder.
  /// </summary>
  [JsonProperty("bundle-anchors")]
  [Obsolete("Use BundleAnchor instead")]
  public IReadOnlyDictionary<string, string> BundleAnchors { get; }

  /// <summary>
  /// Test if the bundle anchors dictionary should be serialized. It is expected that
  /// it is not.
  /// </summary>
  public bool ShouldSerializeBundleAnchors()
  {
    // This is to avoid serializing the bundle anchors if they are already determined
    // by the BundleAnchor property.
#pragma warning disable CS0618 // Type or member is obsolete
    return BundleAnchors.Count != 1 ||
           !BundleAnchors.ContainsKey("default") ||
           BundleAnchors["default"] != BundleAnchor;
#pragma warning restore CS0618 // Type or member is obsolete
  }

  /// <summary>
  /// The one anchor folder for bundles (previously 'BundleAnchors["default"]').
  /// This must be a folder that is local, not cloud backed up.
  /// </summary>
  [JsonProperty("bundle-anchor")]
  public string BundleAnchor { get; }

  /// <summary>
  /// The default hostname for repositories
  /// </summary>
  [JsonProperty("hostname")]
  public string DefaultHostname { get; set; }

  /// <summary>
  /// True if the settings have been modified and need to be saved.
  /// </summary>
  [JsonIgnore]
  public bool Modified { get; set; } = false;

  /// <summary>
  /// Save the settings to the default file name if the
  /// <see cref="Modified"/> property is true.
  /// </summary>
  public void SaveIfModified()
  {
    if(Modified)
    {
      var json = JsonConvert.SerializeObject(
        this,
        Formatting.Indented);
      File.WriteAllText(
        CentralSettingsFileName,
        json);
    }
    Modified = false;
  }

  /// <summary>
  /// Try to add a new anchor folder to the settings.
  /// Returns null on success, or an error message on failure.
  /// </summary>
  /// <param name="name">
  /// The name for the new anchor. This name must be unique and valid.
  /// If null, and the leaf of <paramref name="anchorFolder"/> is a multi-segment
  /// name in which one of the segments is "gitvault", that segment the
  /// anchor name is contructed from the remaining segments.
  /// </param>
  /// <param name="anchorFolder">
  /// The folder to use as the anchor. This folder must exist and is
  /// intended to be a cloud-backed folder. If the leaf folder name isn't
  /// "GitVault", and is not a multi-segment name in which one of the segments
  /// is "gitvault", a child folder named "{name}.gitvault" will be created
  /// and used as the anchor folder.
  /// </param>
  /// <param name="entry">
  /// Returns the actual entry added to the anchors dictionary. The name and
  /// folder may differ from the parameters. Returns null if the anchor was
  /// not registered due to an error.
  /// </param>
  public string? TryAddAnchor(
    string? name, string anchorFolder, out KeyValuePair<string, string>? entry)
  {
    entry = null;
    anchorFolder = Path.GetFullPath(anchorFolder).TrimEnd('/', '\\');
    var anchorLeaf = Path.GetFileName(anchorFolder);
    var anchorLeafSegments = anchorLeaf.Split(
      ['.'],
      StringSplitOptions.RemoveEmptyEntries);
    var gitVaultParts =
      anchorLeafSegments.Where(p => p.Equals("gitvault", StringComparison.OrdinalIgnoreCase))
      .ToList();
    if(string.IsNullOrWhiteSpace(name))
    {
      if(gitVaultParts.Count != 1)
      {
        return
          "No anchor name given and cannot derive it from anchor folder: " +
          $"leaf folder '{anchorLeaf}' must have a single 'gitvault' part in the name, " +
          "e.g. foo.gitvault or gitvault.foo";
      }
      var notGitVaultParts =
        anchorLeafSegments.Where(p => !p.Equals("gitvault", StringComparison.OrdinalIgnoreCase))
        .ToList();
      if(notGitVaultParts.Count == 0)
      {
        return
          "No anchor name given and cannot derive it from anchor folder: " +
          $"leaf folder '{anchorLeaf}' must have at least one part other than 'gitvault', " +
          "e.g. foo.gitvault or gitvault.foo";
      }
      // Anything with more than one part won't be valid, but let the check later on to
      // fail that, to make the error message more clear.
      name = String.Join(".", notGitVaultParts);
    }
    if(_anchors.ContainsKey(name))
    {
      return $"Anchor {name} already exists";
    }
    if(!IsValidAnchor(name))
    {
      return
        $"Anchor name {name} is not valid as an anchor name. " +
        "Only letters, digits and '-' are allowed";
    }
    if(gitVaultParts.Count == 0)
    {
      anchorFolder = Path.Combine(anchorFolder, $"{name}.gitvault");
    }
    var anchorParent = Path.GetDirectoryName(anchorFolder);
    if(!Directory.Exists(anchorParent))
    {
      return
        $"Parent folder of Anchor path {anchorFolder} must already exist: {anchorParent} ";
    }
    if(!Directory.Exists(anchorFolder))
    {
      Directory.CreateDirectory(anchorFolder);
    }
    var anchorFid = FileIdentifier.FromPath(anchorFolder);
    if(anchorFid == null)
    {
      return $"Anchor path {anchorFolder} is not accessible";
    }
    foreach(var kv in _anchors)
    {
      if(anchorFid.SameAs(kv.Value))
      {
        return
          $"Anchor folder {anchorFolder} points to the same as anchor '{kv.Key}' ({kv.Value})";
      }
    }
    _anchors[name] = anchorFolder;
    entry = new KeyValuePair<string, string>(name, anchorFolder);
    Modified = true;
    SaveIfModified();
    return null;
  }

  /// <summary>
  /// Create a new BundleRecord instance for the given bundle key triplet.
  /// </summary>
  public BundleRecord CreateBundleRecord(BundleKey key)
  {
    return new BundleRecord(this, key);
  }

  /// <summary>
  /// Checks that the name is valid for use in gitvault parts
  /// (anchors, hosts, etc.)
  /// </summary>
  /// <param name="itemName">
  /// </param>
  /// <param name="allowDots">
  /// When true, dots are allowed in the name. This is used for
  /// repository names, where dots are allowed.
  /// </param>
  /// <returns></returns>
  public static bool IsValidName(string itemName, bool allowDots)
  {
    return allowDots
      ? Regex.IsMatch(
          itemName,
          @"^([a-z][a-z0-9]*)([-._][a-z0-9]+)*$",
          RegexOptions.IgnoreCase)
      : Regex.IsMatch(
          itemName,
          @"^([a-z][a-z0-9]*)([-_][a-z0-9]+)*$",
          RegexOptions.IgnoreCase);
  }

  /// <summary>
  /// Test if the name is a valid anchor name in addition to 
  /// IsValidName(name, false), this also checks that the name is not
  /// 'gitvault' (case insensitive).
  /// </summary>
  /// <param name="name"></param>
  /// <returns></returns>
  public static bool IsValidAnchor(string name)
  {
    return IsValidName(name, false) && !name.Equals("gitvault", StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Enumerates vault folders within the vault anchor folder identified by
  /// <paramref name="anchorName"/>. To be elegible to be considered a vault folder,
  /// a child folder of the anchor folder must have a valid repo name, define one
  /// single unique zkey (possibly reused in multiple *.zkey and *.mvlt files) and
  /// contain a 'git-roots.json' file.
  /// </summary>
  /// <param name="anchorName"></param>
  /// <returns></returns>
  public IEnumerable<RepoVaultFolder> EnumerateRepoVaultFolders(string anchorName)
  {
    if(!Anchors.TryGetValue(anchorName, out var anchorFolder))
    {
      throw new ArgumentException(
        $"Unknown vault anchor '{anchorName}'",
        nameof(anchorName));
    }
    var anchorInfo = new DirectoryInfo(anchorFolder);
    var vaultCandidates = anchorInfo.GetDirectories();
    foreach(var vaultCandidate in vaultCandidates)
    {
      var repoName = vaultCandidate.Name;
      if(!IsValidName(repoName, true))
      {
        continue;
      }
      var gitRootsName = Path.Combine(vaultCandidate.FullName, "git-roots.json");
      if(!File.Exists(gitRootsName))
      {
        continue;
      }
      var folderKeys = FolderKey.KeysInFolder(vaultCandidate.FullName).Values;
      if(folderKeys.Count != 1)
      {
        continue;
      }
      var rvf = new RepoVaultFolder(anchorFolder, repoName);
      yield return rvf;
    }
  }

}
