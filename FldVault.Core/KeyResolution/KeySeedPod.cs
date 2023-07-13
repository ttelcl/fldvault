/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Crypto;

namespace FldVault.Core.KeyResolution;

/// <summary>
/// An <see cref="IKeySeed"/> implementation that tries a series
/// of contained key seeds until one succeeds
/// </summary>
public class KeySeedPod: IKeySeed
{
  private readonly List<IKeySeed> _seeds;

  /// <summary>
  /// Create a new KeySeedPod
  /// </summary>
  public KeySeedPod(Guid keyId)
  {
    _seeds = new List<IKeySeed>();
    KeyId = keyId;
    Seeds = _seeds.AsReadOnly();
  }

  /// <inheritdoc/>
  public Guid KeyId { get; init; }

  /// <summary>
  /// A read-only view on the child seeds
  /// </summary>
  public IReadOnlyList<IKeySeed> Seeds { get; init; }

  /// <summary>
  /// Add a matching seed to this seed pod
  /// </summary>
  /// <param name="seed">
  /// An <see cref="IKeySeed"/> instance with the same key id
  /// as this pod.
  /// </param>
  public void AddSeed(IKeySeed seed)
  {
    if(seed.KeyId != KeyId)
    {
      throw new ArgumentOutOfRangeException(
        nameof(seed), "Key id mismatch");
    }
    _seeds.Add(seed);
  }

  /// <summary>
  /// Implements <see cref="IKeySeed.TryResolveKey(KeyChain)"/> by trying
  /// the embedded key seeds one by one, after first checking if the seed
  /// is already in the chain
  /// </summary>
  public bool TryResolveKey(KeyChain keyChain)
  {
    var existingKey = keyChain.FindCopy(KeyId);
    if(existingKey != null)
    {
      return true;
    }
    return _seeds.Any(seed => seed.TryResolveKey(keyChain));
  }

  /// <summary>
  /// Try writing each of the child seeds, returning true as soon as any
  /// of them succeeded in writing
  /// </summary>
  public bool WriteAsBlock(Stream stream)
  {
    foreach(var seed in _seeds)
    {
      if(seed.WriteAsBlock(stream))
      {
        return true;
      }
    }
    return false;
  }
}