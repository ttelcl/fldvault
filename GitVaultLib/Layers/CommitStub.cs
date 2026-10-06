using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LibGit2Sharp;

namespace GitVaultLib.Layers;

/// <summary>
/// A placeholder for a <see cref="Commit"/>, even when that commit object
/// is not yet known
/// </summary>
public class CommitStub
{
  private readonly HashSet<CommitStub> _children = new HashSet<CommitStub>();
  private readonly HashSet<CommitStub> _parents = new HashSet<CommitStub>();

  internal CommitStub(string sha)
  {
    Sha = sha;
  }

  /// <summary>
  /// The identifier for the commit
  /// </summary>
  public string Sha { get; }

  /// <summary>
  /// The actual <see cref="Commit"/>, if known.
  /// Set via <see cref="CommitStubGraph.Connect(Commit)"/>.
  /// </summary>
  public Commit? Target { get; internal set; }

  /// <summary>
  /// True if this stub is connected to a known commit. False if it is external or not yet resolved.
  /// </summary>
  public bool Connected => Target != null;

  /// <summary>
  /// The known parent commits.
  /// This list is maintained through <see cref="CommitStubGraph.Connect(Commit)"/>.
  /// </summary>
  public IReadOnlySet<CommitStub> Parents => _parents;

  /// <summary>
  /// The known child commits
  /// This list is maintained through <see cref="CommitStubGraph.Connect(Commit)"/>.
  /// </summary>
  public IReadOnlySet<CommitStub> Children => _children;

  internal void AddChild(CommitStub child)
  {
    _children.Add(child);
  }

  internal void AddParent(CommitStub parent)
  {
    _parents.Add(parent);
  }
}
