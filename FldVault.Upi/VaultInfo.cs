/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FldVault.Upi;

/// <summary>
/// Minimal information about a ZVault file
/// </summary>
public class VaultInfo
{
  /// <summary>
  /// Create a new VaultInfo instance
  /// </summary>
  public VaultInfo(Guid keyId, int majorVersion, int minorVersion)
  {
    KeyId = keyId;
    MajorVersion = majorVersion;
    MinorVersion = minorVersion;
  }

  /// <summary>
  /// The primary encryption key ID for the vault file.
  /// </summary>
  public Guid KeyId { get; init; }

  /// <summary>
  /// The major vault format version. Expected to be 3.
  /// </summary>
  public int MajorVersion { get; init; }

  /// <summary>
  /// The minor vault format version. Expected to be 0.
  /// </summary>
  public int MinorVersion { get; init; }
}

