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
using ZvaultViewerEditor.WpfUtilities;
using FldVault.Core.Zvlt2;

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
  }

  public IApplicationModel ApplicationModel { get; }

  /// <summary>
  /// The application's key chain (shared across modules)
  /// </summary>
  public KeyChain KeyChain { get => ApplicationModel.KeyChain; }

  public VaultFile Vault { get; }

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

  private void CheckVaultKeyKnown()
  {
    IsVaultKeyKnown = Vault != null && KeyChain.ContainsKey(Vault.KeyId);
  }

}
