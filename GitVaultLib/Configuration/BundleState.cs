/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitVaultLib.Configuration;

/// <summary>
/// Tracks the state of a logical bundle: its bundle file stamp and existsence,
/// its vault file stamp and existence, and whether it is incoming or outgoing.
/// This object is immutable. It implements <see cref="IBundleState"/> for
/// full bundles.
/// </summary>
public class BundleState: IBundleState
{
  /// <summary>
  /// Create a new BundleState from a BundleRecord, making a snapshot of its state.
  /// </summary>
  public BundleState(BundleRecord bundleRecord)
  {
    Key = bundleRecord.Key;
    BundleStamp = bundleRecord.BundleTime;
    VaultStamp = bundleRecord.VaultTime;
    Outgoing = bundleRecord.HasSourceFile;
    _ = bundleRecord.TryGetVaultFileName(out var vaultFileName);
    VaultFileNameIfKnown = vaultFileName;
    BundleFileName = bundleRecord.BundleFileName;
  }

  /// <summary>
  /// The bundle key uniquely identifying this object.
  /// </summary>
  public BundleKey Key { get; }

  /// <inheritdoc/>
  public string BundleFileName { get; }

  /// <inheritdoc/>
  public string? VaultFileNameIfKnown { get; }

  /// <inheritdoc/>
  public DateTimeOffset? BundleStamp { get; }

  /// <inheritdoc/>
  public DateTimeOffset? VaultStamp { get; }

  /// <inheritdoc/>
  public bool Outgoing { get; }
}
