/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Upi;

namespace FldVault.KeyServer;

/// <summary>
/// Describes the availability of a key
/// (this is a subset of <see cref="KeyStatus"/> as it is presented to clients;
/// <see cref="KeyStatus.Seeded"/> is omitted since it is a server implementation detail,
/// instead transformed to <see cref="KeyPresence.Unavailable"/>)
/// </summary>
public enum KeyPresence: byte
{
  /// <summary>
  /// The key is not known
  /// </summary>
  Unavailable = KeyStatus.Unknown,

  /// <summary>
  /// The key is known, but not available (it is hidden)
  /// </summary>
  Cloaked = KeyStatus.Hidden,

  /// <summary>
  /// The key is known and available
  /// </summary>
  Present = KeyStatus.Published,
}
