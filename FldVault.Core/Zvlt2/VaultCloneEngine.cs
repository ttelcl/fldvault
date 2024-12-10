/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.BlockFiles;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Utility class for cloning vault blocks between two compatible vaults.
/// </summary>
public class VaultCloneEngine: IDisposable
{
  private Stream? _sourceStream;
  private Stream? _targetStream;

  /// <summary>
  /// Create a new VaultCloneEngine
  /// </summary>
  public VaultCloneEngine(
    VaultFile sourceVault,
    VaultFile targetVault)
  {
    if(!AreVaultsCompatible(sourceVault, targetVault))
    {
      throw new ArgumentException("Vaults are not compatible");
    }
    SourceVault = sourceVault;
    TargetVault = targetVault;
    _sourceStream = File.OpenRead(sourceVault.FileName);
  }

  /// <summary>
  /// Create a VaultCloneEngine for cloning content from 
  /// <paramref name="sourceVault"/> to a vault named
  /// <paramref name="targetVaultName"/>. If the target vault
  /// already exists, it will be opened. Otherwise, a new
  /// empty vault will be created.
  /// </summary>
  /// <param name="sourceVault"></param>
  /// <param name="targetVaultName"></param>
  /// <returns></returns>
  public static VaultCloneEngine Create(
    VaultFile sourceVault,
    string targetVaultName)
  {
    var targetVault = 
      File.Exists(targetVaultName)
      ? VaultFile.Open(targetVaultName)
      : VaultFile.CreateEmptyClone(targetVaultName, sourceVault);
    return new VaultCloneEngine(sourceVault, targetVault);
  }

  /// <summary>
  /// Try to create a VaultCloneEngine for cloning content from
  /// <paramref name="sourceVault"/> to a vault named
  /// <paramref name="targetVaultName"/>. If the target vault
  /// does not exist, a new compatible vault will be created.
  /// If the target vault exists but is not compatible with the
  /// source, null will be returned.
  /// </summary>
  /// <param name="sourceVault"></param>
  /// <param name="targetVaultName"></param>
  /// <returns></returns>
  public static VaultCloneEngine? TryCreate(
    VaultFile sourceVault,
    string targetVaultName)
  {
    if(File.Exists(targetVaultName))
    {
      var targetVault = VaultFile.Open(targetVaultName);
      if(!AreVaultsCompatible(sourceVault, targetVault))
      {
        return null;
      }
      else
      {
        return new VaultCloneEngine(sourceVault, targetVault);
      }
    }
    else
    {
      var targetVault = VaultFile.CreateEmptyClone(targetVaultName, sourceVault);
      return new VaultCloneEngine(sourceVault, targetVault);
    }
  }

  /// <summary>
  /// The source vault that is being cloned from.
  /// </summary>
  public VaultFile SourceVault { get; }

  /// <summary>
  /// The target vault that is being cloned to.
  /// </summary>
  public VaultFile TargetVault { get; }

  /// <summary>
  /// Test if two vault files are compatible for cloning.
  /// To be compatible, the vaults must have the same key ID
  /// and the same creation timestamp.
  /// </summary>
  /// <param name="sourceVault"></param>
  /// <param name="targetVault"></param>
  /// <returns>
  /// True if the vaults are compatible for cloning.
  /// </returns>
  public static bool AreVaultsCompatible(
    VaultFile sourceVault,
    VaultFile targetVault)
  {
    return sourceVault.KeyId == targetVault.KeyId
      && sourceVault.Header.TimeStamp == targetVault.Header.TimeStamp;
  }

  /// <summary>
  /// Clone a block element (such as an entire file) from the source
  /// </summary>
  /// <param name="sourceElement"></param>
  /// <returns></returns>
  public BlockElement CloneElement(IBlockElement sourceElement)
  {
    EnsureNotDisposed();
    var rootBlock = CloneBlock(sourceElement.Block);
    var rootElement = new BlockElement(rootBlock);
    foreach(var child in sourceElement.Children)
    {
      // Recursive!
      // But the expectation is that the children don't have children themselves.
      var childElement = CloneElement(child);
      rootElement.AddChild(childElement);
    }
    return rootElement;
  }

  public BlockInfo CloneBlock(IBlockInfo sourceBlock)
  {
    if(sourceBlock.Offset <= 0L)
    {
      throw new ArgumentException("The Block is missing an offset");
    }
    if(!SourceVault.Blocks.ContainsBlock(sourceBlock))
    {
      throw new ArgumentException("The Block is not in the source vault");
    }
    OpenTargetStream(); // includes EnsureNotDisposed()

    throw new NotImplementedException();
  }

  /// <summary>
  /// Close the source and target streams if they are still open.
  /// </summary>
  public void Dispose()
  {
    if(_sourceStream != null)
    {
      _sourceStream.Dispose();
      _sourceStream = null;
    }
    if(_targetStream != null)
    {
      _targetStream.Dispose();
      _targetStream = null;
    }
  }

  private void EnsureNotDisposed()
  {
    ObjectDisposedException.ThrowIf(_sourceStream == null, this);
  }

  /// <summary>
  /// Open the target stream and seek to the end, preparing
  /// to append data.
  /// </summary>
  private void OpenTargetStream()
  {
    EnsureNotDisposed();
    if(_targetStream == null)
    {
      _targetStream = File.OpenWrite(TargetVault.FileName);
      _targetStream.Position = _targetStream.Length;
    }
  }
}
