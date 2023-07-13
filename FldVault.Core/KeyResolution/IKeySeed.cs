﻿/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
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
}

