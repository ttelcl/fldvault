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
    [JsonProperty("bundle-anchors")] Dictionary<string, string> bundleAnchors)
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
    BundleAnchors = _bundleAnchors;
    foreach(var kv in bundleAnchors)
    {
      _bundleAnchors[kv.Key] = kv.Value;
    }

    if(!_bundleAnchors.ContainsKey("default"))
    {
      var defaultBundleAnchor = Path.Combine(
        DefaultCentralSettingsFolder,
        "bundles");
      if(!Directory.Exists(defaultBundleAnchor))
      {
        Directory.CreateDirectory(defaultBundleAnchor);
      }
      _bundleAnchors["default"] = defaultBundleAnchor;
      Modified = true;
    }

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
        Environment.MachineName,
        []);
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
  /// Folders elegible as bundle anchors. Normally this only
  /// contains the anchor named "default".
  /// Bundle anchor folder must be NOT inside a cloud-backed folder, but in
  /// a local disk folder.
  /// </summary>
  [JsonProperty("bundle-anchors")]
  public IReadOnlyDictionary<string, string> BundleAnchors { get; }

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
  /// Add a new anchor folder to the settings.
  /// </summary>
  /// <param name="name">
  /// The name for the new anchor. This name must be unique and valid.
  /// </param>
  /// <param name="anchorFolder">
  /// The folder to use as the anchor. This folder must exist and is
  /// intended to be a cloud-backed folder. If the leaf folder name isn't
  /// "GitVault", a child folder by that name will be used and created.
  /// </param>
  /// <exception cref="ArgumentException"></exception>
  /// <exception cref="DirectoryNotFoundException"></exception>
  public void AddAnchor(string name, string anchorFolder)
  {
    if(_anchors.ContainsKey(name))
    {
      throw new ArgumentException(
        $"Anchor {name} already exists");
    }
    if(!IsValidName(name, false))
    {
      throw new ArgumentException(
        $"Anchor name {name} is not valid as an anchor name. " +
        "Only letters, digits and '-' are allowed");
    }
    anchorFolder = Path.GetFullPath(anchorFolder).TrimEnd('/', '\\');
    if(!Directory.Exists(anchorFolder))
    {
      throw new DirectoryNotFoundException(
        $"Anchor path {anchorFolder} does not exist");
    }
    var anchorLeaf = Path.GetFileName(anchorFolder);
    if(!anchorLeaf.Equals("GitVault", StringComparison.OrdinalIgnoreCase))
    {
      anchorFolder = Path.Combine(anchorFolder, "GitVault");
      if(!Directory.Exists(anchorFolder))
      {
        Directory.CreateDirectory(anchorFolder);
      }
    }
    var anchorFid =
      FileIdentifier.FromPath(anchorFolder)
      ?? throw new ArgumentException(
        $"Anchor path {anchorFolder} is not accessible");
    foreach(var kv in _anchors)
    {
      if(anchorFid.SameAs(kv.Value))
      {
        throw new ArgumentException(
          $"Anchor folder {anchorFolder} points to the same as anchor '{kv.Key}' ({kv.Value})");
      }
    }
    _anchors[name] = anchorFolder;
    Modified = true;
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

}
