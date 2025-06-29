﻿/*
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
using System.Windows.Input;
using System.Windows.Media;

using FldVault.Core.Crypto;
using FldVault.Core.KeyResolution;
using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;
using FldVault.Upi;
using FldVault.Upi.Implementation.Keys;

using Microsoft.Win32;

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
    _autohideSeconds = Owner.DefaultTimeout;
    _timeoutValue = SecondsToText(_autohideSeconds);
    ResetTimeoutCommand = new DelegateCommand(
      p => ResetTimer(),
      p => AutohideEnabled);
    UnloadKeyCommand = new DelegateCommand(
      p => UnloadKey(),
      p => Owner.Model.KeyChain.ContainsKey(KeyId));
    NewVaultCommand = new DelegateCommand(
      p => NewVault(),
      p => Status == KeyStatus.Published);
    SaveZkeyCommand = new DelegateCommand(
      p => SaveZkey(),
      p => Status == KeyStatus.Published);
    CopyZkeyCommand = new DelegateCommand(
      p => CopyZkey(),
      p => Status == KeyStatus.Published);
    CopyKeyIdCommand = new DelegateCommand(
      p => CopyKeyId());
    KeyFiles = new(this);
    ResetTimer();
    SyncModel();
  }

  public ICommand ResetTimeoutCommand { get; }

  public ICommand UnloadKeyCommand { get; }

  public ICommand NewVaultCommand { get; }

  public ICommand SaveZkeyCommand { get; }

  public ICommand CopyZkeyCommand { get; }

  public ICommand CopyKeyIdCommand { get; }

  public KeyState Model { get; }

  public KeysViewModel Owner { get; }

  public Guid KeyId { get => Model.KeyId; }

  public KeyFileInfos KeyFiles { get; }

  /// <summary>
  /// -> KeyFiles.NewestFile.FullName
  /// </summary>
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

  /// <summary>
  /// -> KeyFiles.NewestFile.ShortName
  /// </summary>
  public string ShortName {
    get => String.IsNullOrEmpty(_fullFileName) ? "-" : Path.GetFileName(_fullFileName);
  }

  /// <summary>
  /// -> KeyFiles.NewestFile.FolderName
  /// </summary>
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
        RaisePropertyChanged(nameof(IsPublished));
        if(_status == KeyStatus.Published)
        {
          ResetTimer();
        }
        SyncAutohideState();
      }
    }
  }
  private KeyStatus _status;

  public bool IsPublished {
    get => Status == KeyStatus.Published;
  }

  public string StatusIcon {
    get {
      return _status switch {
        KeyStatus.Unknown => "LockQuestion",
        KeyStatus.Seeded => "LockAlert",
        KeyStatus.Hidden => "EyeOff",
        KeyStatus.Published => "Eye",
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
        SyncAutohideState();
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
    KeyFiles.Resynchronize(files);
    Status = Model.Status;
    ShowKey = !Model.HideKey; // Sync back. Normally HideKey -> Model.HideKey
    var stamp = Model.LastStamp;
    var reason =
      Model.LastStampSource switch {
        "LastRequested" => "Key Requested",
        "LastAssociated" => "File Associated",
        "LastServed" => "Key Served",
        "LastRegistered" => "Key Referenced",
        _ => $"'{Model.LastStampSource}'"
      };
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
        RaisePropertyChanged(nameof(TimeoutText));
        if(!AutohideEnabled)
        {
          ResetTimer();
        }
        SyncAutohideState();
      }
    }
  }
  private bool _autohideEnabled = true;

  /// <summary>
  /// Number of seconds from a auto-hide reset until automatic hiding.
  /// The hardcoded minimum for this is 20 seconds; the initial value
  /// is 300 seconds (5 minutes)
  /// </summary>
  public int AutohideSeconds {
    get => _autohideSeconds;
    set {
      if(value < 20)
      {
        value = 20;
      }
      if(SetValueProperty(ref _autohideSeconds, value))
      {
        TimeoutValue = SecondsToText(value);
        ResetTimer();
        RaisePropertyChanged(nameof(TimeoutText));
        SyncAutohideState();
      }
    }
  }
  private int _autohideSeconds; // Initialized in constructor based on Owner.DefaultTimerSetting

  public static string SecondsToText(int seconds)
  {
    var minutes = seconds / 60;
    seconds %= 60;
    return $"{minutes}:{seconds:D2}";
  }

  /// <summary>
  /// Equivalent to AutohideSeconds, but as minutes:seconds text
  /// </summary>
  public string TimeoutValue {
    get => _timeoutValue;
    set {
      var parts = value.Split(':');
      if(parts.Length == 2) // else: IGNORE
      {
        var minutes = Int32.Parse(parts[0]);
        var seconds = Int32.Parse(parts[1]);
        if(SetInstanceProperty(ref _timeoutValue, value))
        {
          AutohideSeconds = seconds + 60 * minutes;
        }
      }
    }
  }
  private string _timeoutValue;

  /// <summary>
  /// The number of seconds left until the key will auto-hide, if that
  /// is enabled with <see cref="AutohideEnabled"/>. The value varies
  /// from <see cref="AutohideSeconds"/> downto 0.
  /// </summary>
  public int AutohideLeft {
    get => _autohideLeft;
    set {
      if(SetValueProperty(ref _autohideLeft, value))
      {
        RaisePropertyChanged(nameof(TimeoutText));
        RaisePropertyChanged(nameof(AutohideLeftText));
        SyncAutohideState();
      }
    }
  }
  private int _autohideLeft = 300;

  public string AutohideLeftText {
    get => SecondsToText(AutohideLeft);
  }

  public void ResetTimer()
  {
    AutohideLeft = AutohideSeconds;
    Model.HideKey = false;
    SetCurrentKeyShowState(true); // also unhide, if hidden
  }

  public void UnloadKey()
  {
    if(Model.UnloadKey())
    {
      SyncModel();
    }
  }

  public void NewVault()
  {
    if(Model.Seed is IKeySeed keyseed)
    {
      var prefix = KeyId.ToString().Substring(0, 8);
      var dialog = new SaveFileDialog() {
        Filter = "Z-Vault files (*.zvlt)|*.zvlt",
        OverwritePrompt = true,
        CheckPathExists = true,
        DefaultExt = ".zvlt",
        FileName = $"new-vault.{prefix}.zvlt"
      };
      if(dialog.ShowDialog() == true)
      {
        var fileName = dialog.FileName;
        if(File.Exists(fileName))
        {
          MessageBox.Show(
            "That file already exists; Aborting.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
          return;
        }
        var vf = VaultFile.OpenOrCreate(fileName, keyseed);
        Model.AssociateFile(fileName, false);
        SyncModel();
      }
    }
    else
    {
      MessageBox.Show("Creating a vault for this type of key is not yet supported");
    }
  }

  private void SaveZkey()
  {
    if(Model.Seed is IKeySeed<PassphraseKeyInfoFile> keyseed)
    {
      var pkif = keyseed.KeyDetail;
      var zkey = pkif.ToZkey();
      var zkeyFileName = $"{KeyId}.zkey";
      var dialog = new SaveFileDialog() {
        Filter = "Key description files (*.zkey)|*.zkey",
        OverwritePrompt = true,
        CheckPathExists = true,
        DefaultExt = ".zkey",
        FileName = zkeyFileName,
      };
      if(dialog.ShowDialog() == true)
      {
        var fileName = dialog.FileName;
        var json = zkey.ToString(true);
        File.WriteAllText(fileName, json);
        Model.AssociateFile(fileName, true);
        SyncModel();
      }
    }
    else
    {
      MessageBox.Show("Saving a Z-key for this type of key is not yet supported");
    }
  }

  private void CopyZkey()
  {
    if(Model.Seed is IKeySeed<PassphraseKeyInfoFile> keyseed)
    {
      var pkif = keyseed.KeyDetail;
      var zkey = pkif.ToZkey();
      var transferstring = zkey.ToZkeyTransferString(true);
      Clipboard.SetText(transferstring);
      Owner.StatusHost.StatusMessage =
        $"<ZKEY> for {KeyId} copied to clipboard";
      Owner.CheckClipboard();
    }
    else
    {
      MessageBox.Show("Copying a Z-key for this type of key is not yet supported");
    }
  }

  private void CopyKeyId()
  {
    var keyId = Model.KeyId.ToString();
    Clipboard.SetText(keyId);
    Owner.StatusHost.StatusMessage =
        $"Copied key ID '{KeyId}' to clipboard";
    Owner.CheckClipboard();
  }

  /// <summary>
  /// Grace period for autohide timer. The minimum time for the
  /// time left after the grace period is triggered (e.g. whenever
  /// the key is successfully served).
  /// NO LONGER USED (left in case I change my mind)
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
  private int _graceSeconds = 90;

  public void ApplyGracePeriod()
  {
    var graceSeconds = /*_graceSeconds*/ AutohideSeconds * 2 / 3 ;
    if(graceSeconds > AutohideSeconds)
    {
      graceSeconds = AutohideSeconds;
    }
    if(Status == KeyStatus.Published
      && _autohideLeft < graceSeconds)
    {
      AutohideLeft = graceSeconds;
    }
  }

  private bool IsCurrent()
  {
    return Object.ReferenceEquals(Owner.CurrentKey, this);
  }

  public AutohideState AutohideStatus {
    get => _autohideStatus; 
    set {
      if(SetValueProperty(ref _autohideStatus, value))
      {
        RaisePropertyChanged(nameof(AutohideIsCounting));
        RaisePropertyChanged(nameof(AutohideStatusText));
      }
    }
  }
  private AutohideState _autohideStatus;

  public string AutohideStatusText {
    get {
      return AutohideStatus switch {
        AutohideState.Inactive => "Key not loaded",
        AutohideState.Disabled => "Auto-hide disabled",
        AutohideState.Paused => "Auto-hide paused",
        AutohideState.Running => "Auto-hiding in:",
        AutohideState.Hiding => "(auto-hiding soon)",
        AutohideState.Hidden => "Key hidden from clients",
        _ => $"'{AutohideStatus}'",
      };
    }
  }

  public bool AutohideIsCounting {
    get => Model.Status == KeyStatus.Published
      && AutohideEnabled
      && AutohideLeft > 0;
  }

  public AutohideState SyncAutohideState()
  {
    var status = GetAutohideState();
    AutohideStatus = status;
    return status;
  }

  public AutohideState GetAutohideState()
  {
    var keyStatus = Model.Status;
    switch(keyStatus)
    {
      case KeyStatus.Published:
        {
          if(!AutohideEnabled)
          {
            return AutohideState.Disabled;
          }
          if(AutohideLeft <= 0)
          {
            Trace.TraceError("Auto-hide state is in a transitional state (probably an error)");
            return AutohideState.Hiding;
          }
          if(IsCurrent() && Application.Current.MainWindow.IsActive)
          {
            return AutohideState.Paused;
          }
          return AutohideState.Running;
        }
      case KeyStatus.Hidden:
        {
          return AutohideState.Hidden;
        }
      case KeyStatus.Seeded:
      case KeyStatus.Unknown:
        {
          return AutohideState.Inactive;
        }
      default:
        {
          throw new InvalidOperationException(
            $"Unrecognized key status {keyStatus}");
        }
    }
  }

  public void TimerTick()
  {
    SyncAutohideState();
    if(AutohideStatus == AutohideState.Running)
    {
      AutohideLeft = _autohideLeft-1;
      if(AutohideLeft <= 0)
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
