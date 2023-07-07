/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Crypto;

namespace FldVault.Core.Vaults;

/// <summary>
/// Describes a folder containing encrypted content: zero
/// or more vault files and the key-info files they need.
/// </summary>
public class VaultsFolder
{
  // While this class is not used elsewhere in this library, it is used directly
  // in zvlt.exe. So: don't delete this file.

  private List<KeyInfoName>? _keyinfoCache = null;

  /// <summary>
  /// Create a new VaultsFolder
  /// </summary>
  public VaultsFolder(
    string folder)
  {
    Folder = Path.GetFullPath(folder);
  }

  /// <summary>
  /// The full path to the folder
  /// </summary>
  public string Folder { get; init; }

  /// <summary>
  /// Scan the folder for name conformant key-info files
  /// (uncached - each call scans the folder)
  /// </summary>
  /// <returns>
  /// The KeyInfoName objects derived from the key-info files' names.
  /// </returns>
  public IEnumerable<KeyInfoName> ScanKeys()
  {
    var di = new DirectoryInfo(Folder);
    if(di.Exists) // if there is no folder then there no key files, of course
    {
      foreach(var fi in di.GetFiles("*.key-info"))
      {
        var kin = KeyInfoName.TryFromFile(fi.FullName);
        if(kin != null)
        {
          yield return kin;
        }
      }
    }
  }

  /// <summary>
  /// Return a cached copy of the output of <see cref="ScanKeys()"/>.
  /// </summary>
  /// <param name="rescan">
  /// Force re-loading the cache
  /// </param>
  /// <returns></returns>
  public IReadOnlyList<KeyInfoName> KeyInfoCache(bool rescan = false)
  {
    if(rescan || _keyinfoCache == null)
    {
      _keyinfoCache = ScanKeys().ToList();
    }
    return _keyinfoCache.AsReadOnly();
  }

  /// <summary>
  /// Return keys from the key name cache with the given key ID, and if not not null
  /// the given key kind.
  /// </summary>
  /// <param name="keyId">
  /// The ID of the key to find key-info record(s) on.
  /// </param>
  /// <param name="keyKind">
  /// If not null: the key kind to look for (default null)
  /// </param>
  /// <param name="rescan">
  /// If true, force a cache refresh first
  /// </param>
  /// <returns></returns>
  public IEnumerable<KeyInfoName> FindKeys(
    Guid keyId,
    string? keyKind = null,
    bool rescan = false)
  {
    if(String.IsNullOrEmpty(keyKind))
    {
      return KeyInfoCache(rescan).Where(kin => kin.KeyId == keyId);
    }
    else
    {
      return KeyInfoCache(rescan).Where(kin => kin.KeyId == keyId && kin.Kind == keyKind);
    }
  }

  /// <summary>
  /// Return keys from the key name cache with the given key ID, and if not not null
  /// the given key kind.
  /// </summary>
  /// <param name="prefix">
  /// A prefix of the key ID to find (case insensitive, using "D" serialization
  /// of GUIDs).
  /// </param>
  /// <param name="keyKind">
  /// If not null: the key kind to look for (default null)
  /// </param>
  /// <param name="rescan">
  /// If true, force a cache refresh first
  /// </param>
  /// <returns></returns>
  public IEnumerable<KeyInfoName> FindKeysByPrefix(
    string prefix,
    string? keyKind = null,
    bool rescan = false)
  {
    if(String.IsNullOrEmpty(keyKind))
    {
      return KeyInfoCache(rescan)
        .Where(kin => kin.KeyId.ToString("D").StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase));
    }
    else
    {
      return KeyInfoCache(rescan)
        .Where(kin => kin.KeyId.ToString("D")
                               .StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase) && kin.Kind == keyKind);
    }
  }

  /// <summary>
  /// Write a passphrase based key-info object to a *.pass.key-info into this folder
  /// </summary>
  /// <param name="pkif"></param>
  /// <returns>
  /// The full name of the written file
  /// </returns>
  public string PutKeyInfo(PassphraseKeyInfoFile pkif)
  {
    var name = pkif.WriteToFolder(Folder);
    KeyInfoCache(true);
    return name;
  }

  /// <summary>
  /// Import keys used in this VaultsFolder into the key chain if they
  /// are missing there but are available in the source.
  /// </summary>
  /// <param name="keyChain"></param>
  /// <param name="source"></param>
  /// <param name="rescan"></param>
  public void ImportKeysIntoChain(
    KeyChain keyChain,
    IKeyCacheStore source,
    bool rescan = false)
  {
    keyChain.ImportMissingKeys(source, KeyInfoCache(rescan).Select(kin => kin.KeyId));
  }

  /// <summary>
  /// Try to read the key-info file indicated by <paramref name="kin"/> from 
  /// this folder as a passphrase-based key-info
  /// </summary>
  /// <param name="kin"></param>
  /// <returns></returns>
  public PassphraseKeyInfoFile? TryReadPassphraseInfo(KeyInfoName kin)
  {
    return PassphraseKeyInfoFile.TryRead(kin, Folder);
  }
}
