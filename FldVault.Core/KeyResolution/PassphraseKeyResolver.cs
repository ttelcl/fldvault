/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FldVault.Core.Crypto;
using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;

namespace FldVault.Core.KeyResolution;

/// <summary>
/// Helper service to create passphrase based key seeds and assist in resolving them
/// </summary>
public class PassphraseKeyResolver: IKeyKindSeedService
{
  private readonly Func<Guid, SecureString?>? _passphraseUserInterface;

  /// <summary>
  /// Create a new PassphraseKeyResolver
  /// </summary>
  /// <param name="passphraseUserInterface">
  /// The user interface callback to have the user enter a passphrase.
  /// If this is null, attempts to resolve a seed needing it will fail.
  /// </param>
  public PassphraseKeyResolver(
      Func<Guid, SecureString?>? passphraseUserInterface)
  {
    _passphraseUserInterface = passphraseUserInterface;
  }

  /// <summary>
  /// Create a key seed for the specified <see cref="PassphraseKeyInfoFile"/>
  /// instance
  /// </summary>
  /// <param name="pkif">
  /// The key seed information to wrap into the new seed.
  /// </param>
  /// <returns>
  /// An IKeySeed instance that can resolve the key, possibly triggering
  /// the user interface callback to have the user enter the passphrase.
  /// </returns>
  public IKeySeed CreateSeed(PassphraseKeyInfoFile pkif)
  {
    return new PassphraseKeySeed(pkif, this);
  }

  /// <summary>
  /// Try to create a passphrase key seed for the given vault file.
  /// Returns null if no passphrase linking info is present in the vault
  /// </summary>
  /// <param name="vaultFile">
  /// The vault file to try to create a key seed for.
  /// </param>
  /// <returns>
  /// The created passphrase based key seed on success, or null on failure
  /// </returns>
  public IKeySeed? TryCreateSeedForVault(VaultFile vaultFile)
  {
    var pkif = vaultFile.GetPassphraseInfo();
    if(pkif != null)
    {
      return CreateSeed(pkif);
    }
    // For now, looking for external *.pass.key-info files is NYI
    return null;
  }

  /// <summary>
  /// Try to create a passphrase key seed for the given *.key-info
  /// file.
  /// </summary>
  /// <param name="keyInfoFileName">
  /// The name of the *.key-info file to load
  /// </param>
  /// <returns></returns>
  public IKeySeed? TryCreateFromKeyInfoFile(string keyInfoFileName)
  {
    keyInfoFileName = Path.GetFullPath(keyInfoFileName);
    if(!File.Exists(keyInfoFileName))
    {
      return null;
    }
    var kin = KeyInfoName.TryFromFile(keyInfoFileName);
    if(kin != null && kin.Kind == KeyKind.Passphrase)
    {
      var folder = Path.GetDirectoryName(keyInfoFileName) ?? Environment.CurrentDirectory;
      var pkif = PassphraseKeyInfoFile.TryRead(kin, folder);
      if(pkif != null)
      {
        return CreateSeed(pkif);
      }
    }
    return null;
  }

  /// <summary>
  /// The key kind handled by this resolver
  /// </summary>
  public string Kind { get => KeyKind.Passphrase; }

  /// <summary>
  /// Implements <see cref="IKeySeed"/> for passphrase based keys
  /// </summary>
  internal class PassphraseKeySeed: IKeySeed, IKeySeed<PassphraseKeyInfoFile>
  {
    private readonly PassphraseKeyInfoFile _pkif;
    private readonly PassphraseKeyResolver _resolver;

    internal PassphraseKeySeed(
      PassphraseKeyInfoFile pkif,
      PassphraseKeyResolver resolver)
    {
      _pkif = pkif;
      _resolver = resolver;
    }

    /// <inheritdoc/>
    public Guid KeyId { get => _pkif.KeyId; }

    /// <summary>
    /// Implements IKeySeed{PassphraseKeyInfoFile}
    /// </summary>
    public PassphraseKeyInfoFile KeyDetail { get => _pkif; }

    /// <inheritdoc/>
    public bool TryResolveKey(KeyChain keyChain)
    {
      if(keyChain.ContainsKey(KeyId))
      {
        return true;
      }
      using(var passphrase = _resolver._passphraseUserInterface?.Invoke(KeyId))
      {
        if(passphrase != null)
        {
          using(var key = PassphraseKey.TryPassphrase(passphrase, _pkif))
          {
            if(key != null)
            {
              keyChain.PutCopy(key);
              return true;
            }
          }
        }
      }
      return false;
    }

    /// <summary>
    /// Writes the embedded information as a PASS block
    /// </summary>
    public bool WriteAsBlock(Stream stream)
    {
      _pkif.WriteBlock(stream);
      return true;
    }


    /// <summary>
    /// Returns a singleton containing this seed itself if T is <see cref="PassphraseKeyInfoFile"/>,
    /// an empty collection otherwise
    /// </summary>
    public IEnumerable<IKeySeed<T>> TryAdapt<T>()
    {
      return (this is IKeySeed<T> cast) ? new[] {cast} : Enumerable.Empty<IKeySeed<T>>();
    }

  }
}
