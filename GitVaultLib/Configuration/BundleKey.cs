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
/// An immutable triplet of anchor name, repository name, and host name,
/// uniquely identifying a bundle file and its vault file (even when they do not yet
/// exist)
/// </summary>
public class BundleKey: IEquatable<BundleKey>
{
  /// <summary>
  /// Create a new BundleKey
  /// </summary>
  public BundleKey(
    string anchorName,
    string repoName,
    string hostName)
  {
    AnchorName = anchorName;
    RepoName = repoName;
    HostName = hostName;
    // A '|' is used as separator instead of '.', because '.' is a legal
    // character in repository names. 
    KeyToken = String.Concat(AnchorName, "|", RepoName, "|", HostName);
  }

  /// <summary>
  /// Construct a new BundleKey instance from the name of a vault file and its anchor name.
  /// For this overload the file name can be relative, the path part is ignored.
  /// </summary>
  /// <param name="anchorName">
  /// A valid vault anchor name, which must be registered in the central settings.
  /// This method does not check if the anchor name is registered, only that it is valid.
  /// </param>
  /// <param name="vaultFileName">
  /// The name of the vault file. The file does not need to exist. It can be absolute or
  /// relative (the directory is ignored)
  /// It must match the pattern "{reponame}.{hostname}.-.bundle.{zkeytag}.mvlt"
  /// </param>
  /// <returns></returns>
  public static BundleKey FromVaultFileName(
    string anchorName,
    string vaultFileName)
  {
    if(!CentralSettings.IsValidName(anchorName, false))
    {
      throw new ArgumentException(
        $"Anchor tag '{anchorName}' is not valid. " +
        "Only letters, digits, '-', and '_' are allowed.");
    }
    vaultFileName = Path.GetFileName(vaultFileName);
    var segments = vaultFileName.Split('.');
    if(segments.Length < 6)
    {
      throw new ArgumentException(
        $"Vault file name '{vaultFileName}' is not valid. " +
        "The file name must be of the form {reponame}.{hostname}.-.bundle.{zkeytag}.mvlt.");
    }

    var x = segments[0..^5]; // All but the last 5 segments
    var repoName = String.Join(".", x);
    var mvlt = segments[^1]; // The last segment
    var _ = segments[^2]; // The second to last segment (not validated)
    var bundle = segments[^3]; // The third to last segment
    var dash = segments[^4]; // The fourth to last segment
    var hostName = segments[^5]; // The fifth to last segment
    if(!mvlt.Equals("mvlt", StringComparison.OrdinalIgnoreCase)
      || !bundle.Equals("bundle", StringComparison.OrdinalIgnoreCase)
      || dash != "-")
    {
      throw new ArgumentException(
        $"Vault file name '{vaultFileName}' is not valid. " +
        "The file name must be of the form {reponame}.{hostname}.-.bundle.{zkeytag}.mvlt.");
    }
    return new BundleKey(anchorName, repoName, hostName);
  }

  /// <summary>
  /// Construct a new BundleKey instance from the name of a bundle file and its anchor name.
  /// For this overload the file name can be relative, the path part is ignored.
  /// </summary>
  /// <param name="anchorName">
  /// A valid vault anchor name, which must be registered in the central settings.
  /// This method does not check if the anchor name is registered, only that it is valid.
  /// </param>
  /// <param name="bundleFileName">
  /// The name of the bundle file. The file does not need to exist. It can be absolute or
  /// relative (the directory is ignored).
  /// It must match the pattern "{reponame}.{hostname}.-.bundle"
  /// </param>
  /// <returns></returns>
  public static BundleKey FromBundleFileName(
    string anchorName,
    string bundleFileName)
  {
    if(!CentralSettings.IsValidName(anchorName, false))
    {
      throw new ArgumentException(
        $"Anchor tag '{anchorName}' is not valid. " +
        "Only letters, digits, '-', and '_' are allowed.");
    }
    bundleFileName = Path.GetFileName(bundleFileName);
    var segments = bundleFileName.Split('.');
    if(segments.Length < 6)
    {
      throw new ArgumentException(
        $"Bundle file name '{bundleFileName}' is not valid. " +
        "The file name must be of the form {reponame}.{hostname}.-.bundle.");
    }

    var x = segments[0..^3]; // All but the last 3 segments
    var repoName = String.Join(".", x);
    var bundle = segments[^1]; // The last segment
    var dash = segments[^2]; // The second to last segment
    var hostName = segments[^3]; // The third to last segment
    if(!bundle.Equals("bundle", StringComparison.OrdinalIgnoreCase)
      || dash != "-")
    {
      throw new ArgumentException(
        $"Bundle file name '{bundleFileName}' is not valid. " +
        "The file name must be of the form {reponame}.{hostname}.-.bundle.");
    }
    return new BundleKey(anchorName, repoName, hostName);
  }

  /// <summary>
  /// The vault anchor name.
  /// </summary>
  public string AnchorName { get; }

  /// <summary>
  /// The repository name.
  /// </summary>
  public string RepoName { get; }

  /// <summary>
  /// The host name.
  /// </summary>
  public string HostName { get; }

  /// <summary>
  /// Ensure that the non-null parameters match the triplet (case insensitive).
  /// </summary>
  /// <param name="anchorName">
  /// The anchor name to match. If null, it is ignored.
  /// </param>
  /// <param name="repoName">
  /// The repository name to match. If null, it is ignored.
  /// </param>
  /// <param name="hostName">
  /// The host name to match. If null, it is ignored.
  /// </param>
  /// <returns></returns>
  public bool PartialMatch(string? anchorName, string? repoName, string? hostName)
  {
    if(anchorName is not null && !AnchorName.Equals(anchorName, StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }
    if(repoName is not null && !RepoName.Equals(repoName, StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }
    if(hostName is not null && !HostName.Equals(hostName, StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }
    return true;
  }

  /// <summary>
  /// A unique string for this triplet (AnchorName|RepoName|HostName).
  /// A '|' is used as separator instead of '.', because '.' is a legal
  /// character in repository names. This token should be treated as
  /// case insensitive.
  /// </summary>
  public string KeyToken { get; }

  /// <inheritdoc />
  public override string ToString() => KeyToken;

  /// <inheritdoc />
  public override bool Equals(object? obj)
  {
    if(obj is BundleKey other)
    {
      return KeyToken.Equals(other.KeyToken, StringComparison.OrdinalIgnoreCase);
    }
    return false;
  }

  /// <inheritdoc />
  public bool Equals(BundleKey? other)
  {
    return other is not null &&
           KeyToken.Equals(other.KeyToken, StringComparison.OrdinalIgnoreCase);
  }

  /// <inheritdoc />
  public override int GetHashCode()
  {
    return KeyToken.GetHashCode(StringComparison.OrdinalIgnoreCase);
  }
}