/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using FldVault.Core.Crypto;
using FldVault.Core.Zvlt2;

using ZvaultViewerEditor.WpfUtilities;
using FldVault.KeyServer;
using System.Threading;
using System.Windows.Threading;
using System.Windows.Input;

namespace ZvaultViewerEditor.Main;

/// <summary>
/// Models a vault file on the outer level, whether its key is known or not
/// </summary>
public class VaultOuterViewModel: ViewModelBase
{
  /// <summary>
  /// Create a new VaultOuterViewModel
  /// </summary>
  public VaultOuterViewModel(
    IApplicationModel applicationModel,
    VaultFile vault)
  {
    ApplicationModel = applicationModel;
    Vault = vault;
    Label = Path.GetFileNameWithoutExtension(vault.FileName);
    RefreshKeyCommand = new DelegateCommand(
      p => UpdateKeyStatus(),
      p => true);
  }

  public ICommand RefreshKeyCommand { get; }

  public IApplicationModel ApplicationModel { get; }

  /// <summary>
  /// The application's key chain (shared across modules)
  /// </summary>
  public KeyChain KeyChain { get => ApplicationModel.KeyChain; }

  public KeyServerService KeyServer { get => ApplicationModel.KeyServer; }

  public VaultFile Vault { get; }

  public string Label { get; }

  public Guid KeyId => Vault.KeyId;

  public bool IsVaultKeyKnown {
    get => _vaultKeyKnown;
    private set {
      if(SetValueProperty(ref _vaultKeyKnown, value))
      {
        CheckVaultKeyKnown();
      }
    }
  }
  private bool _vaultKeyKnown = false;

  public string KeyStatus {
    get => _keyStatus;
    set {
      if(SetInstanceProperty(ref _keyStatus, value))
      {
      }
    }
  }
  private string _keyStatus = "Not yet checked";

  public void UpdateKeyStatus() 
  {
    Task.Run(async () => await UpdateKeyStatusAsync());
  }
  
  public async Task UpdateKeyStatusAsync()
  {
    CheckVaultKeyKnown();
    if(IsVaultKeyKnown)
    {
      KeyStatus = "Unlocked";
    }
    else if(KeyServer.ServerAvailable)
    {
      try
      {
        var keyPresence = await KeyServer.LookupKeyAsync(
          KeyId,
          KeyChain,
          CancellationToken.None);
        switch(keyPresence)
        {
          case KeyPresence.Present:
            KeyStatus = "Unlocked";
            ApplicationModel.StatusMessage = "Key retrieved from server";
            break;
          case KeyPresence.Unavailable:
            KeyStatus = "Key not available in server, registering file";
            await KeyServer.RegisterFileAsync(
              Vault.FileName,
              KeyChain,
              CancellationToken.None);
            break;
          case KeyPresence.Cloaked:
            KeyStatus = "Key is available in server, but currently hidden";
            break;
        }
      }
      catch(Exception ex)
      {
        KeyStatus = "Error looking up key";
        ApplicationModel.StatusMessage = $"Error looking up key: {ex.Message}";
      }
    }
    else
    {
      KeyStatus = "Key not yet provided. Key server not available";
    }
  }

  private void CheckVaultKeyKnown()
  {
    IsVaultKeyKnown = Vault != null && KeyChain.ContainsKey(Vault.KeyId);
  }

}
