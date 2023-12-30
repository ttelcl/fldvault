/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Upi;
using FldVault.Upi.Implementation.Keys;

using ZvaultKeyServer.WpfUtilities;

namespace ZvaultKeyServer.Main.Keys;

/// <summary>
/// ViewModel wrapping a <see cref="KeyState"/>
/// </summary>
public class KeyViewModel: ViewModelBase
{
  public KeyViewModel(KeyState model)
  {
    Model = model;
    SyncModel();
  }

  public KeyState Model { get; }

  public Guid KeyId { get => Model.KeyId; }

  public string? FullFileName {
    get => _fullFileName;
    set {
      if(SetInstanceProperty(ref _fullFileName, value ?? String.Empty))
      {
        RaisePropertyChanged(nameof(ShortName));
        RaisePropertyChanged(nameof(FolderName));
      }
    }
  }
  private string _fullFileName = string.Empty;

  public string ShortName {
    get => String.IsNullOrEmpty(_fullFileName) ? "-" : Path.GetFileName(_fullFileName);
  }

  public string? FolderName {
    get => String.IsNullOrEmpty(_fullFileName) ? null : Path.GetDirectoryName(_fullFileName);
  }

  public KeyStatus Status {
    get => _status;
    set {
      if(SetValueProperty(ref _status, value))
      {
        RaisePropertyChanged(nameof(StatusIcon));
      }
    }
  }
  private KeyStatus _status;

  public string StatusIcon {
    get {
      return _status switch {
        KeyStatus.Unknown => "LockQuestion",
        KeyStatus.Seeded => "LockAlert",
        KeyStatus.Hidden => "LockOff",
        KeyStatus.Published => "LockOpen",
        _ => "HelpRhombusOutline",
      };
    }
  }

  public DateTimeOffset Stamp {
    get => _stamp;
    private set {
      if(SetValueProperty(ref _stamp, value))
      {
        RaisePropertyChanged(nameof(StampText));
        RaisePropertyChanged(nameof(StampShort));
      }
    }
  }
  private DateTimeOffset _stamp;

  public string StampReason {
    get => _stampReason;
    private set {
      if(SetInstanceProperty(ref _stampReason, value))
      {
      }
    }
  }
  private string _stampReason = "?";

  public string StampText {
    get => _stamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
  }

  public string StampShort {
    get => _stamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
  }

  public void SetStamp(DateTimeOffset stamp, string reason)
  {
    Stamp = stamp;
    StampReason = reason;
  }

  /// <summary>
  /// Synchronize the viewmodel to the model.
  /// </summary>
  /// <returns>
  /// True if the time stamp changed (and therefore the sort order)
  /// </returns>
  public bool SyncModel()
  {
    var oldTime = Stamp;
    var files =
      (from kvp in Model.AssociatedFiles
       orderby kvp.Value descending
       select kvp).ToList();
    if(files.Count>0)
    {
      FullFileName = files[0].Key;
    }
    else
    {
      FullFileName = null;
    }
    // TODO: track files in this model
    Status = Model.Status;
    var stamp = Model.LastRegistered;
    var reason = "Key Registered";
    if(Model.LastAssociated.HasValue && Model.LastAssociated.Value > stamp)
    {
      stamp = Model.LastAssociated.Value;
      reason = "File Registered";
    }
    if(Model.LastRequested.HasValue && Model.LastRequested.Value > stamp)
    {
      stamp = Model.LastRequested.Value;
      reason = "Key Requested";
    }
    if(Model.LastServed.HasValue && Model.LastServed.Value > stamp)
    {
      stamp = Model.LastServed.Value;
      reason = "Key Served";
    }
    SetStamp(stamp, reason);
    return oldTime != Stamp;
  }

}

public class KeyViewModelComparer: IComparer<KeyViewModel>, System.Collections.IComparer
{
  private readonly Comparer<DateTimeOffset> _stampComparer
    = Comparer<DateTimeOffset>.Default;

  public int Compare(KeyViewModel? x, KeyViewModel? y)
  {
    if(x == null)
    {
      if(y == null)
      {
        return 0;
      }
      else
      {
        return 1;
      }
    }
    else
    {
      if(y == null)
      {
        return -1;
      }
      else
      {
        return _stampComparer.Compare(y.Stamp, x.Stamp);
      }
    }
  }

  int System.Collections.IComparer.Compare(object? x, object? y)
  {
    return Compare(x as KeyViewModel, y as KeyViewModel);
  }
}
