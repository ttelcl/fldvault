using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GitVaultLib.GitThings;

using LibGit2Sharp;

using static FldVault.KeyServer.KeyServerSeedService;

namespace GitVaultLib.Delta;

/// <summary>
/// Helper class for pre-evaluating version 2 delta recipes
/// </summary>
public sealed class DeltaV2Evaluation: IDisposable
{
  private bool _disposed;
  private readonly Repository _repo;
  private readonly Dictionary<string, Commit> _seedCommitsByRef;
  private readonly Dictionary<string, IReadOnlyList<string>> _commitSeedRefMap;
  private readonly Dictionary<string, Commit> _exclusionCommitsBySha;
  private readonly Dictionary<string, Reference> _refCache;
  private readonly Dictionary<string, Commit> _bundleCommits;
  private readonly Dictionary<string, IReadOnlyList<string>> _includedSeeds;
  private readonly Dictionary<string, IReadOnlyList<string>> _droppedSeeds;
  private readonly Dictionary<string, Commit> _tailCommits;

  /// <summary>
  /// Creates a new <see cref="DeltaV2Evaluation"/> instance, without any recipe prepared.
  /// </summary>
  /// <param name="repoRoot"></param>
  public DeltaV2Evaluation(
    string repoRoot)
  {
    _disposed = false;
    _repo = new Repository(repoRoot);
    _seedCommitsByRef = new Dictionary<string, Commit>();
    _commitSeedRefMap = new Dictionary<string, IReadOnlyList<string>>();
    _exclusionCommitsBySha = new Dictionary<string, Commit>();
    _refCache = new Dictionary<string, Reference>();
    _bundleCommits = new Dictionary<string, Commit>();
    _includedSeeds = new Dictionary<string, IReadOnlyList<string>>();
    _droppedSeeds = new Dictionary<string, IReadOnlyList<string>>();
    _tailCommits = new Dictionary<string, Commit>();
    Warnings = new List<string>();
    Errors = ["No recipe prepared"];
  }

  /// <summary>
  /// Warnings found during <see cref="Prepare"/>.
  /// These do not prevent running the recipe, but the user probably should be aware
  /// </summary>
  public List<string> Warnings { get; }

  /// <summary>
  /// Errors found during <see cref="Prepare"/>.
  /// These prevent running the recipe, but did not prevent <see cref="Prepare"/> from
  /// completing
  /// </summary>
  public List<string> Errors { get; }

  /// <summary>
  /// True if a call to <see cref="Prepare"/> completed without errors
  /// </summary>
  public bool CanRun => Recipe != null && Errors.Count == 0;

  /// <summary>
  /// The underlying LibGit2Sharp <see cref="Repository"/> instance.
  /// </summary>
  public Repository Repo => NotDisposedRepo();

  /// <summary>
  /// The recipe that was prepared (null if none)
  /// </summary>
  public DeltaRecipe? Recipe { get; private set; }

  /// <summary>
  /// A mapping of seed reference names to Commits.
  /// </summary>
  public IReadOnlyDictionary<string, Commit> SeedCommitsByRef => _seedCommitsByRef;

  /// <summary>
  /// A mapping of commit IDs to the seed references pointing to them: more or less the inverse of
  /// <see cref="SeedCommitsByRef"/>.
  /// </summary>
  public IReadOnlyDictionary<string, IReadOnlyList<string>> SeedRefsByCommit => _commitSeedRefMap;

  /// <summary>
  /// A mapping of commit IDs to exclusion commits
  /// </summary>
  public IReadOnlyDictionary<string, Commit> ExclusionsCommitsById => _exclusionCommitsBySha;

  /// <summary>
  /// The set of commits to be bundled calculated by <see cref="CalculateBundleCommits"/>
  /// (indexed by their full SHA)
  /// </summary>
  public IReadOnlyDictionary<string, Commit> BundleCommits => _bundleCommits;

  /// <summary>
  /// Seeds that were calculated to not be hidden by exclusions.
  /// Expressed as a mapping of commit IDs to the canonical ref names pointing to them.
  /// </summary>
  public IReadOnlyDictionary<string, IReadOnlyList<string>> IncludedSeeds => _includedSeeds;

  /// <summary>
  /// Seeds that were calculated to be hidden by exclusions.
  /// Expressed as a mapping of commit IDs to the canonical ref names pointing to them.
  /// </summary>
  public IReadOnlyDictionary<string, IReadOnlyList<string>> DroppedSeeds => _droppedSeeds;

  /// <summary>
  /// The collection of "tail commits" (indexed by their SHA): commits that are a parent of
  /// one or more commits in <see cref="BundleCommits"/>, but are themselves not in there.
  /// This is the actual list of exclusions to use when building the bundle. Note that this
  /// collection may include more commits than strictly necessary.
  /// </summary>
  public IReadOnlyDictionary<string, Commit> TailCommits => _tailCommits;

  /// <summary>
  /// Returns a sorted list of all reference names in the values of <see cref="IncludedSeeds"/>.
  /// </summary>
  /// <returns></returns>
  public List<string> IncludedSeedRefs()
  {
    var list = IncludedSeeds.Values.SelectMany(l => l).ToList();
    list.Sort();
    return list;
  }

  /// <summary>
  /// Returns a sorted list of all reference names in the values of <see cref="DroppedSeeds"/>.
  /// </summary>
  /// <returns></returns>
  public List<string> DroppedSeedRefs()
  {
    var list = DroppedSeeds.Values.SelectMany(l => l).ToList();
    list.Sort();
    return list;
  }


  /// <summary>
  /// Create a placeholder <see cref="GitRunResult"/> to convey the errors and warnings
  /// </summary>
  /// <returns></returns>
  public GitRunResult ToErrorResult()
  {
    var result = new GitRunResult();
    result.StatusCode = -1;
    if(Errors.Count > 0)
    {
      result.ErrorLines.Add("Fatal: Recipe preparation failed");
      foreach(var error in Errors)
      {
        result.ErrorLines.Add("Error: " + error);
      }
    }
    foreach(var warning in Warnings)
    {
      result.ErrorLines.Add("Warning: " + warning);
    }
    return result;
  }

  /// <summary>
  /// Prepare a V2 recipe, calculating the actual inclusions and exclusions.
  /// After calling this, check <see cref="Warnings"/> and <see cref="Errors"/> if
  /// anything unusual happened.
  /// </summary>
  /// <param name="recipe"></param>
  /// <returns>
  /// True if preparation succeeded without errors, false if <see cref="Errors"/>
  /// is not empty.
  /// </returns>
  public bool Prepare(DeltaRecipe recipe)
  {
    if(!recipe.V2)
    {
      throw new InvalidOperationException(
        $"Recipe '{recipe.Name}': expecting a version 2 recipe");
    }
    Reset();
    PrepareSeeds(recipe);
    PrepareExclusions(recipe);
    CalculateBundleCommits();
    CalculateEffectiveSeeds();
    CalculateTails();
    Recipe = recipe;
    return CanRun;
  }

  private void CalculateTails()
  {
    foreach(var bundleCommit in _bundleCommits.Values)
    {
      foreach(var parent in bundleCommit.Parents)
      {
        if(!BundleCommits.ContainsKey(parent.Sha))
        {
          _tailCommits[parent.Sha] = parent;
        }
      }
    }
  }
  
  /// <summary>
  /// Partitions <see cref="SeedRefsByCommit"/> into a visible half (<see cref="IncludedSeeds"/>)
  /// and hidden half (<see cref="DroppedSeeds"/>) based on the calculated bundle commits.
  /// </summary>
  private void CalculateEffectiveSeeds()
  {
    foreach(var kvp in SeedRefsByCommit)
    {
      var sha = kvp.Key;
      if(_bundleCommits.TryGetValue(sha, out var commit))
      {
        _includedSeeds[sha] = kvp.Value;
      }
      else
      {
        _droppedSeeds[sha] = kvp.Value;
      }
    }
  }

  /// <summary>
  /// Calculate the set of commits that are expected to appear in the resulting
  /// git bundle from the prepared inclusions and exclusions.
  /// </summary>
  private void CalculateBundleCommits()
  {
    var inclusions = new List<Reference>();
    var exclusions = new List<Commit>();
    var filter = new CommitFilter() {
      IncludeReachableFrom = inclusions,
      ExcludeReachableFrom = exclusions
    };
    inclusions.AddRange(_seedCommitsByRef.Keys.Select(refname => _refCache[refname]));
    exclusions.AddRange(_exclusionCommitsBySha.Values);
    foreach(var commit in Repo.Commits.QueryBy(filter))
    {
      _bundleCommits[commit.Sha] = commit;
    }
    if(_bundleCommits.Count == 0)
    {
      Errors.Add("Empty bundle: all inclusions are hidden by exclusions. There is nothing to bundle.");
    }
  }

  private void PrepareExclusions(DeltaRecipe recipe)
  {
    foreach(var exclusion in recipe.Exclusions)
    {
      if(exclusion.Contains('*'))
      {
        // definitely not a SHA
        var exRefs = TryResolveShortRef(exclusion);
        foreach(var r in exRefs)
        {
          var commit = r.ResolveReferenceToCommit();
          if(commit != null)
          {
            _exclusionCommitsBySha[commit.Sha] = commit;
          }
        }
        if(exRefs.Count == 0)
        {
          Warnings.Add($"Exclusion '{exclusion}' does not match any references");
        }
      }
      else
      {
        var exRefs = TryResolveShortRef(exclusion);
        var commits = exRefs.Select(r => r.ResolveReferenceToCommit()).Where(c => c != null).ToList();
        if(commits.Count == 1)
        {
          var commit = commits[0];
          if(commit != null)
          {
            _exclusionCommitsBySha[commit.Sha] = commit;
          }
        }
        else if(commits.Count > 1)
        {
          Errors.Add($"Exclusion name '{exclusion}' is ambiguous, matching {commits.Count} separate commits");
        }
        else
        {
          // commits.count == 0. Either a ref wasn't found, or it was a commit ID, not a ref
          var commit = Repo.Lookup<Commit>(exclusion);
          if(commit == null)
          {
            Warnings.Add($"Exclusion '{exclusion}' is not a known reference or commit");
          }
          else
          {
            _exclusionCommitsBySha[commit.Sha] = commit;
          }
        }
      }
    }
  }

  private void PrepareSeeds(DeltaRecipe recipe)
  {
    foreach(var seed in recipe.Seeds)
    {
      if(seed.Contains('*'))
      {
        // Expect one or more matches (zero matches should give a warning)
        var seedRefs = TryResolveShortRef(seed);
        foreach(var r in seedRefs)
        {
          var commit = r.ResolveReferenceToCommit();
          if(commit != null)
          {
            _refCache[r.CanonicalName] = r;
            _seedCommitsByRef[r.CanonicalName] = commit;
          }
        }
        if(seedRefs.Count == 0)
        {
          Warnings.Add($"Pattern '{seed}' did not match any existing references");
        }
      }
      else
      {
        // Expect precisely one match. Zero gives a warning, more than one an error.
        var references = TryResolveShortRef(seed);
        if(references.Count == 0)
        {
          Warnings.Add($"Name '{seed}' did not match any existing references");
        }
        else if(references.Count > 1)
        {
          var names = String.Join(", ", references.Select(r => r.CanonicalName));
          Errors.Add($"Name '{seed}' is ambiguous, matching {references.Count} separate references ({names})");
        }
        else
        {
          var reference = references[0];
          var commit = reference.ResolveReferenceToCommit();
          if(commit == null)
          {
            Warnings.Add($"Name '{seed}' did match an existing reference, but not a commit");
          }
          else
          {
            _refCache[reference.CanonicalName] = reference;
            _seedCommitsByRef[reference.CanonicalName] = commit;
          }
        }
      }
    }
    foreach(var group in _seedCommitsByRef.GroupBy(kvp => kvp.Value.Sha, kvp => kvp.Key))
    {
      _commitSeedRefMap[group.Key] = group.ToList();
    }
  }

  /// <summary>
  /// Try to resolve an abbreviated ref name to actual references
  /// </summary>
  /// <param name="abbreviation">
  /// The abbreviation (a branch name, a tag name, a remote branch name, or even a glob for such)
  /// </param>
  /// <returns>
  /// A list of matching references for branches, tags and remote branches. The caller has to decide
  /// what to do if this is empty, contains exactly 1 reference or contains multiple references.
  /// </returns>
  private List<Reference> TryResolveShortRef(string abbreviation)
  {
    if(abbreviation.StartsWith("refs/"))
    {
      return Repo.Refs.FromGlob(abbreviation).ToList();
    }
    var results = new List<Reference>();
    results.AddRange(Repo.Refs.FromGlob("refs/heads/" + abbreviation));
    results.AddRange(Repo.Refs.FromGlob("refs/tags/" + abbreviation));
    results.AddRange(Repo.Refs.FromGlob("refs/remotes/" + abbreviation));
    return results;
  }

  private void Reset()
  {
    Recipe = null;
    Warnings.Clear();
    Errors.Clear();
    _seedCommitsByRef.Clear();
    _commitSeedRefMap.Clear();
    _exclusionCommitsBySha.Clear();
    _refCache.Clear();
    _bundleCommits.Clear();
    _includedSeeds.Clear();
    _droppedSeeds.Clear();
    _tailCommits.Clear();
  }

  /// <summary>
  /// Clean up
  /// </summary>
  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      _repo.Dispose();
    }
  }

  private Repository NotDisposedRepo()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    return _repo;
  }
}
