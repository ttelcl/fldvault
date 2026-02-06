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
/// Describes a bundle seed as found in a bundle header
/// </summary>
public class BundleSeed
{
  /// <summary>
  /// Create a new BundleSeed
  /// </summary>
  public BundleSeed(string id, string gitref)
  {
    Id = id;
    GitRef = gitref;
  }

  /// <summary>
  /// The (commit) id the seed points to. For seeds this is not necessarily unique.
  /// </summary>
  [JsonProperty("id")]
  public string Id { get; }

  /// <summary>
  /// The full ref path of the seed (branch, tag, or other ref)
  /// </summary>
  [JsonProperty("gitref")]
  public string GitRef { get; }

}
