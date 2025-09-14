/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using FldVault.Core.Crypto;

using ZvaultViewerEditor.WpfUtilities;

namespace ZvaultViewerEditor.Main;

/// <summary>
/// Handles supplying the vault key to the key chain
/// </summary>
public class KeyEntryViewModel: ViewModelBase
{
  public KeyEntryViewModel(
    VaultOuterViewModel owner)
  {
    Owner = owner;
  }

  public VaultOuterViewModel Owner { get; }

  public KeyChain KeyChain => Owner.KeyChain;

  public ICommand RefreshKeyCommand => Owner.RefreshKeyCommand;



}
