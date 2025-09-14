/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;

using Microsoft.Win32;

using ZvaultViewerEditor.WpfUtilities;

namespace ZvaultViewerEditor.Main;

/// <summary>
/// ViewModel for the content shown when no vault file is loaded.
/// This VM exists primarily to isolate behaviour for this state
/// from <see cref="MainViewModel"/> (there is no own state).
/// </summary>
public class NoVaultViewModel: ViewModelBase
{
  public NoVaultViewModel(
    IApplicationModel applicationModel)
  {
    ApplicationModel = applicationModel;
  }

  public IApplicationModel ApplicationModel { get; }

  private static readonly IReadOnlyList<string> __fileTypesToOpen = [
    ".zvlt",
  ];

  private static readonly IReadOnlyList<string> __fileTypesToTemplate = [
    ".mvlt",
    ".zkey",
    ".pass.key-info",
  ];

  internal void DropEvent(DragEventArgs e)
  {
    if(e.Data.GetDataPresent(DataFormats.FileDrop))
    {
      var files = (string[])e.Data.GetData(DataFormats.FileDrop);
      if(files.Length != 1)
      {
        Trace.TraceWarning(
          $"Only expecting files being dropped one at a time, but got {files.Length}");
        SetStatus(
          $"Only expecting files being dropped one at a time, but got {files.Length}");
      }
      else
      {
        var file = files[0];
        if(__fileTypesToOpen.Any(
          ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
          var opened = ApplicationModel.TryOpenVault(file);
          if(!opened)
          {
            Trace.TraceError("Failed to open the dropped file as a vault");
          }
          // Feedback was already given
        }
        else if(__fileTypesToTemplate.Any(
          ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
          CreateNewVaultBasedOn(file);
        }
        else
        {
          Trace.TraceError($"Unrecognized dropped file: {file}");
          SetStatus($"Unrecognized file format");
        }
      }
    }
    e.Handled = true;
  }

  internal void DragOverEvent(DragEventArgs e)
  {
    if(e.Data.GetDataPresent(DataFormats.FileDrop))
    {
      var files = (string[])e.Data.GetData(DataFormats.FileDrop);
      if(files.Length != 1)
      {
        e.Effects = DragDropEffects.None;
        ApplicationModel.StatusMessage = "Multi-file drops are not supported";
      }
      else
      {
        var file = files[0];
        if(__fileTypesToOpen.Any(
          ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
          e.Effects = DragDropEffects.Copy;
          ApplicationModel.StatusMessage = "Open the Z-Vault file";
        }
        else if(__fileTypesToTemplate.Any(
          ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
          e.Effects = DragDropEffects.Link;
          ApplicationModel.StatusMessage = "Start creating a new Z-Vault file based on this file";
        }
        else
        {
          e.Effects = DragDropEffects.None;
          ApplicationModel.StatusMessage = "Unsupported file type";
        }
      }
    }
    else
    {
      e.Effects = DragDropEffects.None;
      ApplicationModel.StatusMessage = "Only file drops are supported";
    }
    e.Handled = true;
  }

  private void CreateNewVaultBasedOn(string keyBearingFile)
  {
    var file = Path.GetFullPath(keyBearingFile);
    var shortName = Path.GetFileName(file);
    var folder = Path.GetDirectoryName(file);
    var pkif = PassphraseKeyInfoFile.TryFromFile(file);
    if(pkif == null)
    {
      Trace.TraceError(
        $"Retrieving PKIF from file failed: {file}");
      SetStatus($"Unable to retrieve key information from '{shortName}'");
    }
    else
    {
      var kss = ApplicationModel.KeyServer;
      if(kss.ServerAvailable)
      {
        var guid = kss.RegisterFileSync(file, ApplicationModel.KeyChain);
        if(guid.HasValue)
        {
          Trace.TraceInformation(
            $"Registered {shortName} with server. Key {guid} is available and loaded.");
        }
        else
        {
          Trace.TraceInformation(
            $"Registered {shortName} with server. Key is not available (yet).");
        }
      }
      var keyTag = pkif.KeyId.ToString().Substring(0, 8);
      var proposedName = $"new-vault.{keyTag}.zvlt";
      var dialog = new SaveFileDialog() {
        Filter = "Z-Vault files (*.zvlt)|*.zvlt",
        OverwritePrompt = true,
        CheckPathExists = true,
        DefaultExt = ".zvlt",
        FileName = proposedName,
        InitialDirectory = folder,
        Title = $"Create new vault with key of {shortName}"
      };
      if(dialog.ShowDialog() == true)
      {
        var newVault = VaultFile.CreateEmpty(dialog.FileName, pkif);
        SetStatus($"Created {dialog.FileName}");
        Trace.TraceInformation($"Created new vault file {dialog.FileName}");
        ApplicationModel.TryOpenVault(dialog.FileName);
      }
    }
  }

  private void SetStatus(string message)
  {
    ApplicationModel.StatusMessage = message;
  }
}
