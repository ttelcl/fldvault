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
  /// Return a collection of distinct key descriptors found in files in the
  /// given folder (indexed by key GUID).
  /// </summary>
  /// <param name="folder"></param>
  /// <returns></returns>
  public static Dictionary<Guid, Zkey> KeysInFolder(string folder)
  {
    var result = new Dictionary<Guid, Zkey>();
    folder = Path.GetFullPath(folder);
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
