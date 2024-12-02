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

using Microsoft.Win32;

using FldVault.Core.Crypto;
using FldVault.Core.Zvlt2;

using ZvaultViewerEditor.WpfUtilities;

namespace ZvaultViewerEditor.Main;

public class MainViewModel: ViewModelBase, IApplicationModel
{
  public MainViewModel(KeyChain keyChain)
  {
    KeyChain = keyChain;
    OpenVaultCommand = new DelegateCommand(
      p => OpenVault(),
      p => CurrentVault == null);
    CloseVaultCommand = new DelegateCommand(
      p => CloseVault(),
      p => CurrentVault != null);
  }

  public ICommand ExitCommand { get; } = new DelegateCommand(p => {
    var w = Application.Current.MainWindow;
    w?.Close();
  });

  public ICommand OpenVaultCommand { get; }

  public ICommand CloseVaultCommand { get; }

  public KeyChain KeyChain { get; }

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

  public bool HasVault => CurrentVault != null;

  public string StatusMessage {
    get => _statusMessage;
    set {
      if(SetInstanceProperty(ref _statusMessage, value))
      {
      }
    }
  }
  private string _statusMessage = "";

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
      Filter = "Vault Files|*.zvlt"
    };
    if(dlg.ShowDialog() == true)
    {
      TryOpenVault(dlg.FileName);
    }
  }

  private bool TryOpenVault(
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
    MessageBox.Show("Closing a vault not yet fully implemented",
      "Not Implemented",
      MessageBoxButton.OK,
      MessageBoxImage.Information);
    StatusMessage = "";
  }
}
