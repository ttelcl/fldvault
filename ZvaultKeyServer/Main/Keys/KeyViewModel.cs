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
using System.Windows;
using System.Windows.Media;

using FldVault.Upi;
using FldVault.Upi.Implementation.Keys;

using ZvaultKeyServer.WpfUtilities;

namespace ZvaultKeyServer.Main.Keys;

/// <summary>
/// ViewModel wrapping a <see cref="KeyState"/>
/// </summary>
public class KeyViewModel: ViewModelBase
{
  public KeyViewModel(
    KeysViewModel owner,
    KeyState model)
  {
    Owner = owner;
    Model = model;
    SyncModel();
  }

  public KeyState Model { get; }

  public KeysViewModel Owner { get; }

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
      Trace.TraceInformation($"Status({Model.KeyTag}) {_status} -> {value}");
      if(SetValueProperty(ref _status, value))
      {
        RaisePropertyChanged(nameof(StatusIcon));
        RaisePropertyChanged(nameof(StatusDescription));
        if(_status == KeyStatus.Published)
        {
          AutohideLeft = AutohideSeconds;
        }
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

  public string StatusDescription {
    get {
      return _status switch {
        KeyStatus.Unknown => "Key description available, but no information to help unlock it",
        KeyStatus.Seeded => "Passphrase required to unlock this key",
        KeyStatus.Hidden => "Key hidden from clients until you unhide it",
        KeyStatus.Published => "Key available to clients requesting it",
        _ => "(Unexpected status)",
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
    var t = stamp.ToLocalTime().ToString("o");
    Trace.TraceInformation($"Key stamp {Model.KeyTag} : {t} ({reason})");
    Stamp = stamp;
    StampReason = reason;
  }

  public bool ShowKey {
    get => _showKey;
    set {
      Model.HideKey = !value;
      if(SetValueProperty(ref _showKey, value))
      {
        Status = Model.Status;
        if(value)
        {
          ResetTimer();
        }
      }
    }
  }
  private bool _showKey = false;

  public void SetCurrentKeyShowState(bool publish)
  {
    ShowKey = publish;
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
    ShowKey = !Model.HideKey; // Sync back. Normally HideKey -> Model.HideKey
    var stamp = Model.LastStamp;
    var reason = Model.LastStampSource;
    SetStamp(stamp, reason);
    return oldTime != Stamp;
  }

  public string TimeoutText {
    get {
      if(AutohideEnabled)
      {
        return $"{AutohideLeft} / {AutohideSeconds}";
      }
      else
      {
        return "(auto-hide disabled)";
      }
    }
  }

  public bool AutohideEnabled {
    get => _autohideEnabled;
    set {
      if(SetValueProperty(ref _autohideEnabled, value))
      {
        if(!AutohideEnabled)
        {
          ResetTimer();
        }
      }
    }
  }
  private bool _autohideEnabled;

  public int AutohideSeconds {
    get => _autohideSeconds;
    set {
      if(SetValueProperty(ref _autohideSeconds, value))
      {
        if(AutohideSeconds == 0)
        {
          AutohideEnabled = false;
        }
        ResetTimer();
        RaisePropertyChanged(nameof(TimedOut));
      }
    }
  }
  private int _autohideSeconds;

  public int AutohideLeft {
    get => _autohideLeft;
    set {
      if(SetValueProperty(ref _autohideLeft, value))
      {
        RaisePropertyChanged(nameof(TimedOut));
      }
    }
  }
  public int _autohideLeft = 300;

  public bool TimedOut {
    get => _autohideLeft == 0 && _autohideSeconds > 0;
  }

  public void ResetTimer()
  {
    if(AutohideSeconds > 0)
    {
      AutohideLeft = AutohideSeconds;
    }
    else
    {
      // dummy value; the import thing is that it is NOT 0
      AutohideLeft = 300;
    }
  }

  /// <summary>
  /// Grace period for autohide timer. The minimum time for the
  /// time left after the grace period is triggered (e.g. whenever
  /// the key is successfully served)
  /// </summary>
  public int GraceSeconds {
    get => _graceSeconds;
    set {
      if(SetValueProperty(ref _graceSeconds, value))
      {
        ApplyGracePeriod();
      }
    }
  }
  private int _graceSeconds = 15;

  public void ApplyGracePeriod()
  {
    if(Status == KeyStatus.Published
      && _autohideLeft < _graceSeconds)
    {
      AutohideLeft = _graceSeconds;
    }
  }

  private bool IsCurrent()
  {
    return Object.ReferenceEquals(Owner.CurrentKey, this);
  }

  public void TimerTick()
  {
    var dontTick =
      IsCurrent() && Application.Current.MainWindow.IsActive;
    // Suppress timer ticks if this is the current key and this app is the foreground app
    // (avoid changing state while editing)
    if(!dontTick
      && AutohideEnabled
      && _autohideLeft>0
      && Status == KeyStatus.Published)
    {
      AutohideLeft = _autohideLeft-1;
      if(AutohideLeft == 0)
      {
        Trace.TraceInformation($"Key {KeyId} timed out");
        SetCurrentKeyShowState(false);
      }
    }
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
