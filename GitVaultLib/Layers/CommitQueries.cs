using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LibGit2Sharp;

namespace GitVaultLib.Layers;

/// <summary>
/// Static (extension) methods wrapping the commit querying (commit graph walking) functionality in
/// LibGit2Sharp
/// </summary>
public static class CommitQueries
{

  /// <summary>
  /// Return all commits in <paramref name="repo"/> reachable by <paramref name="query"/>.
  /// </summary>
  /// <param name="repo"></param>
  /// <param name="query">
  /// The query (commit filter) to use. If no includes are specified, <see cref="Repository.Head"/>
  /// is used by this method (unlike other extension methods in this class that return an
  /// empty collection instead)
  /// </param>
  /// <returns></returns>
  public static IEnumerable<Commit> QueryCommits(this Repository repo, CommitFilter query)
  {
    return repo.Commits.QueryBy(query);
  }

  /// <summary>
  /// Return all commits in <paramref name="repo"/> reachable by walking from
  /// <paramref name="includeReferences"/> but excluding commits reachable from
  /// <paramref name="excludeCommits"/>.
  /// </summary>
  /// <remarks>
  /// This implements just one common use case (tips defined as references).
  /// For full flexibility use <see cref="QueryCommits(Repository, CommitFilter)"/>
  /// with a custom <see cref="CommitFilter"/>.
  /// </remarks>
  /// <param name="repo"></param>
  /// <param name="includeReferences">
  /// The "tip" references to start walking the commit graph from.
  /// If empty, an empty enumeration is returned.
  /// </param>
  /// <param name="excludeCommits">
  /// The "tail" references defining where to stop the graph walk. If
  /// <see langword="null"/> then there are no stops and the walk continues
  /// down to the root(s) of the graph.
  /// </param>
  /// <returns></returns>
  public static IEnumerable<Commit> QueryCommits(
    this Repository repo,
    IEnumerable<Reference> includeReferences,
    IEnumerable<Commit>? excludeCommits = null)
  {
    var query = new CommitFilter();
    var includes = includeReferences.ToList();
    if(includes.Count == 0)
    {
      return [];
    }
    query.IncludeReachableFrom = includes;
    if(excludeCommits != null)
    {
      query.ExcludeReachableFrom = excludeCommits.ToList();
    }
    return repo.Commits.QueryBy(query);
  }

  /// <summary>
  /// Return all commits in <paramref name="repo"/> reachable by walking from
  /// <paramref name="includeCommits"/> but excluding commits reachable from
  /// <paramref name="excludeCommits"/>.
  /// </summary>
  /// <remarks>
  /// This implements just one common use case (tips defined as commits).
  /// For full flexibility use <see cref="QueryCommits(Repository, CommitFilter)"/>
  /// with a custom <see cref="CommitFilter"/>.
  /// </remarks>
  /// <param name="repo"></param>
  /// <param name="includeCommits">
  /// The "tip" commits to start walking the commit graph from
  /// If empty, an empty enumeration is returned.
  /// </param>
  /// <param name="excludeCommits">
  /// The "tail" references defining where to stop the graph walk. If
  /// <see langword="null"/> then there are no stops and the walk continues
  /// down to the root(s) of the graph.
  /// </param>
  /// <returns></returns>
  public static IEnumerable<Commit> QueryCommits(
    this Repository repo,
    IEnumerable<Commit> includeCommits,
    IEnumerable<Commit>? excludeCommits = null)
  {
    var query = new CommitFilter();
    var includes = includeCommits.ToList();
    if(includes.Count == 0)
    {
      return [];
    }
    query.IncludeReachableFrom = includes;
    if(excludeCommits != null)
    {
      query.ExcludeReachableFrom = excludeCommits.ToList();
    }
    return repo.Commits.QueryBy(query);
  }

  /// <summary>
  /// Return all commits in <paramref name="repo"/> (reachable from any of the refs in it).
  /// </summary>
  /// <param name="repo"></param>
  /// <returns></returns>
  public static IEnumerable<Commit> QueryCommits(this Repository repo)
  {
    return repo.QueryCommits(repo.Refs.FromGlob("refs/*"));
  }

  /// <summary>
  /// Return a new <see cref="CommitStubGraph"/> containing the specified
  /// <paramref name="commits"/>.
  /// </summary>
  /// <param name="commits"></param>
  /// <returns></returns>
  public static CommitStubGraph ToGraph(this IEnumerable<Commit> commits)
  {
    return new CommitStubGraph(commits);
  }
}
