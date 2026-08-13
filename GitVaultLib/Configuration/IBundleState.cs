/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;

namespace GitVaultLib.Configuration;

/// <summary>
/// Describes a bundle - vault pair for a full, delta, or incremental bundle
/// </summary>
public interface IBundleState
{

  /// <summary>
  /// The full bundle file name
  /// </summary>
  string BundleFileName { get; }

  /// <summary>
  /// The full vault file name if known, or <see langword="null"/> if the vault key
  /// is unknown, since the file name cannot be deduced in that case.
  /// </summary>
  string? VaultFileNameIfKnown { get; }

  /// <summary>
  /// The timestamp of the bundle file, if it exists.
  /// </summary>
  DateTimeOffset? BundleStamp { get; }

  /// <summary>
  /// The timestamp of the vault file, if it exists.
  /// </summary>
  DateTimeOffset? VaultStamp { get; }

  /// <summary>
  /// True if a source repo folder is known for this bundle
  /// (even if it no longer exists!)
  /// </summary>
  bool Outgoing { get; }
}
