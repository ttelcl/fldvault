using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using static FldVault.KeyServer.KeyServerSeedService;

namespace GitVaultLib.Delta;

/// <summary>
/// A named recipe for a delta bundle.
/// </summary>
public class DeltaRecipe
{
  private readonly HashSet<string> _seeds;
  private readonly HashSet<string> _exclusions;

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
  /// <param name="v2">
  /// Version 2 mode (enabling glob patterns). Default false.
  /// </param>
  public DeltaRecipe(string name, IEnumerable<string> seeds, IEnumerable<string> exclusions, bool v2 = false)
  {
    Name = name;
    _seeds = seeds.ToHashSet();
    _exclusions = exclusions.ToHashSet();
    Seeds = _seeds;
    Exclusions = _exclusions;
    V2 = v2;
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
  public IReadOnlySet<string> Seeds { get; }

  /// <summary>
  /// Specifies zero or more refs that are assumed to be known and are excluded from
  /// the delta bundle. These can be branches or tags or commit IDs. If empty the resulting
  /// bundle will be a full bundle instead of a delta bundle.
  /// </summary>
  [JsonProperty("exclusions")]
  public IReadOnlySet<string> Exclusions { get; }

  /// <summary>
  /// Defines the recipe mode (version). If enabled, <see cref="Seeds"/> and <see cref="Exclusions"/>
  /// are pre-evaluated by gitvault, passing a calculated seed and exclusion set to git.exe. If disabled
  /// seeds and exclusions are passed directly to git.exe. V2 mode allows seeds and exclusions to be glob
  /// patterns.
  /// </summary>
  [JsonProperty("v2")]
  public bool V2 { get; private set; }

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

  private static string ParseRemote(string remoteArg)
  {
    if(!remoteArg.StartsWith("--remote="))
    {
      throw new ArgumentException(
        "Expecting argument to start with '--remote='");
    }
    return $"refs/remotes/{remoteArg[9..]}/*";
  }
  
  /// <summary>
  /// Add a seed or pseudo-seed
  /// </summary>
  /// <param name="seed"></param>
  /// <exception cref="ArgumentException"></exception>
  public void AddSeed(string seed)
  {
    if(seed.StartsWith("--"))
    {
      if(V2)
      {
        // translate to full version
        seed = seed switch {
          "--branches" => "refs/heads/*",
          "--tags" => "refs/tags/*",
          "--remotes" => "refs/remotes/*",
          _ =>
            seed.StartsWith("--remote=")
            ? ParseRemote(seed)
            : throw new ArgumentException(
                $"Unrecognized V2 seed alias '{seed}' (expecting --branches, --tags, --remotes, or --remote=REMOTENAME)")
        };
      }
      else
      {
        if(seed!="--branches" && seed!="--tags")
        {
          throw new ArgumentException(
            $"Not a valid V1 pseudo-seed name: '{seed}' (expecting '--branches' or '--tags')",
            nameof(seed));
        }
      }
    }
    if(seed.Contains('*'))
    {
      if(V2)
      {
        if(!seed.StartsWith("refs/"))
        {
          throw new ArgumentException(
            $"'{seed}': Glob patterns must be fully qualified: they must start with 'refs/'");
        }
      }
      else
      {
        throw new ArgumentException(
          $"'{seed}': Glob patterns are only supported in V2 recipes");
      }
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
    if(V2)
    {
      if(exclusion.StartsWith("--"))
      {
        // translate to full version
        exclusion = exclusion switch {
          "--branches" => "refs/heads/*",
          "--tags" => "refs/tags/*",
          "--remotes" => "refs/remotes/*",
          _ =>
            exclusion.StartsWith("--remote=")
            ? ParseRemote(exclusion)
            : throw new ArgumentException(
                $"Unrecognized V2 seed alias '{exclusion}' (expecting --branches, --tags, --remotes, or --remote=REMOTENAME)")
        };
      }
      if(exclusion.Contains('*'))
      {
        if(!exclusion.StartsWith("refs/"))
        {
          throw new ArgumentException(
            $"'{exclusion}': Glob patterns must be fully qualified: they must start with 'refs/'");
        }
      }
    }
    else
    {
      if(exclusion.StartsWith("--"))
      {
        throw new ArgumentException(
          $"'{exclusion}': V1 recipes do not support aliases as exclusions");
      }
      if(exclusion.Contains("*"))
      {
        throw new ArgumentException(
          $"'{exclusion}': V1 recipes do not support glob patterns as exclusions");
      }
    }
    Modified = true;
    _seeds.Remove(exclusion); // just in case
    _exclusions.Add(exclusion);
  }

}
