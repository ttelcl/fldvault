using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LibGit2Sharp;

namespace GitVaultLib.Layers;

/// <summary>
/// Information about a <see cref="Reference"/> in a git repository.
/// Use <see cref="Create(Reference)"/> to create an instance of
/// the appropriate generic subclass <see cref="GitRefInfo{TargetType}"/>
/// (usually GitRefInfo{Commit})
/// </summary>
public abstract class GitRefInfo
{

  /// <summary>
  /// Create the base part of the <see cref="GitRefInfo"/> subclass.
  /// </summary>
  /// <param name="reference"></param>
  /// <param name="targetRef"></param>
  /// <param name="annotations"></param>
  /// <param name="target"></param>
  protected GitRefInfo(
    Reference reference,
    DirectReference targetRef,
    IReadOnlyList<TagAnnotation> annotations,
    GitObject target)
  {
    Ref = reference;
    Annotations = annotations;
    TargetRef = targetRef;
    TargetObject = target;
  }
  
  /// <summary>
  /// Create a <see cref="GitRefInfo{TargetType}"/> instance appropriate for
  /// the ultimate target of the <paramref name="reference"/>.
  /// </summary>
  /// <param name="reference"></param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static GitRefInfo Create(Reference reference)
  {
    var annotations = new List<TagAnnotation>();
    var targetRef = reference.ResolveToDirectReference();
    var target = targetRef.Target;
    // dig through all annotations
    while(target is TagAnnotation annotation)
    {
      if(annotations.Contains(annotation))
      {
        throw new InvalidOperationException(
          $"Recursive chain of tag annotations detected, reached from {reference.CanonicalName}");
      }
      annotations.Add(annotation);
      target = annotation.Target;
    }
    // Create a generic specialization if the target is recognized, whether common
    // (Commit) or something more obscure. Note that TagAnnotation is missing, since
    // those were already unpacked and dereferenced
    if(target is Commit commit)
    {
      // The most common case - by far
      return new GitRefInfo<Commit>(reference, targetRef, annotations, commit);
    }
    else if(target is Blob blob)
    {
      // Very uncommon. There is at least one in git's own repository
      return new GitRefInfo<Blob>(reference, targetRef, annotations, blob);
    }
    else if(target is Tree tree)
    {
      // Never seen it, and it doesn't feel it makes sense. But it could happen.
      return new GitRefInfo<Tree>(reference, targetRef, annotations, tree);
    }
    else if(target is GitLink link)
    {
      // Never seen it so far. But could make sense.
      return new GitRefInfo<GitLink>(reference, targetRef, annotations, link);
    }
    else
    {
      // Unknown or future object type: create an unspecialized GitRefInfo<GitObject>
      return new GitRefInfo<GitObject>(reference, targetRef, annotations, target);
    }
  }

  /// <summary>
  /// The reference itself
  /// </summary>
  public Reference Ref { get; }

  /// <summary>
  /// The ultimate target object (the target of <see cref="TargetRef"/>, or the target
  /// at the end of a chain of tag annotations)
  /// </summary>
  public GitObject TargetObject { get; }

  /// <summary>
  /// Get the full name of <see cref="Ref"/>.
  /// </summary>
  public string CanonicalName => Ref.CanonicalName;

  /// <summary>
  /// The identifier of the ultimate target <see cref="TargetObject"/>.
  /// </summary>
  public string Sha => TargetObject.Sha;

  /// <summary>
  /// The annotation chain for the reference. Typically empty, and if not empty
  /// there is typically one element. If there are multiple elements in this, then
  /// there were tag annotations targeting other tag annotations.
  /// </summary>
  public IReadOnlyList<TagAnnotation> Annotations { get; }

  /// <summary>
  /// The end of the chain of symbolic refs (usually the same as <see cref="Ref"/>)
  /// </summary>
  public DirectReference TargetRef { get; }
}

/// <summary>
/// A subclass of <see cref="GitRefInfo"/> that adds a stronger typed target field
/// </summary>
/// <typeparam name="TargetType"></typeparam>
public class GitRefInfo<TargetType>: GitRefInfo
  where TargetType : GitObject
{
  internal GitRefInfo(
    Reference reference,
    DirectReference targetRef,
    IReadOnlyList<TagAnnotation> annotations,
    TargetType target)
    : base(reference, targetRef, annotations, target)
  {
    Target = target;
  }

  /// <summary>
  /// The strongly typed ultimate target of the reference
  /// </summary>
  public TargetType Target { get; }
}
