/*
 * (c) 2026  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace GitVaultLib.Delta;

/// <summary>
/// Describes a bundle prerequisite found in a git bundle header
/// </summary>
public class BundlePrerequisite
{
  /// <summary>
  /// Create a new BundleRequirement
  /// </summary>
  public BundlePrerequisite(
    string id,
    string comment)
  {
    Id = id;
    Comment = comment;
  }

  /// <summary>
  /// The Id of the prerequisite. This typically is unique
  /// </summary>
  [JsonProperty("id")]
  public string Id { get; }

  /// <summary>
  /// The comment for the prerequisite. Purely informational.
  /// </summary>
  [JsonProperty("comment")]
  public string Comment { get; }
}
