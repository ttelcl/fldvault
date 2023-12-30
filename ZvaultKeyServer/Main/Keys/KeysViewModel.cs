/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

using FldVault.Core.Vaults;
using FldVault.Upi.Implementation.Keys;

using Microsoft.Win32;

using ZvaultKeyServer.WpfUtilities;

namespace ZvaultKeyServer.Main.Keys;

/// <summary>
/// ViewModel around <see cref="KeyStateStore"/>
/// </summary>
public class KeysViewModel: ViewModelBase
{

  /// <summary>
  /// Create a new KeysViewModel
  /// </summary>
  public KeysViewModel(
    KeyStateStore model,
    IStatusMessage statusHost)
  {
    Model = model;
    StatusHost = statusHost;
    Keys = new ObservableCollection<KeyViewModel>();
    ImportKeyCommand = new DelegateCommand(p => ImportKey());
    NewKeyCommand = new DelegateCommand(p => NewKey());
    NewVaultCommand = new DelegateCommand(p => NewVault());
    // A bit ugly to depend on the actual type
    KeysView = (ListCollectionView)CollectionViewSource.GetDefaultView(Keys);
    KeysView.CustomSort = new KeyViewModelComparer();
    SyncModel();
  }

  public KeyStateStore Model { get; }

  public IStatusMessage StatusHost { get; }

  public ICommand ImportKeyCommand { get; }

  public ICommand NewKeyCommand { get; }

  public ICommand NewVaultCommand { get; }

  public ObservableCollection<KeyViewModel> Keys { get; }

  public /*ICollectionView*/ ListCollectionView KeysView { get; }

  public KeyViewModel? CurrentKey {
    get => _currentKey;
    set {
      if(SetNullableInstanceProperty(ref _currentKey, value))
      {
      }
    }
  }
  private KeyViewModel? _currentKey;

  /// <summary>
  /// Find the viewmodel for the key identified by the id.
  /// Existence state of the viewmodel is synchronized with the raw
  /// model by this method, creating or deleting the viewmodel if needed
  /// </summary>
  /// <param name="keyId">
  /// The ID of the key
  /// </param>
  /// <returns>
  /// Returns null if the key is not present in <see cref="Model"/> (and
  /// potentially deleting the viewmodel in that case.
  /// Returns the existing or newly created viewmodel otherwise.
  /// </returns>
  public KeyViewModel? FindKey(Guid keyId)
  {
    var rawKey = Model.FindKey(keyId);
    var kvm = Keys.FirstOrDefault(kvm => kvm.KeyId == keyId);
    if(kvm == null)
    {
      if(rawKey == null)
      {
        return null;
      }
      else
      {
        kvm = new KeyViewModel(rawKey);
        Keys.Add(kvm);
        return kvm;
      }
    }
    else
    {
      if(rawKey == null)
      {
        Keys.Remove(kvm);
      }
      return kvm;
    }
  }

  public void SyncModel()
  {
    var newModels = new List<KeyViewModel>();
    var removedModels = new List<KeyViewModel>();
    var vmIds = Keys.Select(k => k.KeyId).ToHashSet();
    foreach(var state in Model.AllStates)
    {
      if(!vmIds.Contains(state.KeyId))
      {
        newModels.Add(new KeyViewModel(state));
      }
    }
    foreach(var vm in Keys)
    {
      if(!Model.HasKey(vm.KeyId))
      {
        removedModels.Remove(vm);
      }
    }
    foreach(var vm in removedModels)
    {
      Keys.Remove(vm);
    }
    foreach(var vm in newModels)
    {
      Keys.Add(vm);
    }
    var needSort = false;
    foreach(var vm in Keys)
    {
      needSort = vm.SyncModel() || needSort;
    }
    if(needSort)
    {
      // Forcing a re-sort when only the content of items change
      // turns out to be tricky. Replacing the sorter with a new
      // (but equivalent) one does the trick.
      KeysView.CustomSort = new KeyViewModelComparer();
    }
  }

  public void ImportKey()
  {
    StatusHost.StatusMessage = "Select file(s) to import the key of.";
    var dialog = new OpenFileDialog() {
      Filter = "All supported files (*.pass.key-info, *.zvlt)|*.pass.key-info;*.zvlt",
      Multiselect = true,
    };
    if(dialog.ShowDialog() == true)
    {
      foreach(var fileName in dialog.FileNames)
      {
        var pkif = PassphraseKeyInfoFile.TryFromFile(fileName);
        if(pkif == null)
        {
          Trace.TraceWarning($"Unsupported file {fileName}");
          StatusHost.StatusMessage = $"Unsupported file {Path.GetFileName(fileName)}";
        }
        else
        {
          var state = Model.GetKey(pkif.KeyId);
          state.AssociateFile(fileName, true);
          StatusHost.StatusMessage = $"Updated key {pkif.KeyId}";
          Trace.TraceInformation($"Linked key {pkif.KeyId} to file {fileName}");
        }
      }
      SyncModel();
    }
    else
    {
      StatusHost.StatusMessage = "Operation canceled";
    }
  }

  public void NewKey()
  {
    Trace.TraceInformation("New Key - NYI");
    StatusHost.StatusMessage = "New Key - NYI";
  }

  public void NewVault()
  {
    Trace.TraceInformation("New Vault - NYI");
    StatusHost.StatusMessage = "New Vault - NYI";
  }

}
