/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Utilities;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FldVault.Core.Zvlt2
{
  /// <summary>
  /// Metadata about a file stored in a vault
  /// </summary>
  public class FileMetadata
  {
    /// <summary>
    /// Create a new FileMetadata
    /// </summary>
    public FileMetadata(
      string? name = null,
      long? stamp = null,
      long? size = null)
    {
      OtherFields = new Dictionary<string, JToken?>();
      Name = name;
      Stamp = stamp;
      Size = size;
      if(name != null)
      {
        ValidateName(name);
      }
    }

    /// <summary>
    /// The name of the file, or null for anonymous entries.
    /// This may include a relative path,
    /// but must use '/' as path separator, and none of the segments
    /// can be '..' nor '.', nor end with '.'.
    /// </summary>
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; init; }

    /// <summary>
    /// Last write time stamp expressed as epoch ticks, or null if not
    /// available. Use <see cref="UtcStamp"/> to get the equivalent DateTime.
    /// </summary>
    [JsonProperty("stamp", NullValueHandling = NullValueHandling.Ignore)]
    public long? Stamp { get; init; }

    /// <summary>
    /// <see cref="Stamp"/> converted to a UTC <see cref="DateTime"/>.
    /// </summary>
    [JsonIgnore]
    public DateTime? UtcStamp { get => Stamp.HasValue ? EpochTicks.ToUtc(Stamp.Value) : null; }

    /// <summary>
    /// The size of the file, or null if unknown
    /// </summary>
    [JsonProperty("size", NullValueHandling = NullValueHandling.Ignore)]
    public long? Size { get; init; }

    /// <summary>
    /// Contains fields from the JSON representation that are not
    /// explicitly handled otherwise
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JToken?> OtherFields { get; init; }

    /// <summary>
    /// Check if a non-null "file name" meets the requirements for
    /// a file name recorded in a metadata record, throwing an
    /// <see cref="ArgumentException"/> if it isn't.
    /// </summary>
    public static void ValidateName(string name)
    {
      if(name.IndexOfAny(new[] { ':', '\\' }) >= 0)
      {
        throw new ArgumentException(
          "Invalid character(s) in file name. If it has a path, it must be relative and use '/' as path separator.");
      }
      if(name.IndexOfAny(new[] { '|', '<', '>' }) >= 0)
      {
        throw new ArgumentException(
          "Invalid character(s) in file name.");
      }
      var parts = name.Split('/');
      if(parts.Any(part => String.IsNullOrEmpty(part)))
      {
        throw new ArgumentException(
          "Invalid file name: empty path segments are not allowed (that also forbids a leading or trailing '/')");
      }
      if(parts.Any(part => part == "." || part == ".."))
      {
        throw new ArgumentException(
          "Invalid file name: path segments '..' and '.' are not allowed.");
      }
      if(parts.Any(part => part.EndsWith('.')))
      {
        throw new ArgumentException(
          "Invalid file name: path segments must not end with '.'");
      }
    }

    /// <summary>
    /// Insert all entries found in <paramref name="extraMeta"/> into
    /// <see cref="OtherFields"/>. The values are cloned. Fields
    /// "name", "size", and "stamp" are ignored.
    /// </summary>
    /// <param name="extraMeta">
    /// The container carrying the extra fields. Note that <see cref="JObject"/>
    /// is compatible with this type.
    /// </param>
    public void MergeMetadata(IDictionary<string, JToken?> extraMeta)
    {
      foreach(var kvp in extraMeta)
      {
        if(kvp.Key != "name" && kvp.Key != "size" && kvp.Key != "stamp")  // else: ignore
        {
          OtherFields[kvp.Key] = kvp.Value?.DeepClone();
        }
      }
    }

    /// <summary>
    /// Try to merge metadata from the specified file, returning true
    /// if successful. False indicates failure, which may because of
    /// the file not existing, or the file not containing a json object.
    /// </summary>
    /// <param name="fileName">
    /// The json file to merge.
    /// </param>
    public bool TryMergeMetadata(string fileName)
    {
      if(File.Exists(fileName))
      {
        var json = File.ReadAllText(fileName);
        try
        {
          var jobject = JsonConvert.DeserializeObject<JObject>(json);
          if(jobject != null)
          {
            MergeMetadata(jobject);
            return true;
          }
          else
          {
            return false;
          }
        }
        catch(Exception)
        {
          return false;
        }
      }
      else
      {
        return false;
      }
    }

  }
}
