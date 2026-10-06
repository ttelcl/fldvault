using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GitVaultLib.Layers;

using LibGit2Sharp;

namespace GitVaultLib.Layers;

/// <summary>
/// A database of <see cref="GitRefInfo"/> instances
/// </summary>
public class GitRefsDb
{
  private Dictionary<string, GitRefInfo> _infosByRefName;
  private Dictionary<string, GitRefInfo<Commit>> _commitsByRefName;
  private Dictionary<string, IReadOnlyList<GitRefInfo<Commit>>> _refsByCommit;

  /// <summary>
  /// Create a new empty <see cref="GitRefsDb"/>
  /// </summary>
  public GitRefsDb()
  {
    _infosByRefName = new Dictionary<string, GitRefInfo>();
    _commitsByRefName = new Dictionary<string, GitRefInfo<Commit>>();
    _refsByCommit = new Dictionary<string, IReadOnlyList<GitRefInfo<Commit>>>();
  }

  /// <summary>
  /// Create a new <see cref="GitRefsDb"/> and add all references in <paramref name="repo"/>
  /// to it.
  /// </summary>
  /// <param name="repo"></param>
  /// <returns></returns>
  public static GitRefsDb ForRepository(Repository repo)
  {
    return (new GitRefsDb()).WithRefs(repo.Refs);
  }

  /// <summary>
  /// Return this <see cref="GitRefsDb"/> itself after adding all <paramref name="references"/>.
  /// </summary>
  /// <param name="references"></param>
  /// <returns></returns>
  public GitRefsDb WithRefs(IEnumerable<Reference> references)
  {
    AddRange(references);
    return this;
  }

  /// <summary>
  /// Get a mapping from all stored refs to their <see cref="GitRefInfo"/>.
  /// This includes refs targeting any types.
  /// </summary>
  public IReadOnlyDictionary<string, GitRefInfo> RefInfoByRefName => _infosByRefName;

  /// <summary>
  /// Get a mapping from stored refs that target a <see cref="Commit"/> to their
  /// typed <see cref="GitRefInfo"/> subclass.
  /// </summary>
  public IReadOnlyDictionary<string, GitRefInfo<Commit>> CommitRefInfosByRefName => _commitsByRefName;

  /// <summary>
  /// Get a mapping from known commit identifiers to a set of reference descriptors that map back to them
  /// </summary>
  public IReadOnlyDictionary<string, IReadOnlyList<GitRefInfo<Commit>>> ReferenceNamesByCommit => _refsByCommit;

  /// <summary>
  /// Return the list of all descriptors of refs in this <see cref="GitRefsDb"/>
  /// (regardless of their target type)
  /// </summary>
  public IReadOnlyCollection<GitRefInfo> AllRefs => _infosByRefName.Values;

  /// <summary>
  /// Return the list of all descriptors of refs that target <see cref="Commit"/> in this
  /// <see cref="GitRefsDb"/>.
  /// </summary>
  public IReadOnlyCollection<GitRefInfo<Commit>> CommitRefs => _commitsByRefName.Values;

  /// <summary>
  /// Get the ids of all commits that are targeted by at least one reference.
  /// </summary>
  public IReadOnlyCollection<string> ReferencedCommitTags => _refsByCommit.Keys;

  /// <summary>
  /// Enumerate all <see cref="Commit"/>s that are directly referenced by one or more references
  /// in this collection
  /// </summary>
  public IEnumerable<Commit> ReferencedCommits =>
    _refsByCommit.Values
    // precondition: all refinfos in the list share the same commit and the list is not empty
    .Select(list => list[0].Target); 

  /// <summary>
  /// Return the <see cref="GitRefInfo"/> for the commit-targetting reference with the given
  /// <paramref name="referenceName"/>, returning <see langword="null"/> if not found.
  /// </summary>
  /// <param name="referenceName"></param>
  /// <returns></returns>
  public GitRefInfo<Commit>? FindCommitForReferenceName(string referenceName)
  {
    return _commitsByRefName.TryGetValue(referenceName, out var commit) ? commit : null;
  }

  /// <summary>
  /// Try to get the set of reference descriptors that share the commit with the given
  /// <see cref="GitObject.Sha"/>.
  /// </summary>
  /// <param name="commitSha"></param>
  /// <param name="refs"></param>
  /// <returns></returns>
  public bool TryGetRefNamesForCommit(
    string commitSha, [MaybeNullWhen(false)]out IReadOnlyList<GitRefInfo<Commit>> refs)
  {
    return _refsByCommit.TryGetValue(commitSha, out refs);
  }

  /// <summary>
  /// Returns true if there are any references for the commit with the given
  /// <see cref="GitObject.Sha"/>.
  /// </summary>
  /// <param name="commitSha"></param>
  /// <returns></returns>
  public bool HasReferences(string commitSha)
  {
    return TryGetRefNamesForCommit(commitSha, out var refNames) && refNames.Count > 0;
  }

  /// <summary>
  /// Add a single reference to this <see cref="GitRefsDb"/>.
  /// </summary>
  /// <param name="reference"></param>
  /// <returns></returns>
  public GitRefInfo Add(Reference reference)
  {
    if(_infosByRefName.TryGetValue(reference.CanonicalName, out var existing))
    {
      // Don't insert the same ref again
      return existing;
    }
    var refInfo = GitRefInfo.Create(reference);
    _infosByRefName[refInfo.CanonicalName] = refInfo;
    if(refInfo is GitRefInfo<Commit> commitRef)
    {
      _commitsByRefName[commitRef.CanonicalName] = commitRef;
      var sha = commitRef.Sha;
      List<GitRefInfo<Commit>> refList;
      if(!_refsByCommit.TryGetValue(sha, out var infoList))
      {
        refList = new List<GitRefInfo<Commit>>();
        _refsByCommit[sha] = refList;
      }
      else
      {
        refList = (List<GitRefInfo<Commit>>)infoList;
      }
      refList.Add(commitRef);
    }
    return refInfo;
  }

  /// <summary>
  /// Add multiple references at once
  /// </summary>
  /// <param name="references"></param>
  public void AddRange(IEnumerable<Reference> references)
  {
    foreach(var reference in references)
    {
      Add(reference);
    }
  }

}
