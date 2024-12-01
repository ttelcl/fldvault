/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using FldVault.Core.Crypto;
using FldVault.Core.Zvlt2;

using ZvaultViewerEditor.WpfUtilities;

namespace ZvaultViewerEditor.Main;

public class MainViewModel: ViewModelBase
{
  public MainViewModel(KeyChain keyChain)
  {
    KeyChain = keyChain;
  }

  public KeyChain KeyChain { get; }

  public VaultFile? CurrentVault {
    get => _currentVault;
    set {
      if(SetNullableInstanceProperty(ref _currentVault, value))
      {
        RaisePropertyChanged(nameof(HasVault));
      }
    }
  }
  private VaultFile? _currentVault;

  public bool HasVault => CurrentVault != null;

  public bool VaultKeyKnown {
    get => _vaultKeyKnown;
    private set {
      if(SetValueProperty(ref _vaultKeyKnown, value))
      {
        CheckVaultKeyKnown();
      }
    }
  }
  private bool _vaultKeyKnown = false;

  private void CheckVaultKeyKnown()
  {
    VaultKeyKnown = CurrentVault != null && KeyChain.ContainsKey(CurrentVault.KeyId);
  }

  public ICommand ExitCommand { get; } = new DelegateCommand(p => {
    var w = Application.Current.MainWindow;
    w?.Close();
  });
  public string StatusMessage {
    get => _statusMessage;
    set {
      if(SetInstanceProperty(ref _statusMessage, value))
      {
      }
    }
  }
  private string _statusMessage = "Welcome";

}
