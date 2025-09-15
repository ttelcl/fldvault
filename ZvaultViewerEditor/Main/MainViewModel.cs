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
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using FldVault.Core.Crypto;
using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;
using FldVault.KeyServer;

using Microsoft.Win32;

using ZvaultViewerEditor.WpfUtilities;

namespace ZvaultViewerEditor.Main;

public class MainViewModel: ViewModelBase, IApplicationModel
{
  public MainViewModel(KeyChain keyChain)
  {
    KeyChain = keyChain;
    KeyServer = new KeyServerService();
    NoVault = new NoVaultViewModel(this);
    OpenVaultCommand = new DelegateCommand(
      p => OpenVault(),
      p => CurrentVault == null);
    NewVaultFromExistingKeyCommand = new DelegateCommand(
      p => NewVaultFromExistingKey(),
      p => CurrentVault == null);
    CloseVaultCommand = new DelegateCommand(
      p => CloseVault(),
      p => CurrentVault != null);
    _clearStatusTimer = new DispatcherTimer {
      Interval = TimeSpan.FromMilliseconds(1000),
      IsEnabled = false,
    };
    _clearStatusTimer.Tick += (s, e) => CheckStatusClearing();
  }

  public ICommand ExitCommand { get; } = new DelegateCommand(p => {
    var w = Application.Current.MainWindow;
    w?.Close();
  });

  public ICommand OpenVaultCommand { get; }

  public ICommand NewVaultFromExistingKeyCommand { get; }

  public ICommand CloseVaultCommand { get; }

  public KeyChain KeyChain { get; }

  public KeyServerService KeyServer { get; }

  public VaultOuterViewModel? CurrentVault {
    get => _currentVault;
    set {
      if(SetNullableInstanceProperty(ref _currentVault, value))
      {
        RaisePropertyChanged(nameof(HasVault));
      }
    }
  }
  private VaultOuterViewModel? _currentVault;

  /// <summary>
  /// The stub VM used when there is no vault loaded.
  /// Note that this exists also when there is a vault loaded,
  /// it is just not in use in that case.
  /// </summary>
  public NoVaultViewModel NoVault { get; }

  public bool HasVault => CurrentVault != null;

  public string StatusMessage {
    get => _statusMessage;
    set {
      if(SetInstanceProperty(ref _statusMessage, value))
      {
        if(String.IsNullOrEmpty(_statusMessage))
        {
          _clearStatusAfter = DateTimeOffset.MaxValue;
          _clearStatusTimer.Stop();
          Trace.TraceInformation($"Clearing StatusMessage");
        }
        else
        {
          _clearStatusAfter = DateTimeOffset.Now.AddSeconds(5);
          _clearStatusTimer.Start();
          Trace.TraceInformation($"Setting StatusMessage: '{_statusMessage}'");
        }
      }
    }
  }
  private string _statusMessage = "";

  public void CreateNewVaultBasedOn(string keyBearingFile)
  {
    var file = Path.GetFullPath(keyBearingFile);
    var shortName = Path.GetFileName(file);
    var folder = Path.GetDirectoryName(file);
    var pkif = PassphraseKeyInfoFile.TryFromFile(file);
    if(pkif == null)
    {
      Trace.TraceError(
        $"Retrieving PKIF from file failed: {file}");
      StatusMessage = $"Unable to retrieve key information from '{shortName}'";
    }
    else
    {
      var kss = KeyServer;
      if(kss.ServerAvailable)
      {
        var guid = kss.RegisterFileSync(file, KeyChain);
        if(guid.HasValue)
        {
          Trace.TraceInformation(
            $"Registered {shortName} with server. Key {guid} is available and loaded.");
        }
        else
        {
          Trace.TraceInformation(
            $"Registered {shortName} with server. Key is not available (yet).");
        }
      }
      var keyTag = pkif.KeyId.ToString().Substring(0, 8);
      var proposedName = $"new-vault.{keyTag}.zvlt";
      var dialog = new SaveFileDialog() {
        Filter = "Z-Vault files (*.zvlt)|*.zvlt",
        OverwritePrompt = true,
        CheckPathExists = true,
        DefaultExt = ".zvlt",
        FileName = proposedName,
        InitialDirectory = folder,
        Title = $"Create new vault with key of {shortName}"
      };
      if(dialog.ShowDialog() == true)
      {
        var _ = VaultFile.CreateEmpty(dialog.FileName, pkif);
        StatusMessage = $"Created {dialog.FileName}";
        Trace.TraceInformation($"Created new vault file {dialog.FileName}");
        TryOpenVault(dialog.FileName);
      }
    }
  }

  private DateTimeOffset _clearStatusAfter = DateTimeOffset.MaxValue;

  private DispatcherTimer _clearStatusTimer;

  private void CheckStatusClearing()
  {
    if(DateTimeOffset.Now > _clearStatusAfter)
    {
      StatusMessage = "";
    }
  }

  private void OpenVault()
  {
    if(CurrentVault != null)
    {
      MessageBox.Show(
        "Please Close the current vault before opening another one",
        "Close current vault",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
      return;
    }
    var dlg = new OpenFileDialog {
      DefaultExt = ".zvlt",
      Filter = "Vault Files|*.zvlt",
      ClientGuid = DialogGuids.VaultFileGuid,
    };
    if(dlg.ShowDialog() == true)
    {
      TryOpenVault(dlg.FileName);
    }
  }

  public bool TryOpenVault(
    string vaultFileName)
  {
    StatusMessage = "";
    try
    {
      var vaultFile = VaultFile.Open(vaultFileName);
      var vault = new VaultOuterViewModel(this, vaultFile);
      CurrentVault = vault;
      StatusMessage = $"Opened {Path.GetFileName(vaultFileName)}";
      return true;
    }
    catch(Exception ex)
    {
      MessageBox.Show(
        $"{ex.Message}",
        "Error opening vault file",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      StatusMessage = $"Error opening {Path.GetFileName(vaultFileName)}";
      return false;
    }
  }

  private void CloseVault()
  {
    if(CurrentVault == null)
    {
      return;
    }
    CurrentVault = null;
    StatusMessage = "";
  }

  private void NewVaultFromExistingKey()
  {
    StatusMessage = "";
    if(CurrentVault != null)
    {
      MessageBox.Show(
        "Please Close the current vault before opening another one",
        "Close current vault",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
      return;
    }
    var openDialog = new OpenFileDialog {
      Filter =
        "All key bearing files (*.zvlt;*.mvlt;*.zkey;*.pass.key-info)|*.zvlt;*.mvlt;*.zkey;*.pass.key-info" +
        "|Vault files (*.zvlt)|*.zvlt" +
        "|MonoVault files (*.mvlt)|*.mvlt" +
        "|Key descriptor files (*.zkey)|*.zkey" +
        "|Legacy password key info files (*.pass.key-info)|*.pass.key-info",
      ClientGuid = DialogGuids.KeyFileGuid,
      Title = "Step 1: pick a file to reuse the key of",
    };
    if(openDialog.ShowDialog() != true)
    {
      // canceled
      StatusMessage = "'New vault from existing key' operation canceled";
      return;
    }
    CreateNewVaultBasedOn(openDialog.FileName);
  }
}
