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
using FldVault.Core.KeyResolution;
using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;

namespace FldVault.KeyServer;

/// <summary>
/// Implements IKeySeedService to find keys from the key server
/// </summary>
public class KeyServerSeedService: IKeySeedService
{
  /// <summary>
  /// Create a new KeyServerSeedService
  /// </summary>
  public KeyServerSeedService(KeyServerService kss)
  {
    Enabled = true;
    ServerService = kss;
  }

  /// <summary>
  /// Whether this seed service is enabled or not.
  /// When disabled, all seeds fail to hatch
  /// </summary>
  public bool Enabled { get; set; }

  /// <summary>
  /// The key server interface
  /// </summary>
  public KeyServerService ServerService { get; init; }

  /// <inheritdoc/>
  public IKeySeed? TryCreateFromKeyInfoFile(string keyInfoFileName)
  {
    keyInfoFileName = Path.GetFullPath(keyInfoFileName);
    if(!File.Exists(keyInfoFileName))
    {
      return null;
    }
    var kin = KeyInfoName.TryFromFile(keyInfoFileName);
    return kin == null ? null : new Seed(this, kin.KeyId);
  }

  /// <inheritdoc/>
  public IKeySeed? TryCreateSeedForVault(VaultFile vaultFile)
  {
    return new Seed(this, vaultFile.KeyId);
  }

  /// <summary>
  /// Implements <see cref="IKeySeed"/> for <see cref="KeyServerSeedService"/>
  /// </summary>
  public class Seed: IKeySeed
  {
    private readonly KeyServerSeedService _owner;

    internal Seed(KeyServerSeedService owner, Guid keyId)
    {
      _owner=owner;
      KeyId=keyId;
    }

    /// <inheritdoc/>
    public Guid KeyId { get; init; }

    /// <inheritdoc/>
    public IEnumerable<IKeySeed<T>> TryAdapt<T>()
    {
      return Enumerable.Empty<IKeySeed<T>>();
    }

    /// <inheritdoc/>
    public bool TryResolveKey(KeyChain keyChain)
    {
      if(!_owner.Enabled || !_owner.ServerService.ServerAvailable)
      {
        return false;
      }
      //var task = _owner.ServerService.LookupKeyAsync()
      throw new NotImplementedException("NYI - how to get the ct here???");
    }

    /// <inheritdoc/>
    public bool WriteAsBlock(Stream stream)
    {
      return false;
    }
  }
}
