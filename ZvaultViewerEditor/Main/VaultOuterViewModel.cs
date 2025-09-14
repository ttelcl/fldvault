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
    RefreshKeyCommand = new DelegateCommand(
      p => UpdateKeyStatus(),
      p => true);
    // Create this, to match the initial state of IsVaultKeyKnown
    KeyEntryModel = new KeyEntryViewModel(this);
    UpdateKeyStatus();
  }

  public ICommand RefreshKeyCommand { get; }

  public IApplicationModel ApplicationModel { get; }

  /// <summary>
  /// The application's key chain (shared across modules)
  /// </summary>
  public KeyChain KeyChain { get => ApplicationModel.KeyChain; }

  public KeyServerService KeyServer { get => ApplicationModel.KeyServer; }

  public VaultFile Vault { get; }

  public string Label => Path.GetFileNameWithoutExtension(Vault.FileName);

  public string FileName => Path.GetFileName(Vault.FileName);

  public string FilePath => Path.GetDirectoryName(Vault.FileName) ?? "";

  public Guid KeyId => Vault.KeyId;

  public bool IsVaultKeyKnown {
    get => _vaultKeyKnown;
    private set {
      if(SetValueProperty(ref _vaultKeyKnown, value))
      {
        if(_vaultKeyKnown)
        {
          InnerModel = new VaultInnerViewModel(this);
          KeyEntryModel = null;
        }
        else
        {
          KeyEntryModel = new KeyEntryViewModel(this);
          InnerModel = null;
        }
      }
    }
  }
  private bool _vaultKeyKnown = false;

  public VaultInnerViewModel? InnerModel {
    get => _innerModel;
    private set {
      if(SetNullableInstanceProperty(ref _innerModel, value))
      {
      }
    }
  }
  private VaultInnerViewModel? _innerModel;

  public KeyEntryViewModel? KeyEntryModel {
    get => _keyEntryModel;
    private set {
      if(SetNullableInstanceProperty(ref _keyEntryModel, value))
      {
      }
    }
  }
  private KeyEntryViewModel? _keyEntryModel;


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
      //Trace.TraceInformation($"Delay lookup for test purposes");
      //await Task.Delay(5000);
      Trace.TraceInformation($"Looking up key {KeyId} in key server");
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
      CheckVaultKeyKnown();
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
