using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core;

/// <summary>
/// Information about a folder that acts as vault
/// </summary>
public class VaultFolder
{

  private VaultFolder(string folder, Guid vaultGuid)
  {
    Folder = Path.GetFullPath(folder);
    VaultGuid = vaultGuid;
    if(!Directory.Exists(Folder))
    {
      throw new DirectoryNotFoundException(
        $"The vault folder does not exist yet ({Folder})");
    }
    var tagFile = $"{VaultGuid:D}.vault-tag";
    if(!File.Exists(tagFile))
    {
      throw new FileNotFoundException(
        $"The vault tag file does not exist - the folder is not a vault");
    }
  }

  /// <summary>
  /// Open an existing vault folder
  /// </summary>
  /// <param name="tagFile">
  /// The path to the tag file inside the folder marking its folder
  /// as vault folder. The name must be a GUID followed by the extension
  /// ".vault-tag"
  /// </param>
  /// <returns></returns>
  /// <exception cref="FileNotFoundException"></exception>
  /// <exception cref="InvalidOperationException"></exception>
  public static VaultFolder Open(string tagFile)
  {
    tagFile = Path.GetFullPath(tagFile);
    if(!File.Exists(tagFile))
    {
      throw new FileNotFoundException(
        "The vault tag file is missing",
        tagFile);
    }
    var folder = Path.GetDirectoryName(tagFile);
    var shortname = Path.GetFileNameWithoutExtension(tagFile);
    var extension = Path.GetExtension(tagFile);
    if(String.IsNullOrEmpty(folder))
    {
      throw new InvalidOperationException(
        "Invalid folder name");
    }
    if(String.IsNullOrEmpty(shortname)
      || !Guid.TryParseExact(shortname, "D", out var vaultGuid))
    {
      throw new InvalidOperationException(
        "Invalid tag file name (expecting '<GUID>.vault-tag')");
    }
    if(String.IsNullOrEmpty(extension)
      || !String.Equals(extension, ".vault-tag", StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException(
        "Invalid tag file extension (expecting '.vault-tag')");
    }
    return new VaultFolder(folder, vaultGuid);
  }

  /// <summary>
  /// Initialize a new vault, creating a new folder (if it did not exist yet)
  /// and the tag file (if it did not exist yet). Fails if the folder already
  /// contains another vault. Does not initialize a key.
  /// </summary>
  public static VaultFolder Initialize(string folder, Guid? vaultGuid = null)
  {
    folder = Path.GetFullPath(folder);
    if(!Directory.Exists(folder))
    {
      Directory.CreateDirectory(folder);
      vaultGuid ??= Guid.NewGuid();
    }
    else
    {
      var tagFiles = Directory.GetFiles(folder, "*.vault-tag");
      if(tagFiles.Length == 0)
      {
        vaultGuid ??= Guid.NewGuid();
      }
      else if(tagFiles.Length == 1)
      {
        var probe = Open(tagFiles[0]);
        if(vaultGuid != null && vaultGuid.Value != probe.VaultGuid)
        {
          throw new InvalidOperationException(
            "The folder already contains a different vault");
        }
        if(vaultGuid == null)
        {
          vaultGuid = probe.VaultGuid;
        }
      }
      else
      {
        throw new InvalidOperationException(
          "The folder is already tagged as multiple conflicting vaults");
      }
    }
    string tagFile = $"{vaultGuid.Value:D}.vault-tag";
    tagFile = Path.Combine(folder, tagFile);
    if(!File.Exists(tagFile))
    {
      File.WriteAllBytes(tagFile, new byte[0]);
    }
    return new VaultFolder(folder, vaultGuid.Value);
  }

  /// <summary>
  /// The root folder
  /// </summary>
  public string Folder { get; }

  /// <summary>
  /// The vault's GUID
  /// </summary>
  public Guid VaultGuid { get; }
}
