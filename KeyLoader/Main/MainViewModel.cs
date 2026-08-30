using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FldVault.KeyServer;

using KeyLoader.Main.TaskTab;

namespace KeyLoader.Main;

/// <summary>
/// The main application viewmodel
/// </summary>
public class MainViewModel: ObservableObject
{
  /// <summary>
  /// Create a new <see cref="MainViewModel"/>. Called as part of the bootstrapping
  /// process in App.xaml.cs. This demo application uses the "ViewModel first" approach.
  /// </summary>
  public MainViewModel()
  {
    KeyServer = new KeyServerService();
    ExitCommand = new RelayCommand(() => {
      var w = Application.Current.MainWindow;
      w?.Close();
    });
    TaskTabs = new ObservableCollection<TaskTabBaseViewModel>();
    TryCloseCurrentTabCommand = new RelayCommand(
      () => _ = TryCloseCurrentTab(),
      CanCloseCurrentTab);
  }

  /// <summary>
  /// Command to bind to the "File|Exit" menu. Closes the main window and
  /// thus the application.
  /// </summary>
  public ICommand ExitCommand { get; }

  /// <summary>
  /// Get or set the message shown in the status bar
  /// </summary>
  public string StatusMessage {
    get => _statusMessage;
    set {
      SetProperty(ref _statusMessage, value);
    }
  }
  private string _statusMessage = "CellGrids demo application (work in progress)";

  /// <summary>
  /// The key server instance
  /// </summary>
  public KeyServerService KeyServer { get; }

  /// <summary>
  /// The list of open Task Tabs (implemented by subclasses of <see cref="TaskTabBaseViewModel"/>)
  /// </summary>
  public ObservableCollection<TaskTabBaseViewModel> TaskTabs { get; }

  /// <summary>
  /// The currently active Task Tab, if any.
  /// The tab to add should already be present in <see cref="TaskTabs"/>.
  /// </summary>
  public TaskTabBaseViewModel? CurrentTab {
    get => _currentTab;
    set {
      if(value != null && !TaskTabs.Contains(value))
      {
        // This is a normal way of adding a new tab
        RegisterTab(value);
      }
      if(value != _currentTab)
      {
        var old = _currentTab;
        OnPropertyChanging();
        old?.BeforeIsActiveChange();
        value?.BeforeIsActiveChange();
        _currentTab = value;
        old?.AfterIsActiveChange();
        value?.AfterIsActiveChange();
        OnPropertyChanged();
      }
    }
  }
  private TaskTabBaseViewModel? _currentTab;

  /// <summary>
  /// Test if the current tab can be closed
  /// </summary>
  /// <returns></returns>
  public bool CanCloseCurrentTab()
  {
    return CurrentTab != null && !CurrentTab.Modified;
  }

  /// <summary>
  /// Try to close the current tab, if there is one and it can be closed.
  /// </summary>
  /// <returns></returns>
  public bool TryCloseCurrentTab()
  {
    if(CurrentTab == null)
    {
      return false;
    }
    else if(CurrentTab.Modified)
    {
      return false;
    }
    else
    {
      var tabToClose = CurrentTab;
      return tabToClose.TryCloseGentle(); // also takes care of deactivation
    }
  }

  /// <summary>
  /// Deactivate the given <paramref name="tab"/> if it is the <see cref="CurrentTab"/>.
  /// Else this is a NOP.
  /// </summary>
  /// <param name="tab"></param>
  public void Deactivate(TaskTabBaseViewModel tab)
  {
    if(tab == CurrentTab)
    {
      // Not yet implemented, but throwing an exception is not safe now.
      // Todo: pick and activate a different tab in a sensible way
      
      // Temporary plug: pick *first* other tab (if there is any)
      var othertab = TaskTabs.Where(t => t != tab).FirstOrDefault();
      CurrentTab = othertab;
      Trace.TraceError(
        $"Deactivating tabs is currently using a simplified implementation. Tab was '{tab.Title}'");
    }
  }

  /// <summary>
  /// Callback after a tab has been closed and disposed
  /// </summary>
  /// <param name="tab"></param>
  internal void TabClosed(TaskTabBaseViewModel tab)
  {
    Deactivate(tab);
    TaskTabs.Remove(tab);
  }

  /// <summary>
  /// Add a tab, if it wasn't present already.
  /// Alternatively, just set the new tab as current tab
  /// (which calls this)
  /// </summary>
  /// <param name="tab"></param>
  public void RegisterTab(TaskTabBaseViewModel tab)
  {
    if(!TaskTabs.Contains(tab))
    {
      TaskTabs.Add(tab);
    }
  }

  /// <summary>
  /// The command to try to close the current tab
  /// </summary>
  public ICommand TryCloseCurrentTabCommand { get; }
}
