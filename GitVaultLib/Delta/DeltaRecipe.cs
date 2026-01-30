using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace GitVaultLib.Delta;

/// <summary>
/// A named recipe for a delta bundle.
/// </summary>
public class DeltaRecipe
{
  private readonly List<string> _seeds;
  private readonly List<string> _exclusions;

  /// <summary>
  /// Create a new <see cref="DeltaRecipe"/>. Also used as JSON constructor.
  /// </summary>
  /// <param name="name">
  /// The recipe name
  /// </param>
  /// <param name="seeds">
  /// The initial content for <see cref="Seeds"/>.
  /// </param>
  /// <param name="exclusions">
  /// The initial content for <see cref="Exclusions"/>.
  /// </param>
  public DeltaRecipe(string name, IEnumerable<string> seeds, IEnumerable<string> exclusions)
  {
    Name = name;
    _seeds = seeds.ToList();
    _exclusions = exclusions.ToList();
    Seeds = _seeds.AsReadOnly();
    Exclusions = _exclusions.AsReadOnly();
  }

  /// <summary>
  /// The name identifying this recipe
  /// </summary>
  [JsonProperty("name")]
  public string Name { get; }

  /// <summary>
  /// Specifies one or more refs or options to identify seed commits. These can name
  /// branches (optionally prefixed with "heads/"), tags (optionally prefixed with
  /// "tags/"). Alternatively these can be the options to include a group of seeds:
  /// "--branches", "--tags", "--all". Note that commit IDs are not valid here.
  /// </summary>
  [JsonProperty("seeds")]
  public IReadOnlyList<string> Seeds { get; }

  /// <summary>
  /// Specifies zero or more refs that are assumed to be known and are excluded from
  /// the delta bundle. These can be branches or tags or commit IDs. If empty the resulting
  /// bundle will be a full bundle instead of a delta bundle.
  /// </summary>
  [JsonProperty("exclusions")]
  public IReadOnlyList<string> Exclusions { get; }

  /// <summary>
  /// A flag set when <see cref="Zap"/>, <see cref="AddSeed(string)"/> or <see cref="AddExclusion(string)"/>
  /// is called.
  /// </summary>
  [JsonIgnore]
  public bool Modified { get; internal set; }

  /// <summary>
  /// Remove a seed or exclusion by name
  /// </summary>
  /// <param name="seedOrExclusion"></param>
  /// <returns></returns>
  public bool Zap(string seedOrExclusion)
  {
    Modified = true;
    // Assume it exists in either seeds or exclusions, but not both
    return _seeds.Remove(seedOrExclusion) || _exclusions.Remove(seedOrExclusion);
  }

  /// <summary>
  /// Add a seed or pseudo-seed
  /// </summary>
  /// <param name="seed"></param>
  /// <exception cref="ArgumentException"></exception>
  public void AddSeed(string seed)
  {
    if(seed.StartsWith("--") && !(seed=="--all" || seed=="--branches" || seed=="--tags"))
    {
      throw new ArgumentException(
        $"Not a valid pseudo-seed name: '{seed}'",
        nameof(seed));
    }
    Modified = true;
    _exclusions.Remove(seed); // just in case
    _seeds.Add(seed);
  }

  /// <summary>
  /// Add an exclusion
  /// </summary>
  /// <param name="exclusion"></param>
  public void AddExclusion(string exclusion)
  {
    Modified = true;
    _seeds.Remove(exclusion); // just in case
    _exclusions.Add(exclusion);
  }
}
