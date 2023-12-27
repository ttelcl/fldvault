/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

using FldVault.Core.Crypto;

namespace FldVault.Core.KeyResolution;

/// <summary>
/// Base interface for <see cref="IParameterKeySeed{TParam}"/>,
/// containing the parameter-independent parts
/// </summary>
public interface IParameterKeySeed
{
  /// <summary>
  /// The ID of the key that this seed represents
  /// </summary>
  Guid KeyId { get; }

  /// <summary>
  /// Try to interpret this IParameterKeySeed as 
  /// <see cref="IParameterKeySeed{TParam}"/>
  /// </summary>
  /// <typeparam name="TParam">
  /// The parameter type
  /// </typeparam>
  /// <returns>
  /// The specialized interface if available, or null if not
  /// available
  /// </returns>
  IParameterKeySeed<TParam>? TryAdapt<TParam>();
}

/// <summary>
/// An object that can instantiate a key, but unlike IKeySeed, requiring an
/// additional input to do so.
/// </summary>
public interface IParameterKeySeed<TParam>: IParameterKeySeed
{

  /// <summary>
  /// Try resolving the key from this seed using the given parameter
  /// </summary>
  /// <param name="param">
  /// The parameter used to resolve the seed
  /// </param>
  /// <param name="chain">
  /// The keychain where the resolved key is stored, if successful
  /// </param>
  /// <returns>
  /// True if the key was successfully resolved, false otherwise.
  /// </returns>
  public bool TryResolve(TParam param, KeyChain chain);
}

