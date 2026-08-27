using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace KeyLoader.Main.TaskTab;

/// <summary>
/// Shared base class for all task tab implementations
/// </summary>
public class TaskTabBaseViewModel: ObservableObject, IDisposable
{
  private bool _disposed;

  /// <summary>
  /// Create a new <see cref="TaskTabBaseViewModel"/>
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
  /// Get or set if this tab is the current one. Changing this
  /// affects <see cref="MainViewModel.CurrentTab"/>.
  /// </summary>
  public bool IsActive {
    get => this == Owner.CurrentTab;
    set {
      if(this == Owner.CurrentTab && !value)
      {
        OnPropertyChanging();
        Owner.CurrentTab = null;
        OnPropertyChanged();
      }
      else if(this != Owner.CurrentTab && value)
      {
        OnPropertyChanging();
        Owner.CurrentTab = this;
        OnPropertyChanged();
      }
    }
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
