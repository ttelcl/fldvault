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
using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;

namespace FldVault.Core.KeyResolution;

/// <summary>
/// An <see cref="IKeySeedService"/> that looks up keys
/// in an <see cref="IKeyCacheStore"/>
/// </summary>
public class KeyStoreSeedService: IKeySeedService
{
  private readonly IKeyCacheStore _keyCacheStore;

  /// <summary>
  /// Create a new KeyStoreSeedService
  /// </summary>
  public KeyStoreSeedService(IKeyCacheStore store)
  {
    _keyCacheStore = store;
  }

  /// <inheritdoc/>
  public IKeySeed? TryCreateFromKeyInfoFile(string keyInfoFileName)
  {
    keyInfoFileName = Path.GetFullPath(keyInfoFileName);
    if(!File.Exists(keyInfoFileName))
    {
      return null;
    }
    var kin = KeyInfoName.TryFromFile(keyInfoFileName);
    return kin == null ? null : new StoreKeySeed(kin.KeyId, _keyCacheStore);
  }

  /// <inheritdoc/>
  public IKeySeed? TryCreateSeedForVault(VaultFile vaultFile)
  {
    return new StoreKeySeed(vaultFile.KeyId, _keyCacheStore);
  }

  internal class StoreKeySeed: IKeySeed
  {
    public StoreKeySeed(Guid keyId, IKeyCacheStore store)
    {
      KeyId = keyId;
      KeyCacheStore = store;
    }

    public Guid KeyId { get; init; }

    public IKeyCacheStore KeyCacheStore { get; init; }

    public bool TryResolveKey(KeyChain keyChain)
    {
      var existingKey = keyChain.FindCopy(KeyId);
      if(existingKey != null)
      {
        return true;
      }
      using(var key = KeyCacheStore.LoadKey(KeyId))
      {
        if(key!=null)
        {
          keyChain.PutCopy(key);
          return true;
        }
        else
        {
          return false;
        }
      }
    }

  }

}
