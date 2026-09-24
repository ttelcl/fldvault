using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FldVault.Core.Crypto;
using FldVault.Core.Vaults;
using FldVault.KeyServer;

using KeyLoader.UserMessages;

namespace KeyLoader.Main.MasterVaults;

/// <summary>
/// ViewModel for a single child key inside a <see cref="MasterVaultViewModel"/>.
/// The only thing guaranteed to exist is the child key ID. Both the
/// raw key and the key info may be missing. The raw key itself is not actually stored
/// here, only a flag indicating its availability.
/// </summary>
public class ChildKeyViewModel: ObservableObject
{
  private readonly KeyChain _childKeyChain;

  /// <summary>
  /// Create a new <see cref="ChildKeyViewModel"/>
  /// </summary>
  /// <param name="childKeyChain"></param>
  /// <param name="vaultModel"></param>
  /// <param name="keyId"></param>
  internal ChildKeyViewModel(
    KeyChain childKeyChain,
    MasterVaultViewModel vaultModel,
    Guid keyId)
  {
    _childKeyChain = childKeyChain;
    VaultModel = vaultModel;
    KeyId = keyId;
    TryLoadKeyCommand = new AsyncRelayCommand(
      () => TryLoadKey(),
      () => (!KeyKnown || !KeyInfoKnown) && VaultModel.Owner.IsEditing && VaultModel.Owner.Owner.KeyServer.ServerAvailable);
    TryPushKeyCommand = new AsyncRelayCommand(
      TryPushKey,
      () => (KeyKnown || KeyInfoKnown) && VaultModel.Owner.Owner.KeyServer.ServerAvailable);
    CopyPrefixCommand = new RelayCommand(CopyKeyPrefix);
    CopyKeyIdCommand = new RelayCommand(CopyKeyId);
    RemoveKeyInfoCommand = new RelayCommand(
      RemoveKeyInfo,
      () => KeyInfo != null);
    DeleteKeyCommand = new RelayCommand(DeleteKey);
    UpdateKeyKnown();
  }

  /// <summary>
  /// Try to load the raw key and key info from the server, if it is still missing and the
  /// server is available
  /// </summary>
  public AsyncRelayCommand TryLoadKeyCommand { get; }

  /// <summary>
  /// Try to push the raw key and key info object to the server
  /// </summary>
  public AsyncRelayCommand TryPushKeyCommand { get; }

  /// <summary>
  /// Copy the key prefix to the clipboard
  /// </summary>
  public RelayCommand CopyPrefixCommand { get; }

  /// <summary>
  /// Copy the full key ID to the clipboard
  /// </summary>
  public RelayCommand CopyKeyIdCommand { get; }

  /// <summary>
  /// Remove the passphrase link for the key
  /// </summary>
  public RelayCommand RemoveKeyInfoCommand { get; }

  /// <summary>
  /// Delete this key record from <see cref="VaultModel"/>.
  /// Note that the raw key stays in the keychain, so it can be re-added
  /// without reentering the passphrase.
  /// </summary>
  public RelayCommand DeleteKeyCommand { get; }

  /// <summary>
  /// The owning <see cref="MasterVaultViewModel"/> that this child
  /// is part of
  /// </summary>
  public MasterVaultViewModel VaultModel { get; }

  /// <summary>
  /// Get the message host (implemented by the main VM)
  /// </summary>
  public IMessageHost MessageHost => VaultModel.Owner.MessageHost;

  /// <summary>
  /// The ID of the key that this object represents.
  /// </summary>
  public Guid KeyId { get; }

  /// <summary>
  /// Whether or not the raw key is known. This is a cached copy,
  /// set by calling <see cref="UpdateKeyKnown"/>.
  /// </summary>
  public bool KeyKnown {
    get => _keyKnown;
    private set {
      if(SetProperty(ref _keyKnown, value))
      {
        TryLoadKeyCommand.NotifyCanExecuteChanged();
        TryPushKeyCommand.NotifyCanExecuteChanged();
        KeyIcon = _keyKnown ? "LockOpenCheck" : "LockAlert";
        VaultModel.Owner.MarkModified(true);
      }
    }
  }
  private bool _keyKnown;

  /// <summary>
  /// An icon indicating key presence or absence
  /// </summary>
  public string KeyIcon {
    get => _keyIcon;
    set {
      SetProperty(ref _keyIcon, value);
    }
  }
  private string _keyIcon = "LockAlert";

  /// <summary>
  /// Get or set the key info (passphrase link) for this key
  /// </summary>
  public PassphraseKeyInfoFile? KeyInfo {
    get => _keyInfo;
    internal set {
      if(value != null && value.KeyId != KeyId)
      {
        throw new InvalidOperationException(
          "The key ids of the passphrase link and this key viewmodel do not match");
      }
      var oldInfo = _keyInfo;
      if(SetProperty(ref _keyInfo, value))
      {
        KeyInfoKnown = _keyInfo != null;
        RemoveKeyInfoCommand.NotifyCanExecuteChanged();
        // determine if there were actual changes before declaring a modification
        var realChange =
           _keyInfo?.SaltBase64 != oldInfo?.SaltBase64
           || _keyInfo?.KeyId != oldInfo?.KeyId
           || _keyInfo?.UtcKeyStamp != oldInfo?.UtcKeyStamp;
        if(realChange)
        {
          VaultModel.Owner.MarkModified(true);
        }
      }
    }
  }
  private PassphraseKeyInfoFile? _keyInfo;

  /// <summary>
  /// Whether or not the key info is available (which allows unlocking
  /// a locked key with a passphrase).
  /// </summary>
  public bool KeyInfoKnown {
    get => _keyInfoKnown;
    private set {
      if(SetProperty(ref _keyInfoKnown, value))
      {
        KeyInfoIcon = _keyInfoKnown ? "KeyboardOutline" : "KeyboardOffOutline";
        TryPushKeyCommand.NotifyCanExecuteChanged();
        TryLoadKeyCommand.NotifyCanExecuteChanged();
      }
    }
  }
  private bool _keyInfoKnown;

  /// <summary>
  /// The icon to represent <see cref="KeyInfoKnown"/> (in PackIconMaterial)
  /// </summary>
  public string KeyInfoIcon { 
    get => _keyInfoIcon;
    private set {
      SetProperty(ref _keyInfoIcon, value);
    }
  }
  private string _keyInfoIcon = "KeyboardOffOutline";

  /// <summary>
  /// Updates the value of <see cref="KeyKnown"/> to its correct value
  /// </summary>
  public void UpdateKeyKnown()
  {
    KeyKnown = VaultModel.HasChildKey(KeyId);
  }

  private void CopyKeyPrefix()
  {
    var text = KeyId.ToString().Substring(0, 8);
    Clipboard.SetText(text);
  }

  private void CopyKeyId()
  {
    var text = KeyId.ToString();
    Clipboard.SetText(text);
  }

  private void RemoveKeyInfo()
  {
    KeyInfo = null;
  }

  private void DeleteKey()
  {
    // Hard delete, no questions asked
    VaultModel.DeleteKey(KeyId);
  }

  private async Task TryPushKey()
  {
    UpdateKeyKnown();
    if(!KeyKnown && !KeyInfoKnown)
    {
      // nothing to upload - command should not have been enabled
      return;
    }
    var vaultModel = VaultModel;
    var tabVm = vaultModel.Owner;
    var mainVm = tabVm.Owner;
    var serverWidget = mainVm.ServerWidget;
    var server = serverWidget.Server;
    var messageHost = tabVm.MessageHost;
    if(server.ServerAvailable)
    {
      if(KeyInfo != null)
      {
        var result = await server.UploadKeyInfosAsync(
          [KeyInfo], serverWidget.AppCancelationToken);
        switch(result)
        {
          case KeyServerMessages.KeyUploadedCode:
            // success, continue
            break;
          case KeyServerMessages.NoServer:
            MessageHost.ShowError(
              "The key server is not responding",
              "Key server down");
            return;
          case KeyServerMessages.Unrecognized:
            MessageHost.ShowWarning(
              "Unable to upload key descriptor to server. Please update your key server. Functionality is limited.",
              "Incompatible key server detected");
            return;
          default:
            // should not happen
            MessageHost.ShowError(
              $"Unexpected server response 0x{result:X8}",
              "Internal error");
            return;
        }
      }
      if(KeyKnown)
      {
        var result = await server.UploadKeysAsync(
          _childKeyChain, [KeyId], serverWidget.AppCancelationToken);
        switch(result)
        {
          case KeyServerMessages.KeyUploadedCode:
            // success, continue
            break;
          case KeyServerMessages.NoServer:
            MessageHost.ShowError(
              "The key server is not responding",
              "Key server down");
            return;
          case KeyServerMessages.Unrecognized:
            MessageHost.ShowWarning(
              "Unable to upload key descriptor to server. Please update your key server. Functionality is limited.",
              "Incompatible key server detected");
            return;
          default:
            // should not happen
            MessageHost.ShowError(
              $"Unexpected server response 0x{result:X8}",
              "Internal error");
            return;
        }
      }
    }
    else
    {
      MessageHost.ShowError(
        "The Key Server is not running",
        "No key server found");
    }
  }

  /// <summary>
  /// Try to load key information from the server into this key in this master file
  /// </summary>
  /// <returns></returns>
  private async Task TryLoadKey()
  {
    UpdateKeyKnown();
    if(KeyKnown && KeyInfoKnown)
    {
      return;
    }
    if(!VaultModel.Owner.IsEditing)
    {
      Trace.TraceWarning(
        "Ignoring request to load key while in read only mode");
      return;
    }
    var result = await TryRetrieveKey();
    if(result == null)
    {
      if(!MessageHost.CurrentMessageSeverity().HasValue)
      {
        MessageHost.ShowWarning(
          "The key server is not available",
          "Failed");
      }
    }
    else
    {
      switch(result.Value)
      {
        case KeyPresence.Unavailable:
          if(!MessageHost.CurrentMessageSeverity().HasValue)
          {
            MessageHost.ShowWarning(
              "The key is not available in the server",
              "Failed");
          }
          return;
        case KeyPresence.Cloaked:
          MessageHost.ShowWarning(
            "The key is present but hidden in the server. Consider unhiding it and trying again.",
            "Failed");
          return;
        case KeyPresence.Present:
          // success
          UpdateKeyKnown();
          return;
        default:
          MessageHost.ShowError(
            "Internal error. Unrecognized server response.",
            "Failed");
          return;
      }
    }
  }

  /// <summary>
  /// Asynchronously refresh the raw key value and key info from the key server, if 
  /// the key server is available. 
  /// </summary>
  /// <returns></returns>
  public async Task<KeyPresence?> TryRetrieveKey()
  {
    var vaultModel = VaultModel;
    var tabVm = vaultModel.Owner;
    var mainVm = tabVm.Owner;
    var serverWidget = mainVm.ServerWidget;
    var server = serverWidget.Server;
    var messageHost = tabVm.MessageHost;
    if(server.ServerAvailable)
    {
      var result = await server.LookupKeyAsync(KeyId, _childKeyChain, serverWidget.AppCancelationToken);
      if(result == KeyPresence.Present)
      {
        if(KeyInfo == null)
        {
          // Also try to fetch key info
          var pkif = await server.LookupKeyInfoAsync(KeyId, serverWidget.AppCancelationToken);
          if(pkif != null)
          {
            KeyInfo = pkif;
          }
        }
      }
      else if(result == KeyPresence.Unavailable) // and thus not Cloaked
      {
        if(KeyInfo != null)
        {
          // upload key info, to avoid needlessly announcing a ghost key to the server
          var response = await server.UploadKeyInfosAsync([KeyInfo], serverWidget.AppCancelationToken);
          switch(response)
          {
            case KeyServerMessages.KeyUploadCode:
              // everything is fine
              break;
            case KeyServerMessages.NoServer:
              // This should not happen - we could communicate before
              messageHost.ShowError(
                "Error communicating with the key server");
              return null;
            case KeyServerMessages.Unrecognized:
              messageHost.ShowWarning(
                "Unable to upload key descriptor to server. Please update your key server. Functionality is limited.",
                "Incompatible key server detected");
              break;
          }
        }
        else // KeyInfo == null
        {
          // the key is not in the server - but it may still have the key info, which we do not have yet here
          KeyInfo = await server.LookupKeyInfoAsync(KeyId, serverWidget.AppCancelationToken);
        }
      }
      UpdateKeyKnown();
      return result;
    }
    return null;
  }


}
