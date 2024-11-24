/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Crypto;
using FldVault.Core.KeyResolution;
using FldVault.Upi;

namespace FldVault.Upi.Implementation.Keys;

/// <summary>
/// Carries the state for a key
/// </summary>
public class KeyState: IKeyInfo
{
  private readonly object _lock = new();
  private readonly Dictionary<string, DateTimeOffset> _associatedFiles;

  /// <summary>
  /// Create a new KeyState
  /// </summary>
  internal KeyState(Guid keyId, KeyChain keyChain)
  {
    KeyChain = keyChain;
    KeyId = keyId;
    KeyTag = keyId.ToString()[..8];
    _lastRegistered = DateTimeOffset.UtcNow;
    LastStamp = _lastRegistered;
    LastStampSource = nameof(LastRegistered);
    _associatedFiles = new Dictionary<string, DateTimeOffset>();
  }

  /// <summary>
  /// The key chain that holds the actual key, if known.
  /// </summary>
  public KeyChain KeyChain { get; }

  /// <inheritdoc/>
  public Guid KeyId { get; }

  /// <summary>
  /// An abbreviated KeyId in string form
  /// </summary>
  public string KeyTag { get; }

  /// <inheritdoc/>
  public KeyStatus Status { 
    get { 
      if(KeyChain.ContainsKey(KeyId))
      {
        return HideKey ? KeyStatus.Hidden : KeyStatus.Published;
      }
      else
      {
        return Seed == null ? KeyStatus.Unknown : KeyStatus.Seeded;
      }
    }
  }

  /// <summary>
  /// Remove this key info's raw key if it exists
  /// </summary>
  /// <returns>
  /// True if the key existed and was removed, false if not.
  /// </returns>
  public bool UnloadKey()
  {
    return KeyChain.DeleteKey(KeyId);
  }

  /// <summary>
  /// True if the key is currently hidden (or should be hidden if it were known).
  /// </summary>
  public bool HideKey { get; set; }

  /// <inheritdoc/>
  public DateTimeOffset LastRegistered { 
    get => _lastRegistered;
    set {
      _lastRegistered = value;
      SetLastStamp(value);
    }
  }
  private DateTimeOffset _lastRegistered;

  /// <inheritdoc/>
  public DateTimeOffset? LastRequested {
    get => _lastRequested;
    private set {
      _lastRequested = value;
      SetLastStamp(value);
    }
  }
  private DateTimeOffset? _lastRequested;

  /// <inheritdoc/>
  public DateTimeOffset? LastServed {
    get => _lastServed;
    private set {
      _lastServed = value;
      SetLastStamp(value);
    } 
  }
  private DateTimeOffset? _lastServed;

  /// <inheritdoc/>
  public DateTimeOffset? LastAssociated { 
    get => _lastAssociated;
    private set {
      _lastAssociated = value;
      SetLastStamp(value);
    }
  }
  private DateTimeOffset? _lastAssociated;

  /// <summary>
  /// The most recent of <see cref="LastRegistered"/>, <see cref="LastRequested"/>,
  /// <see cref="LastServed"/> and <see cref="LastServed"/>
  /// </summary>
  public DateTimeOffset LastStamp { get; private set; }

  /// <summary>
  /// The source property for <see cref="LastStamp"/>
  /// </summary>
  public string LastStampSource { get; private set; }

  private void SetLastStamp(
    DateTimeOffset? value,
    [CallerMemberName] string propertyName = "INVALID")
  {
    if(value.HasValue)
    {
      Trace.TraceInformation($"LastStamp({KeyTag}/{propertyName}) <- {value.Value.ToLocalTime():o}");
      LastStamp = value.Value;
      LastStampSource = propertyName;
    }
  }

  /// <inheritdoc/>
  public IReadOnlyDictionary<string, DateTimeOffset> AssociatedFiles {
    get => _associatedFiles;
  }

  /// <summary>
  /// A seed object that can produce the key value. Resolving the seed
  /// likely requires user input (typically entering a passphrase)
  /// </summary>
  public IParameterKeySeed? Seed { get; private set; }

  /// <summary>
  /// True if a seed is available and that seed supports 
  /// passphrase based key resolution
  /// </summary>
  public bool SupportsPassphrase { get => Seed?.TryAdapt<SecureString>() != null; }

  /// <summary>
  /// Attach (or detach) a key seed to this key
  /// </summary>
  public void SetSeed(IParameterKeySeed? seed)
  {
    Seed = seed;
  }

  /// <summary>
  /// Try to associate a file with this key info
  /// </summary>
  /// <param name="fileName">
  /// The file name to associate. The file name must be absolute
  /// and the file must exist.
  /// </param>
  /// <param name="loadSeed">
  /// If true and there is no seed available, try to create a
  /// seed from the file if possible.
  /// </param>
  /// <returns>
  /// True if the file name was accepted
  /// </returns>
  public bool AssociateFile(string fileName, bool loadSeed)
  {
    lock(_lock)
    {
      if(!File.Exists(fileName))
      {
        return false;
      }
      if(!Path.IsPathFullyQualified(fileName))
      {
        return false;
      }
      var time = DateTimeOffset.Now;
      _associatedFiles[fileName] = time;
      LastAssociated = time;
      if(loadSeed && Seed == null)
      {
        SetSeed(PassphraseKeySeed2.TryFromFile(fileName));
      }
      return true;
    }
  }

  /// <summary>
  /// Try to resolve the key using the passphrase.
  /// </summary>
  /// <param name="passphrase">
  /// The passphrase to try
  /// </param>
  /// <returns>
  /// True if the key is resolved after this call (including the
  /// case where it already was resolved before)
  /// </returns>
  public bool TryResolveKey(SecureString passphrase)
  {
    lock(_lock)
    {
      if(KeyChain.ContainsKey(KeyId))
      {
        return true;
      }
      var seed = Seed?.TryAdapt<SecureString>();
      if(seed != null)
      {
        var result = seed.TryResolve(passphrase, KeyChain);
        return result;
      }
    }
    return false;
  }

  /// <summary>
  /// Call the <paramref name="keyUser"/> action with the raw key
  /// as argument if available, or with null if not available or
  /// withheld.
  /// This method avoids unnecessary key copying.
  /// This method updates the <see cref="LastRequested"/> and
  /// <see cref="LastServed"/> fields.
  /// </summary>
  /// <param name="keyUser">
  /// The action to be called with the key
  /// </param>
  public void UseKey(Action<IBytesWrapper?> keyUser)
  {
    IBytesWrapper? key = null;
    lock(_lock)
    {
      var now = DateTimeOffset.Now;
      LastRequested = now;
      if(!HideKey)
      {
        KeyChain.TryUseKey(KeyId, (_, bw) => { key = bw; });
        if(key != null)
        {
          LastServed = now;
        }
      }
      else
      {
        key = SimpleBytesWrapper.Empty;
      }
    }
    keyUser(key);
  }
}
