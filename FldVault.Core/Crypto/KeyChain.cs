/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto;

/// <summary>
/// Storage and lifetime management for KeyBuffers. Maps Key IDs to KeyBuffer instances
/// </summary>
public class KeyChain: IDisposable
{
  private readonly Dictionary<Guid, KeyBuffer> _store;

  /// <summary>
  /// Create a new KeyChain
  /// </summary>
  public KeyChain()
  {
    _store = new Dictionary<Guid, KeyBuffer>();
  }

  /// <summary>
  /// Find a key by its identifier, returning null if not found.
  /// </summary>
  public KeyBuffer? this[Guid guid] {
    get {
      return _store.TryGetValue(guid, out var key) ? key : null;
    }
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
  public void PutCopy(KeyBuffer key)
  {
    if(!_store.ContainsKey(key.GetId()))
    {
      var copy = new KeyBuffer(key.Bytes);
      _store.Add(copy.GetId(), copy);
    }
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
