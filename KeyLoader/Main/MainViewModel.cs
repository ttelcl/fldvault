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
        TaskTabs.Add(value);
        Trace.TraceWarning(
          $"Setting unknown tab '{value.Title}' as active tab. Adding it as new tab as side effect.");
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
  /// <exception cref="NotImplementedException"></exception>
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
      // * pick another tab to make active instead
      // * activate that other tab
      // * actually close the tab to be closed (dispose content)
      // * remove the closed tab from the list
      throw new NotImplementedException();
    }
  }

  /// <summary>
  /// The command to try to close the current tab
  /// </summary>
  public ICommand TryCloseCurrentTabCommand { get; }
}
