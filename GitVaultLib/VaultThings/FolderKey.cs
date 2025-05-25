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

using FldVault.Core.Vaults;

namespace GitVaultLib.VaultThings;

/// <summary>
/// Static utility class to find the key associated with a folder.
/// </summary>
public static class FolderKey
{
  /// <summary>
  /// The list of file extensions that carry a key descriptor relevant to gitvault
  /// </summary>
  public static readonly IReadOnlyList<string> KeySourceExtensions =
    new List<string> { ".zkey", ".mvlt" };

  /// <summary>
  /// Put a key file in the given folder, constructing the file name from
  /// the folder name.
  /// </summary>
  /// <returns>
  /// True if written, false if the file already exists.
  /// </returns>
  public static bool PutFolderKey(string folder, Zkey key)
  {
    folder = Path.GetFullPath(folder).TrimEnd('/', '\\');
    // Special case the file name that is equal to the folder name with a .zkey extension
    var keyFile = Path.Combine(folder, $"{Path.GetFileName(folder)}.zkey");
    if(!File.Exists(keyFile))
    {
      key.SaveToJsonFile(keyFile);
      return true; // Key file created
    }
    return false; // Key file already exists, no need to overwrite
  }

  /// <summary>
  /// Return a collection of distinct key descriptors found in files in the
  /// given folder (indexed by key GUID).
  /// </summary>
  /// <param name="folder"></param>
  /// <returns></returns>
  public static Dictionary<Guid, Zkey> KeysInFolder(string folder)
  {
    var result = new Dictionary<Guid, Zkey>();
    folder = Path.GetFullPath(folder).TrimEnd('/', '\\');
    // Special case the file name that is equal to the folder name with a .zkey extension
    // If it exists, than it is the key for this folder and other files are ignored.
    var keyFile = Path.Combine(folder, $"{Path.GetFileName(folder)}.zkey");
    if(File.Exists(keyFile))
    {
      var pkif = PassphraseKeyInfoFile.TryFromFile(keyFile);
      if(pkif != null)
      {
        var key = pkif.ToZkey();
        result[key.KeyGuid] = key;
        return result; // No need to look further
      }
    }
    if(Directory.Exists(folder))
    {
      foreach(var extension in KeySourceExtensions)
      {
        var files = Directory.GetFiles(folder, $"*{extension}");
        foreach(var file in files)
        {
          var pkif = PassphraseKeyInfoFile.TryFromFile(file);
          if(pkif != null)
          {
            var key = pkif.ToZkey();
            result[key.KeyGuid] = key;
          }
        }
      }
    }
    return result;
  }
}
