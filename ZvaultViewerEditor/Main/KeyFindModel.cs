/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using FldVault.Core.Crypto;
using FldVault.Core.Zvlt2;

using ZvaultViewerEditor.WpfUtilities;
using FldVault.Core.Vaults;

namespace ZvaultViewerEditor.Main;

/// <summary>
/// Data backing the situation where the key is missing, to help
/// find it (either by passphrase entry or by key server lookup)
/// </summary>
public class KeyFindModel: ViewModelBase
{
  /// <summary>
  /// Create a new KeyFindModel
  /// </summary>
  /// <param name="applicationModel">
  /// Shared application services
  /// </param>
  /// <param name="target">
  /// The vault for which to find the key.
  /// </param>
  public KeyFindModel(
    IApplicationModel applicationModel,
    VaultFile target)
  {
    ApplicationModel = applicationModel;
    Target = target;
    CheckDone();
    KeyInfo = Target.GetPassphraseInfo();
  }

  public IApplicationModel ApplicationModel { get; }

  /// <summary>
  /// The key chain to receive the key
  /// </summary>
  public KeyChain KeyChain { get => ApplicationModel.KeyChain; }

  /// <summary>
  /// The vault for which to find the key
  /// </summary>
  public VaultFile Target { get; }

  public Guid KeyId => Target.KeyId;

  /// <summary>
  /// The passphrase key information for the vault. It may be null,
  /// which indicates that the key cannot be found through a passphrase,
  /// and a key server is required.
  /// </summary>
  public PassphraseKeyInfoFile? KeyInfo { get; }

  /// <summary>
  /// Whether or not the vault has key information. If false, there is
  /// no point in asking the user for a passphrase.
  /// </summary>
  public bool HasKeyInfo => KeyInfo != null;

  /// <summary>
  /// True if the key is now in the keychain, and this component
  /// has completed its purpose.
  /// </summary>
  public bool Done {
    get => _done;
    private set {
      if(SetValueProperty(ref _done, value))
      {
      }
    }
  }
  private bool _done = false;

  public void CheckDone()
  {
    Done = KeyChain.ContainsKey(Target.KeyId);
  }
}
