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
  private readonly HashSet<string> _tipCommits;
  private readonly HashSet<string> _tipReferences;
  private readonly HashSet<string> _tailCommits;
  private readonly Dictionary<string, Commit> _seedCommitsByRef;
  private readonly Dictionary<string, IReadOnlyList<string>> _commitSeedRefMap;
  private readonly Dictionary<string, Commit> _excludeCommitsBySha;

  /// <summary>
  /// Creates a new <see cref="DeltaV2Evaluation"/> instance, without any recipe prepared.
  /// </summary>
  /// <param name="repoRoot"></param>
  public DeltaV2Evaluation(
    string repoRoot)
  {
    _disposed = false;
    _repo = new Repository(repoRoot);
    _tipCommits = new HashSet<string>();
    _tipReferences = new HashSet<string>();
    _tailCommits = new HashSet<string>();
    _seedCommitsByRef = new Dictionary<string, Commit>();
    _commitSeedRefMap = new Dictionary<string, IReadOnlyList<string>>();
    _excludeCommitsBySha = new Dictionary<string, Commit>();
    Warnings = new List<string>();
    Errors = ["No recipe prepared"];
  }

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
  public IReadOnlyDictionary<string, Commit> SeedCommitMap => _seedCommitsByRef;

  /// <summary>
  /// A mapping of commit IDs to the seed references pointing to them: more or less the inverse of
  /// <see cref="SeedCommitMap"/>.
  /// </summary>
  public IReadOnlyDictionary<string, IReadOnlyList<string>> SeedRefsByCommit => _commitSeedRefMap;

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
    var repo = Repo;
    if(!recipe.V2)
    {
      throw new InvalidOperationException(
        $"Recipe '{recipe.Name}': expecting a version 2 recipe");
    }
    Reset();
    foreach(var seed in recipe.Seeds)
    {
      if(seed.Contains('*'))
      {
        var seedRefs = TryResolveShortRef(seed); // repo.Refs.FromGlob(seed).ToList();
        foreach(var r in seedRefs)
        {
          var commit = r.ResolveReferenceToCommit();
          if(commit != null)
          {
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
            _seedCommitsByRef[reference.CanonicalName] = commit;
          }
        }
      }
    }
    foreach(var group in _seedCommitsByRef.GroupBy(kvp => kvp.Value.Sha, kvp => kvp.Key))
    {
      _commitSeedRefMap[group.Key] = group.ToList();
    }
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
            _excludeCommitsBySha[commit.Sha] = commit;
          }
        }
        if(exRefs.Count == 0)
        {
          Warnings.Add($"Exclusion '{exclusion}' does not match any references");
        }
      }
      else
      {
        // NYI: resolve exclusion to commit via reference or as SHA
      }
    }

    Errors.Add("[Implementation incomplete]");

    Recipe = recipe;
    return CanRun;
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

  private void Reset()
  {
    Recipe = null;
    Warnings.Clear();
    Errors.Clear();
    _tipCommits.Clear();
    _tipReferences.Clear();
    _tailCommits.Clear();
    _seedCommitsByRef.Clear();
    _commitSeedRefMap.Clear();
    _excludeCommitsBySha.Clear();
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
