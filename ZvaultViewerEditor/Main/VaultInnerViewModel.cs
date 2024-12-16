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
using FldVault.Core.BlockFiles;
using FldVault.Core.Utilities;

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

  public IApplicationModel ApplicationModel { get => OuterModel.ApplicationModel; }

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
      Mouse.OverrideCursor = Cursors.Wait;
      try
      {
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
      }
      finally
      {
        Mouse.OverrideCursor = null;
      }
      var message = $"Extracted {extracted.Count} files:\n";
      message += String.Join("\n", extracted);
      MessageBox.Show(
        message);
    }
  }

  public void Clone()
  {
    var keyTag = Vault.KeyId.ToString("N")[..8];
    var suffix = $".{keyTag}.zvlt";
    var shortSourceName = Path.GetFileName(Vault.FileName);
    string shortCloneName;
    if(shortSourceName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
    {
      shortCloneName =
        shortSourceName[..^suffix.Length] + "-clone" + suffix;
    }
    else
    {
      shortCloneName =
        shortSourceName[..^".zvlt".Length] + "-clone" + suffix;
    }
    var selectedCount = Entries.Count(ve => ve.Selected);
    var title =
      selectedCount == 0
      ? "Clone empty version of the vault to"
      : $"Clone or append {selectedCount} selected files to";

    var saveDialog = new SaveFileDialog {
      ClientGuid = DialogGuids.VaultFileGuid,
      Title = title,
      FileName = shortCloneName,
      Filter = "Vault files (*.zvlt)|*.zvlt",
      OverwritePrompt = false,
    };

    var result = saveDialog.ShowDialog();

    if(result == true)
    {
      var fileName = saveDialog.FileName;
      if(fileName == Vault.FileName)
      {
        MessageBox.Show(
          "Cannot clone to the same file",
          "Error cloning",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
        return;
      }
      try
      {
        Mouse.OverrideCursor = Cursors.Wait;
        RunClone(fileName);
      }
      catch(Exception ex)
      {
        MessageBox.Show(
          $"Error cloning to '{fileName}': {ex.Message}",
          "Error cloning",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
      }
      finally
      {
        Mouse.OverrideCursor = null;
      }
    }
  }

  public void Append()
  {
    var dialog = new OpenFileDialog {
      ClientGuid = DialogGuids.PeerFolderGuid,
      Title = "Select file(s) to append to the vault",
      Filter = "Any file (*.*)|*.*",
      Multiselect = true,
    };
    var response = dialog.ShowDialog();
    if(response == true)
    {
      var fileNames = dialog.FileNames.ToList();
      if(fileNames.Count == 0)
      {
        return;
      }
      if(fileNames.Any(
        fnm => fnm.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase)))
      {
        var response1 = MessageBox.Show(
          "One or more of the selected files is a metadata file.\n" +
          "These will be included automatically as metadata for the file they describe\n" +
          "and should normally not be included explicitly.\n"+
          "'Yes' to remove them, 'No' to add the as normal files too",
          "Remove metadata files as main entries?",
          MessageBoxButton.YesNoCancel,
          MessageBoxImage.Question);
        if(response1 == MessageBoxResult.Cancel)
        {
          return;
        }
        if(response1 == MessageBoxResult.Yes)
        {
          fileNames = fileNames.Where(
            fnm => !fnm.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase))
            .ToList();
        }
      }
      var duplicates =
        fileNames
        .Where(fnm => Entries.Any(
          ve => Path.GetFileName(fnm).Equals(
            ve.FileName, StringComparison.OrdinalIgnoreCase)))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
      if(duplicates.Count > 0)
      {
        var response2 = MessageBox.Show(
          $"One or more of the selected files already exist in the vault.\n" +
          "Ok to ignore these? 'Yes' to ignore, 'No' to create duplicate named entries.",
          "Duplicate file names",
          MessageBoxButton.YesNoCancel,
          MessageBoxImage.Warning);
        if(response2 == MessageBoxResult.Cancel)
        {
          return;
        }
        if(response2 == MessageBoxResult.Yes)
        {
          fileNames = fileNames.Where(
            fnm => !duplicates.Contains(fnm))
            .ToList();
        }
      }
      Mouse.OverrideCursor = Cursors.Wait;
      try
      {
        using var cryptor = Vault.CreateCryptor(KeyChain);
        using(var vfw = new VaultFileWriter(Vault, cryptor))
        {
          foreach(var fileName in fileNames)
          {
            var fi = new FileInfo(fileName);
            var fileGuid = Guid.NewGuid(); // Maybe base on Hash in the future
            var meta = new FileMetadata(
              fi.Name,
              EpochTicks.FromUtc(fi.LastWriteTimeUtc),
              fi.Length);
            var customMetaName = fileName + ".meta.json";
            if(File.Exists(customMetaName))
            {
              meta.TryMergeMetadata(customMetaName);
            }
            using var stream = File.OpenRead(fileName);
            // This overload provides more control:
            var be = vfw.AppendFile(
              stream,
              meta,
              ZvltCompression.Auto,
              DateTime.UtcNow, // encryption time, not file time!!
              fileGuid);
          }
        }
        ReloadEntries();
      }
      catch(Exception ex)
      {
        MessageBox.Show(
          $"Error appending files: {ex.Message}",
          "Error appending files",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
      }
      finally
      {
        Mouse.OverrideCursor = null;
      }
    }
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
        if(PreserveTimestamps && metadata.Stamp.HasValue)
        {
          var utcStamp = EpochTicks.ToUtc(metadata.Stamp.Value);
          File.SetLastWriteTimeUtc(metadataPath, utcStamp);
        }
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

  private void RunClone(string targetName)
  {
    var entries = Entries.Where(ve => ve.Selected).ToList();
    var selectedCount = entries.Count;
    var shortTargetName = Path.GetFileName(targetName);

    if(File.Exists(targetName))
    {
      var probeExisting = VaultFile.Open(targetName);
      if(!VaultCloneEngine.AreVaultsCompatible(Vault, probeExisting))
      {
        MessageBox.Show(
          "The existing target vault is not compatible with the source vault",
          "Incompatible vaults",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
        return;
      }
      if(selectedCount == 0)
      {
        var response = MessageBox.Show(
          $"{shortTargetName} already exists.\n"+
          "Overwrite existing file with an empty vault?",
          "Overwrite?",
          MessageBoxButton.OKCancel,
          MessageBoxImage.Question);
        if(response == MessageBoxResult.Cancel)
        {
          return;
        }
        var bakName = targetName + ".bak";
        File.Move(targetName, bakName, true);
      }
      else
      {
        var response = MessageBox.Show(
          $"{shortTargetName} already exists.\n"+
          "'Yes' to append, 'No' to replace.",
          "Overwrite?",
          MessageBoxButton.YesNoCancel,
          MessageBoxImage.Question);
        if(response == MessageBoxResult.Cancel)
        {
          return;
        }
        if(response == MessageBoxResult.No)
        {
          var bakName = targetName + ".bak";
          File.Move(targetName, bakName, true);
        }
      }
    }

    using var cloner = VaultCloneEngine.Create(Vault, targetName);

    if(selectedCount == 0)
    {
      ApplicationModel.StatusMessage =
        $"Cloned empty vault to {shortTargetName}";
      return;
    }

    // For now we skip guarding against duplicates in the target
    // since there are some tricky things involved that may not be
    // worth the effort. I may add this later.

    foreach(var entry in entries)
    {
      var fileElement = entry.Element;
      var rootElement = fileElement.RootElement;
      cloner.CloneElement(rootElement);
    }
    ApplicationModel.StatusMessage =
      $"Copied {entries.Count} entries to {shortTargetName}";
  }

}
