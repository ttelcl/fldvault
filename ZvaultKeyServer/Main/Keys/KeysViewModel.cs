/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

using Microsoft.Win32;

using FldVault.Core.KeyResolution;
using FldVault.Core.Vaults;
using FldVault.Upi;
using FldVault.Upi.Implementation.Keys;

using ZvaultKeyServer.WpfUtilities;
using ZvaultKeyServer.Converters;

namespace ZvaultKeyServer.Main.Keys;

/// <summary>
/// ViewModel around <see cref="KeyStateStore"/>
/// </summary>
public class KeysViewModel: ViewModelBase
{
  private PasswordBox? _passwordBox;
  private string[] _timeoutValues;

  /// <summary>
  /// Create a new KeysViewModel
  /// </summary>
  public KeysViewModel(
    KeyStateStore model,
    IStatusMessage statusHost)
  {
    Model = model;
    StatusHost = statusHost;
    _passwordBackground = BrushCache.Default["#44444444"];
    Keys = new ObservableCollection<KeyViewModel>();
    ImportKeyCommand = new DelegateCommand(
      p => ImportKey());
    NewKeyCommand = new DelegateCommand(
      p => NewKey());
    TryUnlockCommand = new DelegateCommand(
      p => TryUnlock());
    ClearPasswordCommand = new DelegateCommand(
      p => ClearPassword());
    PublishCurrentKeyCommand = new DelegateCommand(
      p => CurrentKey?.SetCurrentKeyShowState(true));
    HideCurrentKeyCommand = new DelegateCommand(
      p => CurrentKey?.SetCurrentKeyShowState(false));
    ResetTimeoutCommand = new DelegateCommand(
      p => CurrentKey?.ResetTimer());
    DeleteCurrentKeyCommand = new DelegateCommand(
      p => DeleteCurrentKey(),
      p => CurrentKey != null);
    HideAllCommand = new DelegateCommand(p => HideAll());
    PasteZkeyFromClipboardCommand = new DelegateCommand(
      p => PasteZkeyFromClipboard(),
      p => ClipBoardMayHaveZkey);
    DefaultTimeout = 180;
    _timeoutValues = [
      "0:30",
      "1:00",
      "3:00",
      "5:00",
      "10:00",
      "30:00",
    ];
    // A bit ugly to depend on the actual type
    KeysView = (ListCollectionView)CollectionViewSource.GetDefaultView(Keys);
    Resort();
    SyncModel();
  }

  public KeyStateStore Model { get; }

  public IStatusMessage StatusHost { get; }

  public ICommand ImportKeyCommand { get; }

  public ICommand NewKeyCommand { get; }

  public ICommand TryUnlockCommand { get; }

  public ICommand ClearPasswordCommand { get; }

  public ICommand PublishCurrentKeyCommand { get; }

  public ICommand HideCurrentKeyCommand { get; }

  public ICommand ResetTimeoutCommand { get; }

  public ICommand DeleteCurrentKeyCommand { get; }

  public ICommand HideAllCommand { get; }

  public ICommand PasteZkeyFromClipboardCommand { get; }

  public ObservableCollection<KeyViewModel> Keys { get; }

  public /*ICollectionView*/ ListCollectionView KeysView { get; }

  public KeyViewModel? CurrentKey {
    get => _currentKey;
    set {
      if(SetNullableInstanceProperty(ref _currentKey, value))
      {
        RaisePropertyChanged(nameof(CurrentKeyVisible));
        if(_passwordBox != null)
        {
          _passwordBox.Clear();
        }
      }
    }
  }
  private KeyViewModel? _currentKey;

  public Visibility CurrentKeyVisible {
    get => _currentKey == null ? Visibility.Collapsed : Visibility.Visible;
  }

  public NewKeyViewModel? NewKeyPane {
    get => _newKeyPane;
    set {
      var oldPane = _newKeyPane;
      if(SetNullableInstanceProperty(ref _newKeyPane, value))
      {
        _newKeyPane?.ClearPasswords();
        oldPane?.Unbind();
        RaisePropertyChanged(nameof(NewKeyPaneVisible));
      }
    }
  }
  private NewKeyViewModel? _newKeyPane;

  public Visibility NewKeyPaneVisible {
    get => _newKeyPane == null ? Visibility.Collapsed : Visibility.Visible;
  }

  public void NewKey()
  {
    NewKeyPane = new NewKeyViewModel(this);
  }

  public int DefaultTimeout { get; }

  public IReadOnlyList<string> TimeoutValues { get => _timeoutValues; }

  /// <summary>
  /// Find the viewmodel for the key identified by the id.
  /// Existence state of the viewmodel is synchronized with the raw
  /// model by this method, creating or deleting the viewmodel if needed
  /// </summary>
  /// <param name="keyId">
  /// The ID of the key
  /// </param>
  /// <returns>
  /// Returns null if the key is not present in <see cref="Model"/> (and
  /// potentially deleting the viewmodel in that case.
  /// Returns the existing or newly created viewmodel otherwise.
  /// </returns>
  public KeyViewModel? FindKey(Guid keyId)
  {
    var rawKey = Model.FindKey(keyId);
    var kvm = Keys.FirstOrDefault(kvm => kvm.KeyId == keyId);
    if(kvm == null)
    {
      if(rawKey == null)
      {
        return null;
      }
      else
      {
        kvm = new KeyViewModel(this, rawKey);
        Keys.Add(kvm);
        return kvm;
      }
    }
    else
    {
      if(rawKey == null)
      {
        Keys.Remove(kvm);
      }
      return kvm;
    }
  }

  public void DeleteCurrentKey()
  {
    var currentKey = CurrentKey;
    if(currentKey != null)
    {
      if(MessageBox.Show(
        $"Are you sure you want to remove this key from this server?",
        "Confirmation",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning) == MessageBoxResult.Yes)
      {
        currentKey.UnloadKey();
        CurrentKey = null;
        Keys.Remove(currentKey);
        KeysView.Remove(currentKey);
        Model.RemoveKey(currentKey.KeyId);
        SyncModel();
      }
    }
  }

  public void SyncModel()
  {
    var newModels = new List<KeyViewModel>();
    var removedModels = new List<KeyViewModel>();
    var vmIds = Keys.Select(k => k.KeyId).ToHashSet();
    foreach(var state in Model.AllStates)
    {
      if(!vmIds.Contains(state.KeyId))
      {
        newModels.Add(new KeyViewModel(this, state));
      }
    }
    foreach(var vm in Keys)
    {
      if(!Model.HasKey(vm.KeyId))
      {
        removedModels.Remove(vm);
      }
    }
    foreach(var vm in removedModels)
    {
      Keys.Remove(vm);
    }
    foreach(var vm in newModels)
    {
      Keys.Add(vm);
    }
    var needSort = false;
    foreach(var vm in Keys)
    {
      needSort = vm.SyncModel() || needSort;
    }
    if(needSort)
    {
      // Forcing a re-sort when only the content of items change
      // turns out to be tricky. Replacing the sorter with a new
      // (but equivalent) one does the trick.
      Resort();
    }
  }

  public void Resort()
  {
    KeysView.CustomSort = new KeyViewModelComparer();
  }

  public void ImportKey()
  {
    StatusHost.StatusMessage = "Select file(s) to import the key of.";
    var dialog = new OpenFileDialog() {
      Filter = "All supported files (*.zkey, *.mvlt, *.zvlt, *.pass.key-info)|*.zkey;*.mvlt;*.zvlt;*.pass.key-info",
      Multiselect = true,
    };
    if(dialog.ShowDialog() == true)
    {
      Guid? guid = null;
      foreach(var fileName in dialog.FileNames)
      {
        var id = LinkFile(fileName);
        if(id.HasValue)
        {
          guid = id;
        }
      }
      SyncModel();
      if(guid.HasValue)
      {
        TrySelectKey(guid.Value);
      }
    }
    else
    {
      StatusHost.StatusMessage = "Operation canceled";
    }
  }

  public Guid? LinkFile(string fileName)
  {
    var pkif = PassphraseKeyInfoFile.TryFromFile(fileName);
    if(pkif == null)
    {
      Trace.TraceWarning($"Unsupported file {fileName}");
      StatusHost.StatusMessage = $"Unsupported file {Path.GetFileName(fileName)}";
      return null;
    }
    else
    {
      var state = Model.GetKey(pkif.KeyId);
      state.AssociateFile(fileName, true);
      StatusHost.StatusMessage = $"Updated key {pkif.KeyId}";
      Trace.TraceInformation($"Linked key {pkif.KeyId} to file {fileName}");
      return pkif.KeyId;
    }
  }

  public void TryUnlock()
  {
    StatusHost.StatusMessage = "";
    var key = CurrentKey;
    if(key == null)
    {
      StatusHost.StatusMessage = "Cannot unlock: No current key";
      return;
    }
    if(_passwordBox == null)
    {
      StatusHost.StatusMessage = "Cannot unlock: password box not bound (internal error)";
      return;
    }
    if(key.Status != KeyStatus.Seeded)
    {
      StatusHost.StatusMessage = "Cannot unlock: key is already unlocked or is not a password-based key";
      return;
    }
    // _passwordBox.SecurePassword returns a copy, so yes: dispose it after use!
    using(var secureString = _passwordBox.SecurePassword)
    {
      var unlocked = key.Model.TryResolveKey(secureString);
      if(unlocked)
      {
        StatusHost.StatusMessage = $"Successfully unlocked key {key.KeyId}";
        SetPasswordBackground(false);
        // No point in keeping the password around anymore
        _passwordBox.Clear();
        key.ResetTimer();
        SyncModel();
      }
      else
      {
        MessageBox.Show(
          "Passphrase incorrect.",
          "Error",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
        StatusHost.StatusMessage = $"Incorrect passphrase for {key.KeyId}";
        SetPasswordBackground(true);
      }
    }
  }

  public void SetPasswordBackground(bool error)
  {
    PasswordBackground = BrushCache.Default[error ? "#44663333" : "#44444444"];
  }

  public void ClearPassword()
  {
    _passwordBox?.Clear();
  }

  internal void BindPasswordBox(PasswordBox pwb)
  {
    _passwordBox = pwb;
  }

  public Brush PasswordBackground {
    get => _passwordBackground;
    set {
      if(SetInstanceProperty(ref _passwordBackground, value))
      {
      }
    }
  }
  private Brush _passwordBackground;

  public void TimerTick()
  {
    foreach(var keyview in Keys)
    {
      keyview.TimerTick();
    }
  }

  public void HideAll()
  {
    foreach(var keyview in Keys)
    {
      keyview.Model.HideKey = true;
    }
    SyncModel();
  }

  private void PasteZkeyFromClipboard()
  {
    if(!Clipboard.ContainsText())
    {
      MessageBox.Show(
        "Clipboard does not contain text",
        "Failed",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return;
    }
    var text = Clipboard.GetText();
    if(String.IsNullOrEmpty(text))
    {
      MessageBox.Show(
        "Clipboard does not contain text (or only empty text)",
        "Failed",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return;
    }
    var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
    AddZkeyText(lines);
  }

  private void AddZkeyText(IEnumerable<string> lines)
  {
    using var zkeyex = ZkeyEx.TryFromTransferLines(lines);
    if(zkeyex == null)
    {
      MessageBox.Show(
        "No valid Zkey descriptor found\n" +
        "(expecting lines to contain at least '<ZKEY>' and '</ZKEY>')",
        "Failed",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return;
    }
    var pkif = zkeyex.ToPassphraseKeyInfoFile();
    var state = Model.GetKey(pkif.KeyId);
    var seed = state.Seed;
    if(seed == null)
    {
      seed = new PassphraseKeySeed2(pkif);
      state.SetSeed(seed);
    }
    if(state.Status >= KeyStatus.Hidden)
    {
      MessageBox.Show(
        $"Key {pkif.KeyId} was already imported and unlocked",
        "Nothing to import",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
    }
    else if(zkeyex.Passphrase != null)
    {
      if(seed is not IParameterKeySeed<SecureString> pks)
      {
        MessageBox.Show(
          $"Key {pkif.KeyId} importing: Existing key descriptor is not compatible",
          "Incompatible key",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
      }
      else
      {
        var resolved = pks.TryResolve(zkeyex.Passphrase, state.KeyChain);
        if(resolved)
        {
          // This is expected to be the case. Stay silent.

          //MessageBox.Show(
          //  $"Key {pkif.KeyId} imported and unlocked",
          //  "Import and unlock key",
          //  MessageBoxButton.OK,
          //  MessageBoxImage.Information);
        }
        else
        {
          MessageBox.Show(
            $"Key {pkif.KeyId} imported, but passphrase was incorrect",
            "Import key - bad passphrase",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        }
      }
    }
    else
    {
      // leave as is if no passphrase was provided
      MessageBox.Show(
        $"Key {pkif.KeyId} imported.\n" + 
        "It is still locked, since no passphrase ('PASS:' line) was included.",
        "Success",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
    }
    SyncModel();
    TrySelectKey(pkif.KeyId);
  }

  public bool ClipBoardMayHaveZkey {
    get => _clipBoardMayHaveZkey;
    set {
      if(SetValueProperty(ref _clipBoardMayHaveZkey, value))
      {
      }
    }
  }
  private bool _clipBoardMayHaveZkey;

  public void ApplicationShowing(bool showing)
  {
    if(showing)
    {
      CheckClipboard();
    }
  }

  public void CheckClipboard()
  {
    var mayHaveZkey = false;
    if(Clipboard.ContainsText())
    {
      var text = Clipboard.GetText();
      var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
      mayHaveZkey =
        lines.Contains("<ZKEY>") &&
        lines.Contains("</ZKEY>");
    }
    ClipBoardMayHaveZkey = mayHaveZkey;
  }

  public void TrySelectKey(Guid keyId)
  {
    var kvm = Keys.FirstOrDefault(k => k.KeyId == keyId);
    if(kvm != null)
    {
      CurrentKey = kvm;
      //KeysView.MoveCurrentTo(kvm);
      //KeysView.Refresh();
    }
  }

}
