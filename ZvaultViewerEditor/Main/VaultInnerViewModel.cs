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
using System.Collections.ObjectModel;

namespace ZvaultViewerEditor.Main;

/// <summary>
/// Describes a Vault whose key is known
/// </summary>
public class VaultInnerViewModel: ViewModelBase
{
  /// <summary>
  /// Create a new VaultInnerViewModel
  /// </summary>
  public VaultInnerViewModel(
    VaultOuterViewModel outerModel)
  {
    OuterModel=outerModel;
    ReloadEntries();
  }

  public VaultOuterViewModel OuterModel { get; }

  public VaultFile Vault { get => OuterModel.Vault; }

  public KeyChain KeyChain { get => OuterModel.KeyChain; }

  public List<VaultEntry> Entries {
    get => _entries;
    private set {
      if(SetInstanceProperty(ref _entries, value))
      {
        RaisePropertyChanged(nameof(FileCount));
      }
    }
  }
  private List<VaultEntry> _entries = [];

  public ObservableCollection<VaultEntry> SelectedEntries { get; } = new();

  public int FileCount => Entries.Count;

  public void ReloadEntries()
  {
    using var cryptor = Vault.CreateCryptor(KeyChain);
    using var reader = new VaultFileReader(Vault, cryptor);
    var entries =
      Vault.FileElements()
      .Select(e => new FileElement(e))
      .Select(fe => VaultEntry.TryCreate(fe, reader))
      .Where(ve => ve!=null)
      .Cast<VaultEntry>()
      .ToList();
    entries.Sort(
      (ve1, ve2) => StringComparer.OrdinalIgnoreCase.Compare(ve1.FileName, ve2.FileName));
    Entries = entries;
    Trace.TraceInformation($"Loaded {FileCount} entries from {Vault.FileName}");
  }
}
