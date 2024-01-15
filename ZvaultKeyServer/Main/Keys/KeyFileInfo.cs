/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

  /// <summary>
  /// The short name of the file (the name without directory)
  /// </summary>
  public string FileShort { get => Path.GetFileName(FullPath); }

  public DateTimeOffset MentionTime { 
    get => _mentionTime;
    set {
      if(SetValueProperty(ref _mentionTime, value))
      {
        RaisePropertyChanged(nameof(MentionTimeShort));
      }
    }
  }
  private DateTimeOffset _mentionTime;

  public string MentionTimeShort {
    get => MentionTime.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
  }

  public string MentionTimeFull {
    get => MentionTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
  }
}
