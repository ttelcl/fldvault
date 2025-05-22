/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Vaults;

namespace GitVaultLib.Configuration;

/// <summary>
/// Describes concrete information about a bundle and its vault.
/// May describe either a local bundle (outgoing) or a remote bundle
/// (incoming)
/// </summary>
public class BundleInfo
{
  /// <summary>
  /// Create a new BundleInfo
  /// </summary>
  public BundleInfo(
    bool outgoing,
    string hostName,
    string repoName,
    string bundleFile,
    string vaultFile,
    Zkey keyInfo)
  {
    Outgoing = outgoing;
    HostName = hostName;
    RepoName = repoName;
    BundleFile = bundleFile;
    VaultFile = vaultFile;
    KeyInfo = keyInfo;
  }

  /// <summary>
  /// Whether this describes a local (outgoing) bundle or a remote
  /// (incoming) bundle. For outgoing bundles the repository is
  /// pushed to the bundle, which is encrypted to the vault. For
  /// incoming bundles, the source is the vault, which is decrypted
  /// to the bundle, which is then fetched to the repository.
  /// </summary>
  public bool Outgoing { get; }

  /// <summary>
  /// The 'host name', logically distinguishing different bundle sources.
  /// For outgoing bundles this could be the actual host name of the current
  /// machine. For incoming bundles this could be the host name of the remote
  /// machine. For incoming bundles this is also used to construct the name
  /// of the GIT remote.
  /// </summary>
  public string HostName { get; }

  /// <summary>
  /// The (logical) repository name. This is used to construct file and
  /// folder names.
  /// </summary>
  public string RepoName { get; }

  /// <summary>
  /// The full path to the bundle file.
  /// </summary>
  public string BundleFile { get; }

  /// <summary>
  /// The full path to the vault file.
  /// </summary>
  public string VaultFile { get; }

  /// <summary>
  /// The key information for the encryption key.
  /// </summary>
  public Zkey KeyInfo { get; }
}
