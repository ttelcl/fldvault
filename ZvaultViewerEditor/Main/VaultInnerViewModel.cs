/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using Microsoft.Win32;

using Newtonsoft.Json;

using FldVault.Core.Crypto;
using FldVault.Core.Zvlt2;

using ZvaultViewerEditor.WpfUtilities;

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
    SelectAllCommand = new DelegateCommand(
      p => SelectAllOrNone(true),
      p => true);
    SelectNoneCommand = new DelegateCommand(
      p => SelectAllOrNone(false),
      p => true);
    ExtractCommand = new DelegateCommand(
      p => Extract(),
      p => HasSelected);
    CloneCommand = new DelegateCommand(
      p => Clone(),
      p => true);
    AppendCommand = new DelegateCommand(
      p => Append(),
      p => AllowWrite && !FileReadOnly);
    var fileName = OuterModel.Vault.FileName;
    FileReadOnly =
      (File.GetAttributes(fileName) & FileAttributes.ReadOnly) != 0;
    ReloadEntries();
  }

  public ICommand SelectAllCommand { get; }

  public ICommand SelectNoneCommand { get; }

  public ICommand ExtractCommand { get; }

  public ICommand CloneCommand { get; }

  public ICommand AppendCommand { get; }

  public VaultOuterViewModel OuterModel { get; }

  public VaultFile Vault { get => OuterModel.Vault; }

  public KeyChain KeyChain { get => OuterModel.KeyChain; }

  public List<VaultEntryViewModel> Entries {
    get => _entries;
    private set {
      if(SetInstanceProperty(ref _entries, value))
      {
        RaisePropertyChanged(nameof(FileCount));
      }
    }
  }
  private List<VaultEntryViewModel> _entries = [];

  public int FileCount => Entries.Count;

  public void ReloadEntries()
  {
    using var cryptor = Vault.CreateCryptor(KeyChain);
    using var reader = new VaultFileReader(Vault, cryptor);
    var entries =
      Vault.FileElements()
      .Select(e => new FileElement(e))
      .Select(fe => VaultEntryViewModel.TryCreate(this, fe, reader))
      .Where(ve => ve!=null)
      .Cast<VaultEntryViewModel>()
      .ToList();
    //entries.Sort(
    //  (ve1, ve2) => StringComparer.OrdinalIgnoreCase.Compare(ve1.FileName, ve2.FileName));
    Entries = entries;
    Trace.TraceInformation($"Loaded {FileCount} entries from {Vault.FileName}");
  }

  public bool HasSelected {
    get => _hasSelected;
    private set {
      if(SetValueProperty(ref _hasSelected, value))
      { 
      }
    }
  }
  private bool _hasSelected;

  public bool AllowWrite {
    get => _allowWrite;
    set {
      if(FileReadOnly && value)
      {
        MessageBox.Show("Cannot write to a read-only file");
        return;
      }
      if(SetValueProperty(ref _allowWrite, value))
      {
      }
    }
  }
  private bool _allowWrite;

  public bool FileReadOnly {
    get => _fileReadOnly;
    private set {
      if(SetValueProperty(ref _fileReadOnly, value))
      {
        RaisePropertyChanged(nameof(FileNotReadOnly));
        if(value)
        {
          AllowWrite = false;
        }
      }
    }
  }
  private bool _fileReadOnly;

  public bool FileNotReadOnly {
    get => !FileReadOnly;
  }

  public bool ExtractMetadata {
    get => _extractMetadata;
    set {
      if(SetValueProperty(ref _extractMetadata, value))
      {
      }
    }
  }
  private bool _extractMetadata = false;

  public bool PreserveTimestamps {
    get => _preserveTimestamps;
    set {
      if(SetValueProperty(ref _preserveTimestamps, value))
      {
      }
    }
  }
  private bool _preserveTimestamps = true;

  public void UpdateHasSelected()
  {
    HasSelected = Entries.Any(ve => ve.Selected);
  }

  public void SelectAllOrNone(bool all)
  {
    foreach(var ve in Entries)
    {
      ve.Selected = all;
    }
  }

  public void Extract()
  {
    var peerFolderDialog = new OpenFolderDialog {
      ClientGuid = DialogGuids.PeerFolderGuid,
      Title = "Select target folder for extraction",
    };
    if(peerFolderDialog.ShowDialog() == true)
    {
      var targetFolder = Path.GetFullPath(peerFolderDialog.FolderName);
      using var cryptor = Vault.CreateCryptor(KeyChain);
      using var reader = new VaultFileReader(Vault, cryptor);
      var extracted = new List<string>();
      foreach(var entry in Entries.Where(e => e.Selected))
      {
        var result = ExtractTo(targetFolder, entry, reader);
        if(result == null)
        {
          MessageBox.Show("Extraction aborted");
          break;
        }
        if(result == true)
        {
          extracted.Add(entry.FileName);
        }
      }
      var message = $"Extracted {extracted.Count} files:\n";
      message += String.Join("\n", extracted);
      MessageBox.Show(
        message);
    }
  }

  public void Clone()
  {
    MessageBox.Show("Clone not yet implemented");
  }

  public void Append()
  {
    MessageBox.Show("Append not yet implemented");
  }

  /// <summary>
  /// Run the extract job.
  /// Returns null to abort further extractions.
  /// Returns true to indicate success.
  /// Returns false to indicate skip.
  /// </summary>
  private bool? ExtractTo(
    string targetFolder,
    VaultEntryViewModel entry,
    VaultFileReader reader)
  {
    var logicalName = entry.FileName;
    if(String.IsNullOrEmpty(logicalName))
    {
      logicalName = $"anon-{entry.FileGuid}.unknown";
    }
    var targetPath = Path.Combine(targetFolder, logicalName);
    var targetDir = Path.GetDirectoryName(targetPath);
    if(!Directory.Exists(targetDir))
    {
      Directory.CreateDirectory(targetDir!);
    }
    var targetShort = Path.GetFileName(targetPath);
    if(File.Exists(targetPath))
    {
      var response = MessageBox.Show(
        $"File '{targetShort}' already exists. 'Yes' to overwrite, 'No' to skip.",
        "Overwrite?",
        MessageBoxButton.YesNoCancel,
        MessageBoxImage.Question);
      if(response == MessageBoxResult.Cancel)
      {
        return null;
      }
      if(response == MessageBoxResult.No)
      {
        return false;
      }
    }
    // (response == MessageBoxResult.Yes, or no question asked)
    var fileElement = entry.Element;
    try
    {
      fileElement.SaveContentToFile(
        reader,
        targetFolder, // not really used
        targetPath,
        PreserveTimestamps,
        false);
      if(ExtractMetadata)
      {
        var metadataPath = targetPath + ".meta.json";
        var metadata = entry.Metadata;
        var json = JsonConvert.SerializeObject(
          metadata, Formatting.Indented);
        File.WriteAllText(metadataPath, json, Encoding.UTF8);
      }
      return true;
    }
    catch(Exception ex)
    {
      MessageBox.Show(
        $"Error writing '{targetShort}': {ex.Message}",
        "Error writing file",
        MessageBoxButton.OK,
        MessageBoxImage.Error);
      return null;
    }
  }
}
