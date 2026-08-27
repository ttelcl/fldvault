using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

}
