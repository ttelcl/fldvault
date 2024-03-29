﻿/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FldVault.Core.Crypto;
using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;

namespace FldVault.Core.KeyResolution;

/// <summary>
/// Implements <see cref="IKeySeedService"/> to handle
/// references to the null key
/// </summary>
public class NullSeedService: IKeySeedService
{
  /// <summary>
  /// Create a new NullSeedService
  /// </summary>
  public NullSeedService()
  {
  }

  /// <inheritdoc/>
  public IKeySeed? TryCreateFromKeyInfoFile(string keyInfoFileName)
  {
    var kin = KeyInfoName.FromFile(keyInfoFileName);
    return (kin != null && kin.KeyId == NullKey.NullKeyId) ? new NullSeed() : null;
  }

  /// <inheritdoc/>
  public IKeySeed? TryCreateSeedForVault(VaultFile vaultFile)
  {
    return (vaultFile.KeyId == NullKey.NullKeyId) ? new NullSeed() : null;
  }

  internal class NullSeed: IKeySeed
  {
    public Guid KeyId { get => NullKey.NullKeyId; }

    public bool TryResolveKey(KeyChain keyChain)
    {
      if(keyChain.ContainsKey(KeyId))
      {
        // This normally should trigger, unless the null key was explicitly
        // removed from the key chain.
        return true;
      }
      using(var key = new NullKey())
      {
        keyChain.PutCopy(key);
        return true;
      }
    }

    /// <summary>
    /// Never writes and always returns false
    /// </summary>
    public bool WriteAsBlock(Stream stream)
    {
      return false;
    }

    /// <inheritdoc/>
    public IEnumerable<IKeySeed<T>> TryAdapt<T>()
    {
      return Enumerable.Empty<IKeySeed<T>>();
    }

  }
}
