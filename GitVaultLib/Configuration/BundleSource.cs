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

using Newtonsoft.Json;

namespace GitVaultLib.Configuration;

/// <summary>
/// Tracks local source folder for GIT bundles. Used to prevent
/// duplicates.
/// </summary>
public class BundleSource
{
  /// <summary>
  /// Create a new BundleSource
  /// </summary>
  public BundleSource(
    [JsonProperty("source-folder")] string sourceFolder)
  {
    SourceFolder=sourceFolder;
  }

  /// <summary>
  /// The Git repository root folder that is the source for this bundle.
  /// </summary>
  [JsonProperty("source-folder")]
  public string SourceFolder { get; }

  /// <summary>
  /// Save the BundleSource to a JSON file.
  /// </summary>
  public void Save(string fileName)
  {
    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
    File.WriteAllText(fileName, json);
  }

  /// <summary>
  /// Try to load a BundleSource from the given file name.
  /// </summary>
  /// <param name="fileName"></param>
  public static BundleSource? TryLoad(string fileName)
  {
    if(!File.Exists(fileName))
    {
      return null;
    }
    var content = File.ReadAllText(fileName);
    return JsonConvert.DeserializeObject<BundleSource>(content) ??
           throw new InvalidOperationException($"Failed to deserialize BundleSource from '{fileName}'.");
  }

  /// <summary>
  /// Test if this and <paramref name="other"/> point to the same source folder on disk.
  /// </summary>
  public bool SameSource(BundleSource other)
  {
    if(other == null)
    {
      throw new ArgumentNullException(nameof(other));
    }
    var fidThis = FileIdentifier.FromPath(SourceFolder);
    var fidOther = FileIdentifier.FromPath(other.SourceFolder);
    if(fidThis == null)
    {
      throw new InvalidOperationException(
        $"Cannot access {SourceFolder}");
    }
    if(fidOther == null)
    {
      throw new InvalidOperationException(
        $"Cannot access {other.SourceFolder}");
    }
    return fidThis.SameAs(fidOther);
  }
}
