/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Upi;

/// <summary>
/// The various states a key in the key server can be in
/// </summary>
public enum KeyStatus
{
  /// <summary>
  /// No information about the key is known
  /// </summary>
  Unknown,

  /// <summary>
  /// The key itself is not known, but information to convert the
  /// right passphrase into the key is available
  /// </summary>
  Seeded,

  /// <summary>
  /// The key is known, but parked in a key chain that is not
  /// publicly accessible to clients. The UI can promote the key
  /// to the published state.
  /// </summary>
  Hidden,

  /// <summary>
  /// The key is known and available to clients. The UI can
  /// retract the key to the Hidden state, for instance to implement
  /// a time out policy.
  /// </summary>
  Published,

}
