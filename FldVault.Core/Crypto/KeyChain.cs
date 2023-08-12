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
  public int KeyCount => _store.Count;

  /// <summary>
  /// Enumerate fingerprints for the keys in the key chain.
  /// Each "fingerprint" is the first 13 characters of the key ID:
  /// enough to verify a key is present, but not enough to reconstruct
  /// the key id.
  /// </summary>
  public IEnumerable<string> EnumerateFingerprints()
  {
    return
      _store
        .Keys
        .Select(key => key.ToString().Substring(0, 13));
  }

  /// <summary>
  /// Returns true if <paramref name="key"/> is present in this store
  /// </summary>
  public bool ContainsKey(Guid key) => _store.ContainsKey(key);

  /// <summary>
  /// Look up and return the key identified by the key. Do NOT dispose
  /// the returned value (if any), this KeyChain takes care of that.
  /// </summary>
  public KeyBuffer? FindDirect(Guid guid)
  {
    return _store.TryGetValue(guid, out var key) ? key : null;
  }

  /// <summary>
  /// Look up the key identified by the key and return a copy if found.
  /// DO dispose the returned value after use (if any).
  /// </summary>
  public KeyBuffer? FindCopy(Guid guid)
  {
    var raw = FindDirect(guid);
    return (raw != null) ? new KeyBuffer(raw.Bytes) : null;
  }

  /// <summary>
  /// Remove the key identified by the guid from this key chain
  /// and return it (if it is present). The caller is now reponsible
  /// for disposing the returned key (if any)
  /// </summary>
  /// <param name="keyId"></param>
  /// <returns></returns>
  public KeyBuffer? Extract(Guid keyId)
  {
    if(_store.TryGetValue(keyId, out var key))
    {
      _store.Remove(keyId);
      return key;
    }
    else
    {
      return null;
    }
  }

  /// <summary>
  /// Copy all keys in this store that are not already present
  /// in <paramref name="destination"/> to that destination.
  /// </summary>
  public void CopyAllTo(KeyChain destination)
  {
    foreach(var kvp in _store)
    {
      if(!destination.ContainsKey(kvp.Key))
      {
        destination.PutCopy(kvp.Value);
      }
    }
  }

  /// <summary>
  /// Remove a key from the chain and dispose it (if it existed)
  /// </summary>
  /// <param name="keyId">
  /// The ID of the key to remove
  /// </param>
  /// <returns></returns>
  public bool Delete(Guid keyId)
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

  /// <summary>
  /// Put a copy of the given key in this chain, if it isn't there already.
  /// The stored copy is always a plain <see cref="KeyBuffer"/>, not the
  /// subclass implementing <paramref name="key"/>
  /// </summary>
  /// <param name="key">
  /// The key to copy into
  /// </param>
  /// <remarks>
  /// <para>
  /// This is probably the most robust of the key insertion methods, since it
  /// doesn't affect keys already present and key Disposal flow is unconditional
  /// (the caller stays reponsible for disposing the key provided for insertion).
  /// </para>
  /// <para>
  /// The drawback is that the stored copy loses any extras the key implementation
  /// had if it was a subclass of KeyBuffer (e.g. the salt in a PassphraseKey is
  /// lost)
  /// </para>
  /// </remarks>
  public KeyBuffer PutCopy(KeyBuffer key)
  {
    if(!_store.ContainsKey(key.GetId()))
    {
      var copy = new KeyBuffer(key.Bytes);
      _store.Add(copy.GetId(), copy);
    }
    return _store[key.GetId()];
  }

  /// <summary>
  /// Put a key in this key chain. If a key with the same ID already exists,
  /// this call fails. This key chain now becomes responsible for disposing
  /// the key.
  /// </summary>
  /// <param name="key">
  /// The key to be stored
  /// </param>
  /// <exception cref="InvalidOperationException">
  /// Thrown when a key with the same ID already exists
  /// </exception>
  public void Put(KeyBuffer key)
  {
    if(_store.ContainsKey(key.GetId()))
    {
      throw new InvalidOperationException(
        $"Duplicate key: {key.GetId()}");
    }
    else
    {
      _store[key.GetId()] = key;
    }
  }

  /// <summary>
  /// Try to put the key in this key chain.
  /// Returns false if there was another instance in the slot already.
  /// Returns true if successful (and this chain has taking over the
  /// responsibility of disposing it later on)
  /// </summary>
  /// <param name="key">
  /// The key to store
  /// </param>
  /// <returns></returns>
  public bool TryPut(KeyBuffer key)
  {
    if(_store.ContainsKey(key.GetId()))
    {
      return false;
    }
    else
    {
      _store[key.GetId()] = key;
      return true;
    }
  }

  /// <summary>
  /// Put or replace a key
  /// </summary>
  /// <param name="key">
  /// The key to insert
  /// </param>
  /// <returns>
  /// The original key with the same ID, if any
  /// </returns>
  public KeyBuffer? Replace(KeyBuffer key)
  {
    var id = key.GetId();
    if(_store.TryGetValue(id, out var original))
    {
      _store[id] = key;
      return original;
    }
    else
    {
      _store[id] = key;
      return null;
    }
  }

  /// <summary>
  /// Import mising keys in the key ID list from the given key cache,
  /// if available
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
    foreach(var keyId in keyIds)
    {
      if(!_store.ContainsKey(keyId))
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
      var keys = _store.Values;
      _store.Clear();
      foreach(var key in keys)
      {
        key.Dispose();
      }
    }
  }
}
