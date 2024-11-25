/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Vaults;

namespace FldVault.Core.Crypto;

/// <summary>
/// Storage and lifetime management for KeyBuffers. Maps Key IDs to KeyBuffer instances
/// </summary>
public class KeyChain: IDisposable
{
  private readonly object _lock = new object();
  private readonly Dictionary<Guid, KeyBuffer> _store;
  private static readonly List<KeyBuffer> __wellKnownKeys = new List<KeyBuffer> {
    new NullKey(),
  };

  /// <summary>
  /// Create a new KeyChain, and initialize it with the well-known keys
  /// </summary>
  public KeyChain()
  {
    _store = new Dictionary<Guid, KeyBuffer>();
    foreach(var key in __wellKnownKeys)
    {
      PutCopy(key);
    }
  }

  /// <summary>
  /// Return the number of keys in the chain
  /// </summary>
  public int KeyCount {
    get {
      lock(_lock)
      {
        return _store.Count;
      }
    }
  }

  /// <summary>
  /// Enumerate fingerprints for the keys in the key chain.
  /// Each "fingerprint" is the first 13 characters of the key ID:
  /// enough to verify a key is present, but not enough to reconstruct
  /// the key id. [Thread safe]
  /// </summary>
  public IEnumerable<string> EnumerateFingerprints()
  {
    List<Guid> keys;
    lock(_lock)
    {
      keys = _store.Keys.ToList();
    }
    return keys.Select(key => key.ToString().Substring(0, 13));
  }

  /// <summary>
  /// Returns true if <paramref name="key"/> is present in this store.
  /// [Thread safe]
  /// </summary>
  public bool ContainsKey(Guid key)
  {
    lock(_lock)
    {
      return _store.ContainsKey(key);
    }
  }

  /// <summary>
  /// Look up the key identified by the key and return a copy if found.
  /// DO dispose the returned value after use (if any).
  /// [Thread safe]
  /// </summary>
  public KeyBuffer? FindCopy(Guid guid)
  {
    lock(_lock)
    {
      if(_store.TryGetValue(guid, out var keyBuffer))
      {
        return new KeyBuffer(keyBuffer.Bytes);
      }
    }
    return null;
  }

  /// <summary>
  /// Put a copy of the given key in this chain, if it isn't there already.
  /// The stored copy is always a plain <see cref="KeyBuffer"/>, not the
  /// subclass implementing <paramref name="key"/>.
  /// [Thread safe]
  /// </summary>
  /// <param name="key">
  /// The key to copy
  /// </param>
  public KeyBuffer PutCopy(KeyBuffer key)
  {
    lock(_lock)
    {
      var keyId = key.GetId();
      if(!_store.ContainsKey(keyId))
      {
        var copy = new KeyBuffer(key.Bytes);
        _store.Add(copy.GetId(), copy);
      }
      return _store[keyId];
    }
  }

  /// <summary>
  /// Put a copy of the key in <paramref name="keyBytes"/> in this chain
  /// if it isn't there already.
  /// </summary>
  /// <param name="keyBytes">
  /// A <see cref="CryptoBuffer{T}"/> containing the 32 bytes of the key.
  /// </param>
  /// <returns>
  /// True if the key was added, false if it was already present.
  /// </returns>
  /// <exception cref="ArgumentOutOfRangeException"></exception>
  public KeyBuffer PutCopy(CryptoBuffer<byte> keyBytes)
  {
    if(keyBytes.Length != 32)
    {
      throw new ArgumentOutOfRangeException(
        nameof(keyBytes),
        "Expecting a 32 byte (256 bit) buffer as argument");
    }
    lock(_lock)
    {
      var keyId = HashResult.FromSha256(keyBytes).AsGuid;
      if(!_store.ContainsKey(keyId))
      {
        var copy = new KeyBuffer(keyBytes.Span());
        _store.Add(copy.GetId(), copy);
      }
      return _store[keyId];
    }
  }

  /// <summary>
  /// Try to find the key, and if found, invoke <paramref name="keyAction"/> and return true.
  /// If not found return false.
  /// This method allows using a key in an action, without copying the key to a temporary buffer.
  /// </summary>
  /// <param name="keyId">
  /// The id of the key to find
  /// </param>
  /// <param name="keyAction">
  /// The Action to invoke on the key id and the the key bytes, if found.
  /// </param>
  /// <returns>
  /// True if the key was found and the action was invoked, false if the key was not found.
  /// </returns>
  /// <seealso cref="TryMapKey{T}(Guid, Func{Guid, IBytesWrapper, T})"/>
  public bool TryUseKey(Guid keyId, Action<Guid, IBytesWrapper> keyAction)
  {
    KeyBuffer kb;
    lock(_lock)
    {
      if(!_store.TryGetValue(keyId, out kb!))
      {
        return false;
      }
    }
    keyAction(keyId, kb);
    return true;
  }

  /// <summary>
  /// Try to find the key, and if found, invoke <paramref name="keyFunction"/> and return its
  /// return value.
  /// If not found return null.
  /// This method allows using a key in a function, without copying the key to a temporary buffer.
  /// </summary>
  /// <typeparam name="T">
  /// The return value of the function, which must be a non-value type (so that this method
  /// can return null as a marker)
  /// </typeparam>
  /// <param name="keyId">
  /// The ID of the key to find
  /// </param>
  /// <param name="keyFunction">
  /// The function that receives the key ID and key content as argument
  /// </param>
  /// <returns>
  /// The return value from the function if the key was found, or null if not found
  /// </returns>
  /// <seealso cref="TryUseKey(Guid, Action{Guid, IBytesWrapper})"/>
  public T? TryMapKey<T>(Guid keyId, Func<Guid, IBytesWrapper, T> keyFunction) where T: class
  {
    KeyBuffer kb;
    lock(_lock)
    {
      if(!_store.TryGetValue(keyId, out kb!))
      {
        return null;
      }
    }
    return keyFunction(keyId, kb);
  }

  /// <summary>
  /// Copy all keys in this store that are not already present
  /// in <paramref name="destination"/> to that destination.
  /// </summary>
  public void CopyAllTo(KeyChain destination)
  {
    List<Guid> keyids;
    lock(_lock)
    {
      keyids = _store.Keys.ToList();
    }
    foreach(var keyId in keyids)
    {
      if(!destination.ContainsKey(keyId))
      {
        using(var kb = FindCopy(keyId))
        {
          if(kb != null)
          {
            destination.PutCopy(kb);
          }
        }
      }
    }
  }

  /// <summary>
  /// Remove a key from the chain and dispose it (if it existed).
  /// [Thread safe]
  /// </summary>
  /// <param name="keyId">
  /// The ID of the key to remove
  /// </param>
  /// <returns></returns>
  public bool DeleteKey(Guid keyId)
  {
    lock(_lock)
    {
      if(_store.Remove(keyId, out var key))
      {
        key.Dispose();
        return true;
      }
      else
      {
        return false;
      }
    }
  }

  /// <summary>
  /// Import mising keys in the key ID list from the given key cache,
  /// if available
  /// [Thread safe]
  /// </summary>
  /// <param name="source">
  /// The key cache that may have some of the missing keys available
  /// </param>
  /// <param name="keyIds">
  /// The IDs of the keys to find
  /// </param>
  public void ImportMissingKeys(
    IKeyCacheStore source, IEnumerable<Guid> keyIds)
  {
    // Note that both ContainsKey and PutCopy lock.
    // To avoid deadlock LoadKey must appear outside the lock
    // It is deemed acceptable if the chain actually contains
    // the key by the time PutCopy is called (replacing the key)
    foreach(var keyId in keyIds)
    {
      if(!ContainsKey(keyId))
      {
        using(var key = source.LoadKey(keyId))
        {
          if(key!=null)
          {
            PutCopy(key);
          }
        }
      }
    }
  }

  /// <summary>
  /// If the key with the given ID is not in this chain
  /// try to import it from the key source, returning
  /// true on success, or false on failure.
  /// [Thread safe]
  /// </summary>
  /// <param name="keyId">
  /// The id of the key to look up
  /// </param>
  /// <param name="source">
  /// The source for cached keys.
  /// </param>
  /// <returns>
  /// True if the key was already in the chain or if it was successfully imported,
  /// false if an import was attempted but failed
  /// </returns>
  public bool ImportKey(Guid keyId, IKeyCacheStore source)
  {
    if(ContainsKey(keyId))
    {
      return true;
    }
    using(var key = source.LoadKey(keyId))
    {
      if(key!=null)
      {
        PutCopy(key);
        return true;
      }
    }
    return false;
  }

  /// <summary>
  /// Clear this KeyChain and Dispose all stored keys.
  /// </summary>
  public void Dispose()
  {
    if(_store != null)
    {
      lock(_lock)
      {
        if(_store != null) // double check locking
        {
          var keys = _store.Values;
          _store.Clear();
          foreach(var key in keys)
          {
            key.Dispose();
          }
        }
      }
    }
  }
}
