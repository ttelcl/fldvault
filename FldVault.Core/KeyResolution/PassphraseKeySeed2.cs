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
using System.Threading.Tasks;

using FldVault.Core.Crypto;
using FldVault.Core.Vaults;

namespace FldVault.Core.KeyResolution;

/// <summary>
/// Implements <see cref="IParameterKeySeed{TParam}"/> for
/// a passphrase based seed
/// </summary>
public class PassphraseKeySeed2: IParameterKeySeed<SecureString>, IKeySeed, IKeySeed<PassphraseKeyInfoFile>
{
  /// <summary>
  /// Create a new PassphraseKeySeed2
  /// </summary>
  public PassphraseKeySeed2(
    PassphraseKeyInfoFile pkif)
  {
    KeyInfo = pkif;
  }

  /// <summary>
  /// Try to locate passphrase key information from a file and build
  /// a key seed from it. Returns null if not found.
  /// </summary>
  public static PassphraseKeySeed2? TryFromFile(string fileName)
  {
    var pkif = PassphraseKeyInfoFile.TryFromFile(fileName);
    return pkif == null ? null : new PassphraseKeySeed2(pkif);
  }

  /// <inheritdoc/>
  public Guid KeyId { get => KeyInfo.KeyId; }

  /// <summary>
  /// The information that allows converting a passphrase to
  /// a raw key.
  /// </summary>
  public PassphraseKeyInfoFile KeyInfo { get; }

  /// <inheritdoc/>
  public IParameterKeySeed<TParam>? TryAdapt<TParam>()
  {
    return (this is IParameterKeySeed<TParam> cast) ? cast : null;
  }

  /// <inheritdoc/>
  public bool TryResolve(SecureString param, KeyChain chain)
  {
    using(var pk = PassphraseKey.TryPassphrase(param, KeyInfo))
    {
      if(pk != null)
      {
        chain.PutCopy(pk);
        return true;
      }
      else
      {
        return false;
      }
    }
  }

  /// <inheritdoc/>
  public PassphraseKeyInfoFile KeyDetail { get => KeyInfo; }

  bool IKeySeed.TryResolveKey(KeyChain keyChain)
  {
    // This seed requires a passphrase to be passed in, which this
    // method does not support.
    return false;
  }

  /// <inheritdoc/>
  public bool WriteAsBlock(Stream stream)
  {
    KeyInfo.WriteBlock(stream);
    return true;
  }

  IEnumerable<IKeySeed<T>> IKeySeed.TryAdapt<T>()
  {
    return (this is IKeySeed<T> cast) ? new[] { cast } : Enumerable.Empty<IKeySeed<T>>();
  }
}
