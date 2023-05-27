/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;

using FldVault.Core.Crypto;

namespace FldVault.Core.Vaults
{
  /// <summary>
  /// Interface for persisted storage of raw keys
  /// </summary>
  public interface IKeyCacheStore
  {
    /// <summary>
    /// Try to find a key by ID, creating a new instance
    /// of <see cref="KeyBuffer"/> if found.
    /// </summary>
    /// <param name="keyId">
    /// The ID of the key
    /// </param>
    /// <returns>
    /// A newly created KeyBuffer if found, null if not found
    /// </returns>
    KeyBuffer? LoadKey(Guid keyId);

    /// <summary>
    /// If the persisted key exists, clear and delete it.
    /// </summary>
    /// <returns>
    /// True if the persisted key existed and was deleted.
    /// </returns>
    bool EraseKey(Guid keyId);

    /// <summary>
    /// Persist a key in this store.
    /// </summary>
    /// <param name="keyBuffer">
    /// The key to store
    /// </param>
    /// <param name="overwrite">
    /// Determines what happens if the target persisted key already exists.
    /// If true, the existing one is erased and deleted, then newly written.
    /// If false (default), this StoreKey request is ignored (nothing is
    /// written, assuming that the existing persisted key is equivalent)
    /// </param>
    /// <returns>
    /// True if the key was stored, false if it already existed
    /// </returns>
    bool StoreKey(KeyBuffer keyBuffer, bool overwrite = false);
  }
}