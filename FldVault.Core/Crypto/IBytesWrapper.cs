/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto;

/// <summary>
/// An object that exposes a ReadOnlySpan{byte}.
/// </summary>
/// <remarks>
/// Unlike ReadOnlySpan{byte} this interface can be used as a 
/// generic type argument.
/// </remarks>
public interface IBytesWrapper
{
  /// <summary>
  /// Retrieve the wrapped ReadOnlySpan
  /// </summary>
  ReadOnlySpan<byte> Bytes { get; }
}

