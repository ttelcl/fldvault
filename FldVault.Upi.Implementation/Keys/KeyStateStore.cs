/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Crypto;
using FldVault.Upi;

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
  public KeyState? GetKey(Guid keyId)
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

}
