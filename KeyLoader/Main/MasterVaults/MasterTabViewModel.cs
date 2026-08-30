using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using FldVault.Core.Crypto;
using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;

using KeyLoader.Main.TaskTab;

namespace KeyLoader.Main.MasterVaults;

/// <summary>
/// A tab for using, editing and creating a master key file
/// </summary>
public class MasterTabViewModel: TaskTabBaseViewModel
{
  private bool _disposed; // local to this class (not the superclass or child classes)
  private readonly KeyChain _masterKeyChain;
  private readonly KeyChain _childKeyChain;

  /// <summary>
  /// Create a new instance of <see cref="MasterTabViewModel"/>.
  /// </summary>
  /// <param name="owner">
  /// The owning <see cref="MainViewModel"/>
  /// </param>
  /// <param name="fileName">
  /// The name of the file to open or create. This must be known before creating
  /// this object. The file name must be valid, but does not yet need to exist.
  /// </param>
  /// <param name="modified">
  /// True if the data should be considered 'modified' and in need of saving
  /// </param>
  public MasterTabViewModel(
    MainViewModel owner,
    string? fileName = null,
    bool modified = false)
    : base(owner, TitleFromFileName(fileName), modified)
  {
    _masterKeyChain = new KeyChain();
    _childKeyChain = new KeyChain();
  }

  /// <summary>
  /// Get or set the file name associated with this tab
  /// </summary>
  public string? FileName {
    get => _fileName;
    set {
      if(SetProperty(ref _fileName, value))
      {
        Title = TitleFromFileName(_fileName);
        UpdateFileExists();
      }
    }
  }
  private string? _fileName;

  /// <summary>
  /// The master key info record (null if not yet known, which happens
  /// in the process of creating a new vault)
  /// </summary>
  public PassphraseKeyInfoFile? MasterKey {
    get => _masterKey;
    set {
      if(value != null && _masterKey != null && value.KeyId != _masterKey.KeyId)
      {
        throw new InvalidOperationException(
          "Once set, the master key cannot be changed");
      }
      if(SetProperty(ref _masterKey, value))
      {
        UpdateMasterKeyLoaded();
        // Do not update state here to avoid double updates. Instead, have the caller
        // do so.
      }
    }
  }
  private PassphraseKeyInfoFile? _masterKey;

  /// <summary>
  /// True if the master key has been set and loaded. Once set to true this
  /// is expected to stay true.
  /// </summary>
  public bool MasterKeyLoaded {
    get => _masterKeyLoaded;
    private set {
      if(SetProperty(ref _masterKeyLoaded, value))
      {
        // Do not update state here to avoid double updates. Instead, have the caller
        // do so.
      }
    }
  }
  private bool _masterKeyLoaded;

  private void UpdateMasterKeyLoaded()
  {
    MasterKeyLoaded = MasterKey != null && _masterKeyChain.ContainsKey(MasterKey.KeyId);
  }

  /// <summary>
  /// Start the process of creating a new key for a new vault file by generating a new
  /// master key using the given <paramref name="passphrase"/>.
  /// </summary>
  /// <param name="passphrase"></param>
  private void SetNewVaultKey(SecureString passphrase)
  {
    ExpectStates(MasterTabState.CreatingKey);
    using(var ppk = PassphraseKey.FromSecureString(passphrase))
    {
      _masterKeyChain.PutCopy(ppk);
      var pkif = new PassphraseKeyInfoFile(ppk);
      MasterKey = pkif;
    }
    UpdateState();
  }

  /// <summary>
  /// Confirm the previously loaded master key for the new master vault.
  /// Upon failure: show a message and return false.
  /// Upon success: create the new (empty) vault file and return true
  /// </summary>
  /// <param name="key"></param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException"></exception>
  private bool ConfirmNewVaultKey(SecureString key)
  {
    ExpectStates(MasterTabState.ConfirmingKey);
    if(MasterKey == null || !_masterKeyChain.ContainsKey(MasterKey.KeyId) || String.IsNullOrEmpty(FileName))
    {
      throw new InvalidOperationException(
        "Cannot confirm a master key that hasn't been loaded yet");
    }
    using(var ppk = PassphraseKey.TryPassphrase(key, MasterKey))
    {
      if(ppk == null)
      {
        MessageBox.Show(
          "Passphrase did not match",
          "Wrong passphrase",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
        return false;
      }
    }
    VaultFile.WriteMasterKeyFile(FileName, MasterKey, [], _childKeyChain, _masterKeyChain);
    UpdateFileExists();
    UpdateState();
    return FileExists;
  }

  /// <summary>
  /// True if a file is defined and that file exists.
  /// Updated explicitly via <see cref="UpdateFileExists"/>, or
  /// implicitly by changing <see cref="FileName"/>.
  /// </summary>
  public bool FileExists {
    get => _fileExistsState;
    private set {
      if(SetProperty(ref _fileExistsState, value))
      {
        UpdateState();
      }
    }
  }
  private bool _fileExistsState = false;

  /// <summary>
  /// Updates the state of <see cref="FileExists"/> to match
  /// the existence of the file named by <see cref="FileName"/>.
  /// </summary>
  public void UpdateFileExists()
  {
    FileExists = !String.IsNullOrEmpty(FileName) && File.Exists(FileName);
  }

  /// <summary>
  /// The state of this tab, determining what to show in the UI
  /// </summary>
  public MasterTabState State {
    get => _state;
    private set {
      SetProperty(ref _state, value);
    }
  }
  private MasterTabState _state;

  /// <summary>
  /// Update the state to the automatically calculated value
  /// </summary>
  public void UpdateState()
  {
    State = CalculateState();
  }

  private void ExpectStates(params MasterTabState[] expectedStates)
  {
    if(!expectedStates.Any(expected => State == expected))
    {
      var list = String.Join(", ", expectedStates);
      State = MasterTabState.Panic;
      throw new InvalidOperationException(
        $"Unexpected state. Got '{State}' while expecting one of: {expectedStates}");
    }
  }

  private MasterTabState CalculateState()
  {
    if(!File.Exists(FileName))
    {
      if(MasterKey == null)
      {
        // creating a new key, for a new file
        return MasterTabState.CreatingKey;
      }
      if(!MasterKeyLoaded)
      {
        // transient state (should not happen if key is loaded before MasterKey is set)
        return MasterTabState.CreatingKey;
      }
      return MasterTabState.ConfirmingKey;
    }
    if(MasterKey == null)
    {
      Trace.TraceError(
        "Not expecting Master Key Info to be missing once the file is known");
      return MasterTabState.Panic;
    }
    if(!MasterKeyLoaded)
    {
      return MasterTabState.AwaitingKey;
    }
    if(Modified || State == MasterTabState.ConfirmingKey || State == MasterTabState.Editing)
    {
      // Already in modified state or just completed new file creation: enter edit mode
      return MasterTabState.Editing;
    }
    if(State == MasterTabState.Closed)
    {
      return MasterTabState.Closed;
    }
    return MasterTabState.UsingMaster;
  }

  /// <summary>
  /// Try to save, updating <see cref="TaskTabBaseViewModel.Modified"/> on success
  /// </summary>
  protected override void TrySave()
  {
    if(Modified)
    {
      if(String.IsNullOrEmpty(FileName))
      {
        Trace.TraceError(
          "Cannot save if there is no file name known");
        return;
      }

      // TODO: actually save...
    }
  }

  /// <summary>
  /// Clean up
  /// </summary>
  /// <param name="disposing"></param>
  protected override void Dispose(bool disposing)
  {
    if(!_disposed)
    {
      _disposed=true;
      if(disposing)
      {
        _masterKeyChain.Dispose();
        _childKeyChain.Dispose();
      }
    }
    base.Dispose(disposing);
  }

  private static string TitleFromFileName(string? fileName)
  {
    if(String.IsNullOrEmpty(fileName))
    {
      return "Untitled";
    }
    return Path.GetFileNameWithoutExtension(fileName);
  }
}
