using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
  /// <summary>
  /// Create a new <see cref="ChildKeyViewModel"/>
  /// </summary>
  /// <param name="vaultModel"></param>
  /// <param name="keyId"></param>
  internal ChildKeyViewModel(
    MasterVaultViewModel vaultModel,
    Guid keyId)
  {
    VaultModel = vaultModel;
    KeyId = keyId;
    TryLoadKeyCommand = new AsyncRelayCommand(
      () => TryLoadKey(),
      () => !KeyKnown && VaultModel.Owner.Owner.KeyServer.ServerAvailable);
    UpdateKeyKnown();
  }

  /// <summary>
  /// Try to load the raw key from the server, if it is still missing and the
  /// server is available
  /// </summary>
  public AsyncRelayCommand TryLoadKeyCommand { get; }

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
      SetProperty(ref _keyKnown, value);
    }
  }
  private bool _keyKnown;

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
      SetProperty(ref _keyInfo, value);
    }
  }
  private PassphraseKeyInfoFile? _keyInfo;

  /// <summary>
  /// Updates the value of <see cref="KeyKnown"/> to its correct value
  /// </summary>
  public void UpdateKeyKnown()
  {
    KeyKnown = VaultModel.HasChildKey(KeyId);
  }

  private async Task TryLoadKey()
  {
    UpdateKeyKnown();
    if(KeyKnown)
    {
      return;
    }
    var result = await VaultModel.TryRetrieveKey(KeyId);
    if(result == null)
    {
      MessageHost.ShowWarning(
        "The key server is not available",
        "Failed");
    }
    else
    {
      switch(result.Value)
      {
        case KeyPresence.Unavailable:
          MessageHost.ShowWarning(
            "The key is not available in the server",
            "Failed");
          return;
        case KeyPresence.Cloaked:
          MessageHost.ShowWarning(
            "The key is present but hidden in the server. Consider unhiding it.",
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
}
