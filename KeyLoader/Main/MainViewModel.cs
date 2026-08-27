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
      if(SetProperty(ref _statusMessage, value))
      {
      }
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
        if(old != null)
        {
          // In this case the callback from setting old.IsActive fizzles because
          // it no longer is the current tab
          old.IsActive = false; // this will set _currentTab to null
        }
        if(value != null)
        {
          // In this case the callback from setting new.IsActive fizzles because
          // it already is the current tab
          value.IsActive = true;
        }
        OnPropertyChanged();
      }
      throw new NotImplementedException("This logic is incorrect!!!");
    }
  }
  private TaskTabBaseViewModel? _currentTab;

  internal void SetCurrentTabCallback(TaskTabBaseViewModel? currentTab) 
  {
    _currentTab = currentTab;
  }
}
