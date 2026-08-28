using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KeyLoader.Main.TaskTab;

/// <summary>
/// Shared base class for all task tab implementations
/// </summary>
public class TaskTabBaseViewModel: ObservableObject, IDisposable
{
  private bool _disposed;

  /// <summary>
  /// Create a new <see cref="TaskTabBaseViewModel"/>.
  /// Does not register or activate the new tab in the parent
  /// (because the child class constructor should run first)
  /// </summary>
  /// <param name="owner"></param>
  /// <param name="title">
  /// The title of the tab
  /// </param>
  /// <param name="modified">
  /// True if the tab initially is in the "modified" state.
  /// </param>
  public TaskTabBaseViewModel(
    MainViewModel owner,
    string title = "untitled",
    bool modified = false)
  {
    Owner = owner;
    _title = title;
    _modified = modified;
    TryCloseInteractiveCommand = new RelayCommand(
      () => TryCloseGentle(),
      () => true);
  }

  /// <summary>
  /// The owning <see cref="MainViewModel"/>
  /// </summary>
  public MainViewModel Owner { get; }

  /// <summary>
  /// The tab title
  /// </summary>
  public string Title {
    get => _title;
    set {
      SetProperty(ref _title, value);
    }
  }
  private string _title;

  /// <summary>
  /// True if this tab has been modified and thus can not be closed without further interaction
  /// </summary>
  public bool Modified {
    get => _modified;
    protected set {
      SetProperty(ref _modified, value);
    }
  }
  private bool _modified;

  /// <summary>
  /// Forcefully close this tab. If <see cref="Modified"/>, the changes are lost and exact behaviour
  /// may be undefined.
  /// </summary>
  public void CloseHard()
  {
    Owner.TabClosed(this);
    Dispose();
  }

  /// <summary>
  /// Attempt to close this tab. If the tab is <see cref="Modified"/>, the user
  /// is asked what to do: Cancel (not closing), Save, or close anyway. If the answer
  /// is "Save", but it fails, the request is effectively Canceled
  /// </summary>
  /// <returns>
  /// <see langword="true"/> if succesfully closed, or <see langword="false"/> if canceled
  /// or saving failed.
  /// </returns>
  public bool TryCloseGentle()
  {
    if(Modified)
    {
      var answer = MessageBox.Show(
        "Save before closing? 'No' will close without saving.",
        "Data is not saved",
        MessageBoxButton.YesNoCancel);
      if(answer == MessageBoxResult.Cancel)
      {
        return false;
      }
      if(answer == MessageBoxResult.Yes)
      {
        TrySave();
        if(Modified)
        {
          // save failed
          MessageBox.Show("Saving failed. Canceling the closing.");
          return false;
        }
      }
    }
    // Not modified, answered "No", or Save succeeded
    CloseHard();
    return true;
  }

  /// <summary>
  /// A command to close this tab. If modified, this asks to save before closing,
  /// which also allows cancelling the closing
  /// </summary>
  public ICommand TryCloseInteractiveCommand { get; }

  /// <summary>
  /// Attempt to save the data. Upon success <see cref="Modified"/> will be
  /// <see langword="false"/> after return. Subclasses should override this.
  /// The default implementation always fails (and does not change <see cref="Modified"/>)
  /// </summary>
  /// <returns></returns>
  protected virtual void TrySave()
  {
    // default implementation does nothing, effectively failing the save.
  }

  /// <summary>
  /// Get if this tab is the current one. Changing this
  /// is done by setting <see cref="MainViewModel.CurrentTab"/>.
  /// </summary>
  public bool IsActive {
    get => this == Owner.CurrentTab;
  }

  /// <summary>
  /// Callback from setting <see cref="MainViewModel.CurrentTab"/> to previous and new task tabs
  /// </summary>
  internal void BeforeIsActiveChange()
  {
    OnPropertyChanging(nameof(IsActive));
  }

  /// <summary>
  /// Callback from setting <see cref="MainViewModel.CurrentTab"/> to previous and new task tabs
  /// </summary>
  internal void AfterIsActiveChange()
  {
    OnPropertyChanged(nameof(IsActive));
  }

  /// <summary>
  /// Clean up (dispose pattern). This base implementation does nothing.
  /// </summary>
  /// <param name="disposing"></param>
  protected virtual void Dispose(bool disposing)
  {
    if(!_disposed)
    {
      _disposed=true;
      if(disposing)
      {
        // TODO: dispose managed state (managed objects)
      }
    }
  }

  /// <summary>
  /// Clean up. Redirects to <see cref="Dispose(bool)"/>.
  /// </summary>
  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }
}
