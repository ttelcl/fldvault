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
/// Information about a key in the server for use by the host.
/// </summary>
public interface IKeyInfo
{
  /// <summary>
  /// The key identifier
  /// </summary>
  Guid KeyId { get; }

  /// <summary>
  /// The latest status of the key
  /// </summary>
  KeyStatus Status { get; }

  /// <summary>
  /// A map of fully specified names of files that use this key to
  /// timestamps when the server was alerted to that file.
  /// </summary>
  IReadOnlyDictionary<string, DateTimeOffset> AssociatedFiles { get; }
}
