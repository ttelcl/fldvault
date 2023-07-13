/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
      var existingKey = keyChain.FindCopy(KeyId);
      if(existingKey != null)
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
  }
}
