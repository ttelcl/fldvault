/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

using FldVault.Core.Crypto;

using ZvaultViewerEditor.WpfUtilities;

namespace ZvaultViewerEditor.Main;

/// <summary>
/// Handles supplying the vault key to the key chain
/// </summary>
public class KeyEntryViewModel: ViewModelBase
{
  private PasswordBox? _passwordBox;

  public KeyEntryViewModel(
    VaultOuterViewModel owner)
  {
    Owner = owner;
    SubmitCommand = new DelegateCommand(
      p => SubmitKey());
  }

  public VaultOuterViewModel Owner { get; }

  public KeyChain KeyChain => Owner.KeyChain;

  public ICommand RefreshKeyCommand => Owner.RefreshKeyCommand;

  public ICommand CloseCommand => Owner.ApplicationModel.CloseVaultCommand;

  public IApplicationModel ApplicationModel => Owner.ApplicationModel;

  public ICommand SubmitCommand { get; }

  private void SubmitKey()
  {
    var submitted = TrySubmitKey();
    if(submitted)
    {
      Owner.CheckVaultKeyKnown();
    }
  }

  private bool TrySubmitKey()
  {
    if(KeyChain.ContainsKey(Owner.KeyId))
    {
      ApplicationModel.StatusMessage = "Key was already succesfully loaded";
      return true;
    }
    if(_passwordBox == null)
    {
      Trace.TraceError("_passwordBox unexpectedly is NULL");
      ApplicationModel.StatusMessage = "Internal Error: password disconnected";
      return false;
    }
    ApplicationModel.StatusMessage = "";
    var pkif = Owner.Vault.GetPassphraseInfo();
    if(pkif == null)
    {
      Trace.TraceError("Vault file does not have a key descriptor");
      ApplicationModel.StatusMessage = "This vault cannot be unlocked with a password";
      // TODO: anticipate this situation and disable the key entry UI
      return false;
    }
    using var ss = _passwordBox.SecurePassword;
    if(ss.Length == 0)
    {
      // fail silently
      Trace.TraceWarning("_passwordBox is empty, ignoring Submit button");
      return false;
    }
    using var key = PassphraseKey.TryPassphrase(ss, pkif);
    if(key == null)
    {
      Trace.TraceError("Passphrase was not correct");
      ApplicationModel.StatusMessage = "That passphrase is not correct.";
      return false;
    }
    else
    {
      Trace.TraceInformation("Passphrase correct - key unlocked");
      ApplicationModel.StatusMessage = "That passphrase successfully unlocked this vault";
      KeyChain.PutCopy(key);
      return true;
    }
  }

  internal void BindPasswordBox(PasswordBox pwb)
  {
    _passwordBox = pwb;
  }

  internal void ClearPasswords()
  {
    _passwordBox?.Clear();
  }

  internal void UnbindPasswordBox()
  {
    ClearPasswords();
    _passwordBox = null;
  }

}
