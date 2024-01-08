/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ZvaultKeyServer.WpfUtilities;

namespace ZvaultKeyServer.Main.Keys;

public class KeyFileInfo: ViewModelBase
{
  public KeyFileInfo(string fullpath, DateTimeOffset mentionTime)
  {
    FullPath = fullpath;
    MentionTime = mentionTime;
  }

  public string FullPath { get; }

  public string Folder { get => Path.GetDirectoryName(FullPath)!; }

  public string FileShort { get => Path.GetFileName(FullPath); }

  public DateTimeOffset MentionTime { 
    get => _mentionTime;
    set {
      if(SetValueProperty(ref _mentionTime, value))
      {
      }
    }
  }
  private DateTimeOffset _mentionTime;
}
