/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;

namespace FldVault.Core.KeyResolution;

/// <summary>
/// Top level implementation for <see cref="IKeySeedService"/>, delegating
/// requests to other instances
/// </summary>
public class KeySeedService: IKeySeedService
{
  private readonly List<IKeySeedService> _seedServices;

  /// <summary>
  /// Create a new KeySeedService without any child services installed.
  /// Normally you should add the null key service, the unlock store service
  /// and a passphrase key service.
  /// </summary>
  private KeySeedService()
  {
    _seedServices = new List<IKeySeedService>();
  }

  /// <summary>
  /// Create a new KeySeedService without any child services installed.
  /// Normally you should add the null key service, the unlock store service
  /// and a passphrase key service.
  /// </summary>
  public static KeySeedService NewEmptyKeyService()
  {
    return new KeySeedService();
  }

  /// <summary>
  /// Appends the key seed service at the end of the list of child
  /// seed services. For most services more specific versions
  /// of this method exist.
  /// </summary>
  /// <param name="keySeedService">
  /// The service to add
  /// </param>
  /// <returns>
  /// This same service, enabling fluent calls
  /// </returns>
  public KeySeedService AddSeedService(IKeySeedService keySeedService)
  {
    _seedServices.Add(keySeedService);
    return this;
  }

  /// <summary>
  /// Add the service that shortcuts handling of the null key
  /// (ID "ad7a6866-62f8-47bd-ac8f-c18b8e9f8e20")
  /// </summary>
  /// <returns></returns>
  public KeySeedService AddNullKeyService()
  {
    return AddSeedService(new NullSeedService());
  }

  /// <summary>
  /// Add a generic key cache store service instance. To add the unlock service,
  /// use <see cref="AddUnlockStoreService"/> instead
  /// </summary>
  public KeySeedService AddStoreService(IKeyCacheStore keyCacheStore)
  {
    return AddSeedService(new KeyStoreSeedService(keyCacheStore));
  }

  /// <summary>
  /// Add the unlock store service
  /// </summary>
  public KeySeedService AddUnlockStoreService()
  {
    return AddStoreService(UnlockStore.Default);
  }

  /// <summary>
  /// Add a passphrase key service, using the specified user interface
  /// callback to ask the user for passphrases.
  /// </summary>
  /// <param name="passphraseUserInterface">
  /// The user interface callback to ask users for a passphrase.
  /// If this is null, users are not asked for passphrases
  /// </param>
  public KeySeedService AddPassphraseKeyService(
    Func<Guid, SecureString?>? passphraseUserInterface)
  {
    return AddSeedService(new PassphraseKeyResolver(passphraseUserInterface));
  }

  /// <inheritdoc/>
  public IKeySeed? TryCreateFromKeyInfoFile(
    string keyInfoFileName)
  {
    var kin = KeyInfoName.TryFromFile(keyInfoFileName);
    if(kin == null)
    {
      return null;
    }
    var pod = new KeySeedPod(kin.KeyId);
    foreach(var keyService in _seedServices)
    {
      var seed = keyService.TryCreateFromKeyInfoFile(keyInfoFileName);
      if(seed != null)
      {
        pod.AddSeed(seed);
      }
    }
    return pod.Seeds.Count > 0 ? pod : null;
  }

  /// <inheritdoc/>
  public IKeySeed? TryCreateSeedForVault(
    VaultFile vaultFile)
  {
    var pod = new KeySeedPod(vaultFile.KeyId);
    foreach(var keyService in _seedServices)
    {
      var seed = keyService.TryCreateSeedForVault(vaultFile);
      if(seed != null)
      {
        pod.AddSeed(seed);
      }
    }
    return pod.Seeds.Count > 0 ? pod : null;
  }
}
