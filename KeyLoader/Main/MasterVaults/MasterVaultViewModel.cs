using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FldVault.Core.Crypto;
using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;
using FldVault.KeyServer;

using KeyLoader.UserMessages;

namespace KeyLoader.Main.MasterVaults;

/// <summary>
/// ViewModel for an unlocked master key vault and its contents.
/// </summary>
public class MasterVaultViewModel: ObservableObject
{
  /// <summary>
  /// A copy of <see cref="Owner"/>'s child key chain
  /// </summary>
  private readonly KeyChain _childKeyChain;
  private readonly KeyChain _masterKeyChain;
  private readonly Dictionary<Guid, ChildKeyViewModel> _children;

  /// <summary>
  /// Create a new <see cref="MasterVaultViewModel"/>
  /// </summary>
  /// <param name="owner">
  /// The owning <see cref="MasterTabViewModel"/> providing the details
  /// of the vault
  /// </param>
  /// <param name="childKeyChain">
  /// The child key chain from <paramref name="owner"/>
  /// </param>
  /// <param name="masterKeyChain">
  /// The master key chain containing the key to unlock the master vault
  /// </param>
  public MasterVaultViewModel(
    MasterTabViewModel owner,
    KeyChain childKeyChain,
    KeyChain masterKeyChain)
  {
    Owner = owner;
    _childKeyChain = childKeyChain;
    _masterKeyChain = masterKeyChain;
    Keys = new ObservableCollection<ChildKeyViewModel>();
    _children = new Dictionary<Guid, ChildKeyViewModel>();
    if(!Owner.FileExists)
    {
      throw new InvalidOperationException(
        "Cannot create the MasterVaultViewModel: the file does not exist");
    }
    if(!Owner.MasterKeyLoaded)
    {
      throw new InvalidOperationException(
        "Cannot create the MasterVaultViewModel: the key is not available");
    }
    TryPasteCommand = new RelayCommand(
      TryPaste,
      () => Owner.IsEditing);
    ReloadContent();
  }

  public RelayCommand TryPasteCommand { get; }

  /// <summary>
  /// The owner of this unlocked master vault viewmodel, providing the details
  /// of the vault that are not related to the fact that the vault is unlocked,
  /// as well as the parts that assert it is unlocked.
  /// </summary>
  public MasterTabViewModel Owner { get; }

  /// <summary>
  /// The list child keys in this vault, as a list. This is an MVVM friendly
  /// copy of the list in the key map.
  /// </summary>
  public ObservableCollection<ChildKeyViewModel> Keys { get; }

  /// <summary>
  /// Get or create a <see cref="ChildKeyViewModel"/> for the key
  /// indicated by <paramref name="keyId"/>
  /// </summary>
  /// <param name="keyId"></param>
  /// <returns></returns>
  public ChildKeyViewModel GetKey(Guid keyId)
  {
    if(!_children.TryGetValue(keyId, out var childVm))
    {
      childVm = new ChildKeyViewModel(this, keyId);
      _children.Add(keyId, childVm);
      Keys.Add(childVm);
    }
    return childVm;
  }

  /// <summary>
  /// Add a key to this list. Refreshes the key-known state. If necessary
  /// this creates the <see cref="ChildKeyViewModel"/> for the key first.
  /// </summary>
  /// <param name="keyId"></param>
  public ChildKeyViewModel AddKey(Guid keyId)
  {
    var vm = GetKey(keyId);
    vm.UpdateKeyKnown();
    return vm;
  }

  /// <summary>
  /// Set the <see cref="ChildKeyViewModel.KeyInfo"/> for the child key
  /// indicated by <see cref="PassphraseKeyInfoFile.KeyId"/>, adding a new
  /// <see cref="ChildKeyViewModel"/> first if necessary.
  /// </summary>
  /// <param name="pkif"></param>
  /// <returns></returns>
  public ChildKeyViewModel AddKey(PassphraseKeyInfoFile pkif)
  {
    var vm = GetKey(pkif.KeyId);
    vm.KeyInfo = pkif;
    return vm;
  }

  /// <summary>
  /// Try to get the <see cref="ChildKeyViewModel"/> for the given <paramref name="keyId"/>
  /// </summary>
  /// <param name="keyId">
  /// The ID to look up
  /// </param>
  /// <param name="childVm">
  /// Receives the value if found. May be null otherwise
  /// </param>
  /// <returns>
  /// True if found, false if unknown.
  /// </returns>
  public bool TryFindKey(Guid keyId, [MaybeNullWhen(false)] out ChildKeyViewModel childVm)
  {
    return _children.TryGetValue(keyId, out childVm);
  }

  /// <summary>
  /// Try to save the vault file given the current state.
  /// </summary>
  /// <returns></returns>
  internal void Save()
  {
    if(!Owner.Modified)
    {
      Trace.TraceWarning($"Saving unmodified vault '{Owner.Title}'");
    }
    else
    {
      Trace.TraceInformation($"Saving modified vault '{Owner.Title}'");
    }
    var destination = Owner.FileName;
    var masterKey = Owner.MasterKey ?? throw new InvalidOperationException("Missing master key info");
    var keyIds = Keys.Select(ckv => ckv.KeyId).Where(_childKeyChain.ContainsKey).ToList();
    var links = Keys.Where(ckv => ckv.KeyInfo != null).Select(ckv => ckv.KeyInfo!).ToList();
    VaultFile.WriteMasterKeyFile(
      destination,
      masterKey,
      keyIds,
      _childKeyChain,
      _masterKeyChain,
      links);
    Owner.MarkModified(false);
  }

  /// <summary>
  /// Asynchronously refresh the raw key value from the key server, if 
  /// the key server is available.
  /// </summary>
  /// <param name="keyId"></param>
  /// <returns></returns>
  public async Task<KeyPresence?> TryRetrieveKey(Guid keyId)
  {
    var serverWidget = Owner.Owner.ServerWidget;
    var server = serverWidget.Server;
    if(server.ServerAvailable)
    {
      var result = await server.LookupKeyAsync(keyId, _childKeyChain, serverWidget.AppCancelationToken);
      if(result == KeyPresence.Present)
      {
        var vm = GetKey(keyId);
        vm.UpdateKeyKnown();
      }
      return result;
    }
    return null;
  }

  /// <summary>
  /// Try to paste whatever is found on the clipboard. As a new key, an update to an existing key,
  /// or even a new master key tab.
  /// </summary>
  /// <remarks>
  /// Tries to recognize the following:
  /// <list type="bullet">
  /// <item>A guid, treated as bare key id. Known master key ids are rejected</item>
  /// <item>A ZKEY record, with or without passphrase</item>
  /// <item>A master key file or master key file name</item>
  /// <item>Another type of key-bearing file or their name (extracting a key-info record)</item>
  /// </list>
  /// </remarks>
  private void TryPaste()
  {
    Owner.MessageHost.ShowError("Magic paste: Not yet implemented");
  }

  internal void UpdateCommandEnabledStates()
  {
    TryPasteCommand.NotifyCanExecuteChanged();
  }

  internal bool HasChildKey(Guid keyId)
  {
    return _childKeyChain.ContainsKey(keyId);
  }

  private void ReloadContent()
  {
    Trace.TraceInformation(
      $"Loading master vault '{Owner.Title}'");
    Keys.Clear();
    _children.Clear();
    var vault = new VaultFile(Owner.FileName, ZvltPurpose.Master);
    using var cryptor = vault.CreateCryptor(_masterKeyChain);
    using var reader = new VaultFileReader(vault, cryptor);
    var keyset = reader.ReadChildKeys(_childKeyChain);
    var linkmap = reader.ReadExternalPassphraseLinks();
    var fullKeySet = new HashSet<Guid>();
    fullKeySet.UnionWith(keyset);
    fullKeySet.UnionWith(linkmap.Keys);
    var sortedKeys = fullKeySet.OrderBy(guid => guid.ToString()).ToList();
    foreach(var key in sortedKeys)
    {
      AddKey(key);
      if(linkmap.TryGetValue(key, out var pkif))
      {
        AddKey(pkif);
      }
    }
    Trace.TraceInformation(
      $"  loaded {Keys.Count} child keys for master vault '{Owner.Title}'");
    Owner.MarkModified(false);
  }
}
