/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FldVault.Core.Crypto;

namespace FldVault.Core.KeyResolution;

/// <summary>
/// An object that can load a specific key into a <see cref="KeyChain"/>,
/// without divulging the details of how to get the key.
/// Normally created via an <see cref="IKeySeedService"/>
/// </summary>
public interface IKeySeed
{
  /// <summary>
  /// The ID of the key that this seed represents
  /// </summary>
  Guid KeyId { get; }

  /// <summary>
  /// Try to resolve the key and put it in the <see cref="KeyChain"/> if
  /// successful. 
  /// </summary>
  /// <param name="keyChain">
  /// The key chain to put the result in. This may also be used to access
  /// related keys and store intermediate keys.
  /// </param>
  /// <returns>
  /// True if the key was successfully loaded into the key chain
  /// </returns>
  bool TryResolveKey(KeyChain keyChain);

  /// <summary>
  /// Write this key identification info as a block in the given blockstream,
  /// which is a brand new vault file being created. Implementations may
  /// choose to ignore this method.
  /// </summary>
  /// <returns>
  /// True if the seed wrote its information to the stream, false if it
  /// skipped the request.
  /// </returns>
  bool WriteAsBlock(Stream stream);

  /// <summary>
  /// Try to adapt this seed to a more specific one.
  /// In the case of composite keys this may return multiple child seeds.
  /// In the typical case it returns no results or a single result
  /// being this seed itself.
  /// </summary>
  IEnumerable<IKeySeed<T>> TryAdapt<T>();
}

/// <summary>
/// A specialization of IKeySeed that exposes a key type specific
/// detail.
/// </summary>
public interface IKeySeed<out T> : IKeySeed
{
  /// <summary>
  /// Get the wrapped key-specific object
  /// </summary>
  T KeyDetail { get; }
}
