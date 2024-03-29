﻿/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Crypto;

namespace FldVault.Upi.Implementation.Keys;

/// <summary>
/// Stores key state
/// </summary>
public class KeyStateStore
{
  private readonly Object _lock = new();
  private readonly Dictionary<Guid, KeyState> _states;

  /// <summary>
  /// Create a new KeyStateStore
  /// </summary>
  public KeyStateStore(
    KeyChain keyChain)
  {
    KeyChain = keyChain;
    _states = new Dictionary<Guid, KeyState>();
  }

  /// <summary>
  /// The key chain storing raw keys
  /// </summary>
  public KeyChain KeyChain { get; }

  /// <summary>
  /// If a key is known, return its state.
  /// Otherwise return null.
  /// Use <see cref="GetKey(Guid)"/> to create a new entry
  /// </summary>
  /// <param name="keyId">
  /// The key for which to retrieve the state
  /// </param>
  /// <returns>
  /// The Key State object, if it exists, or null otherwise
  /// </returns>
  public KeyState? FindKey(Guid keyId)
  {
    lock(_lock)
    {
      if(_states.TryGetValue(keyId, out var keyState))
      {
        return keyState;
      }
      else
      {
        return null;
      }
    }
  }

  /// <summary>
  /// If a key is known, return its state.
  /// Otherwise create and return it.
  /// </summary>
  /// <param name="keyId">
  /// The key for which to retrieve the state
  /// </param>
  /// <returns>
  /// The Key State object, either the existing one
  /// or a new one.
  /// </returns>
  public KeyState GetKey(Guid keyId)
  {
    lock(_lock)
    {
      if(_states.TryGetValue(keyId, out var keyState))
      {
        return keyState;
      }
      else
      {
        keyState = new KeyState(keyId, KeyChain);
        _states[keyId] = keyState;
        return keyState;
      }
    }
  }

  /// <summary>
  /// Remove a key state by id
  /// </summary>
  public KeyState? RemoveKey(Guid keyId)
  {
    lock(_lock)
    {
      var key = FindKey(keyId);
      if(key != null)
      {
        _states.Remove(keyId);
      }
      return key;
    }
  }

  /// <summary>
  /// Return a list containing all States
  /// </summary>
  public IReadOnlyList<KeyState> AllStates {
    get {
      lock(_lock)
      {
        return _states.Values.ToList();
      }
    }
  }

  /// <summary>
  /// Returns true if there are any keys at all
  /// </summary>
  public bool AnyKeys {
    get {
      lock(_lock)
      {
        return _states.Count > 0;
      }
    }
  }

  /// <summary>
  /// Check if any key info is known for the key identified
  /// by the ID
  /// </summary>
  public bool HasKey(Guid keyId)
  {
    return _states.ContainsKey(keyId);
  }
}
