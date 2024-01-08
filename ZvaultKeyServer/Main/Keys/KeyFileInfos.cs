/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ZvaultKeyServer.WpfUtilities;

namespace ZvaultKeyServer.Main.Keys;

public class KeyFileInfos: ViewModelBase
{
  public KeyFileInfos()
  {
    _files = [];
  }

  public ObservableCollection<KeyFileInfo> Files {
    get => _files;
  }
  private ObservableCollection<KeyFileInfo> _files;

  public KeyFileInfo? CurrentFile {
    get => _currentFile;
    set {
      if(SetNullableInstanceProperty(ref _currentFile, value))
      {
      }
    }
  }
  private KeyFileInfo? _currentFile;

  public KeyFileInfo? NewestFile {
    get => _newestFile;
    set {
      if(SetNullableInstanceProperty(ref _newestFile, value))
      {
      }
    }
  }
  private KeyFileInfo? _newestFile;

  public void Resynchronize(IReadOnlyList<KeyValuePair<string, DateTimeOffset>> sortedFiles)
  {
    // for now, keep this simple but robust, replacing all entries
    var oldCurrent = CurrentFile;
    KeyFileInfo? newCurrent = null;
    _files.Clear();
    foreach(var kvp in sortedFiles)
    {
      var kfi = new KeyFileInfo(kvp.Key, kvp.Value);
      _files.Add(kfi);
      if(oldCurrent?.FullPath == kvp.Key)
      {
        newCurrent = kfi;
      }
    }
    CurrentFile = newCurrent;
    NewestFile = _files.FirstOrDefault();
  }

  //public void InsertFile(KeyFileInfo file)
  //{
  //  var index = InsertionIndex(file.MentionTime);
  //  _files.Insert(index, file);
  //}

  //public void InsertOrUpdateFile(string fullName, DateTimeOffset mentionTime)
  //{
  //  var idx0 = FindIndex(fullName);
  //  if(idx0 < 0)
  //  {
  //    // new entry
  //    InsertFile(new KeyFileInfo(fullName, mentionTime));
  //  }
  //  else
  //  {
  //    var item = _files[idx0];
  //    if(item.MentionTime != mentionTime)
  //    {
  //      var idx1 = InsertionIndex(mentionTime);
  //      item.MentionTime = mentionTime;
  //      _files.Move(idx0, idx1);
  //    }
  //    else
  //    {
  //      // operation is a no-op (existing entry is not modified)
  //    }
  //  }
  //}

  //public int FindIndex(string fullName)
  //{
  //  for(var i = 0; i < _files.Count; i++)
  //  {
  //    if(_files[i].FullPath == fullName)
  //    {
  //      return i;
  //    }
  //  }
  //  return -1;
  //}

  //private int InsertionIndex(DateTimeOffset t)
  //{
  //  for(var idx = 0; idx < _files.Count; idx++)
  //  {
  //    if(t < _files[idx].MentionTime)
  //    {
  //      return idx;
  //    }
  //  }
  //  return _files.Count;
  //}

}
