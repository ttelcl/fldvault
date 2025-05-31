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
/// This object is mutable and initially empty.
/// </summary>
public class BundleState
{
  /// <summary>
  /// Create a new empty BundleState
  /// </summary>
  public BundleState(
    BundleKey key,
    bool outgoing)
  {
    Key = key;
    Outgoing = outgoing;
  }

  /// <summary>
  /// Create a new BundleState from a BundleRecord, making a snapshot of its state.
  /// </summary>
  public BundleState(BundleRecord bundleRecord)
  {
    Key = bundleRecord.Key;
    BundleStamp = bundleRecord.BundleTime;
    VaultStamp = bundleRecord.VaultTime;
    Outgoing = bundleRecord.HasSourceFile;
  }

  /// <summary>
  /// The bundle key uniquely identifying this object.
  /// </summary>
  public BundleKey Key { get; }

  /// <summary>
  /// The timestamp of the bundle file, if it exists.
  /// </summary>
  public DateTimeOffset? BundleStamp { get; set; } = null;

  /// <summary>
  /// The timestamp of the vault file, if it exists.
  /// </summary>
  public DateTimeOffset? VaultStamp { get; set; } = null;

  /// <summary>
  /// True if a source repo folder is known for this bundle
  /// (even if it no longer exists!)
  /// </summary>
  public bool Outgoing { get; set; } = false;
}
