/*
 * (c) 2026  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace GitVaultLib.Delta;

/// <summary>
/// Information found in the header of a bundle
/// </summary>
public class BundleHeader
{
  private readonly Dictionary<string, string> _seedrefs;
  private readonly HashSet<string> _prerequisites;

  /// <summary>
  /// Create a new BundleReader
  /// </summary>
  public BundleHeader(
    int bundleversion,
    IReadOnlyDictionary<string, string> seedrefs,
    IEnumerable<string> prerequisites)
  {
    Version = bundleversion;
    _seedrefs = seedrefs.ToDictionary(StringComparer.OrdinalIgnoreCase);
    _prerequisites = prerequisites.ToHashSet(StringComparer.OrdinalIgnoreCase);
  }

  /// <summary>
  /// The bundle version (2 or 3)
  /// </summary>
  [JsonProperty("bundleversion")]
  public int Version { get; }

  /// <summary>
  /// A mapping from the seed's ref to their git id.
  /// That may feel the "wrong way around", but keeps in mind that the
  /// refs are unique, while their target commit ids may be shared across
  /// multiple refs.
  /// </summary>
  [JsonProperty("seedrefs")]
  public IReadOnlyDictionary<string, string> SeedRefs => _seedrefs;

  /// <summary>
  /// The bundle requisites (if any)
  /// </summary>
  [JsonProperty("prerequisites")]
  public IReadOnlySet<string> Prerequisites => _prerequisites;

  /// <summary>
  /// Load a <see cref="BundleHeader"/> froma bundle file
  /// </summary>
  /// <param name="fileName"></param>
  /// <returns></returns>
  /// <exception cref="InvalidDataException"></exception>
  public static BundleHeader FromFile(string fileName)
  {
    using var rdr = File.OpenText(fileName);
    var line = rdr.ReadLine();
    if(String.IsNullOrEmpty(line))
    {
      throw new InvalidDataException(
        $"Invalid bundle file '{fileName}' (empty or missing signature)");
    }
    var version =
      line switch {
        "# v2 git bundle" => 2,
        "# v3 git bundle" => 3,
        _ => throw new InvalidDataException(
          $"This file does not look like a git bundle: '{fileName}'"),
      };
    var seedrefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var prereqs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    while((line = rdr.ReadLine()) != null && !String.IsNullOrEmpty(line))
    {
      if(line.StartsWith("@"))
      {
        // ignore capabilities
      }
      else if(line.StartsWith("-"))
      {
        var parts = line.Split(' ', 2);
        var id = parts[0].Trim()[1..];
        if(parts.Length != 2 || id.Length != 40)
        {
          throw new InvalidDataException(
            "Invalid prerequisite header line");
        }
        var comment = parts[1].Trim();
        prereqs.Add(id);
      }
      else
      {
        var parts = line.Split(' ', 2);
        var id = parts[0].Trim();
        if(parts.Length != 2 || id.Length != 40 || !parts[1].StartsWith("refs/"))
        {
          throw new InvalidDataException(
            "Invalid seed header line");
        }
        var gitref = parts[1].Trim();
        // The gitref is unique; the id not necessarily!
        seedrefs.Add(gitref, id);
      }
    }
    var bundleHeader = new BundleHeader(
      version,
      seedrefs,
      prereqs);
    return bundleHeader;
  }

  /// <summary>
  /// Return the raw header lines of the bundle file
  /// </summary>
  /// <param name="fileName"></param>
  /// <returns></returns>
  public static IEnumerable<string> ReadHeaderLines(string fileName)
  {
    using var rdr = File.OpenText(fileName);
    // Beware: the file is actually not text, only the header is. The header ends with
    // and empty line. After that follows binary content.
    // Lines are LF terminated (not CRLF terminated).
    string? line = null;
    while((line = rdr.ReadLine()) != null && !String.IsNullOrEmpty(line))
    {
      yield return line;
    }
  }
}
