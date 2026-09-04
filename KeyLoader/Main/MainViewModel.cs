using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FldVault.KeyServer;

using KeyLoader.Main.ServerWidget;
using KeyLoader.Main.TaskTab;

namespace KeyLoader.Main;

/// <summary>
/// The main application viewmodel
/// </summary>
public class MainViewModel: ObservableObject
{
  private readonly CancellationTokenSource _modelAwakeTokenSource;

  /// <summary>
  /// Create a new <see cref="MainViewModel"/>. Called as part of the bootstrapping
  /// process in App.xaml.cs. This demo application uses the "ViewModel first" approach.
  /// </summary>
  public MainViewModel()
  {
    _modelAwakeTokenSource = new CancellationTokenSource();
    AppAwakeToken = _modelAwakeTokenSource.Token;
    TabHost = new TabHostViewModel<MainViewModel>(this);
    ServerWidget = new ServerWidgetViewModel(this);
    ExitCommand = new RelayCommand(() => {
      ApplicationClosing(); // One of two paths calling it. The other is in App.
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
  /// The <see cref="CancellationToken"/> that is canceled when the app is closed.
  /// </summary>
  public CancellationToken AppAwakeToken { get; }

  /// <summary>
  /// The server widget
  /// </summary>
  public ServerWidgetViewModel ServerWidget { get; }

  /// <summary>
  /// Get or set the message shown in the status bar
  /// </summary>
  public string StatusMessage {
    get => _statusMessage;
    set {
      SetProperty(ref _statusMessage, value);
    }
  }
  private string _statusMessage = "Key Loader and Master Key management application";

  /// <summary>
  /// The title of the application window
  /// </summary>
  public string WindowTitle {
    get => _windowTitle;
    set {
      SetProperty(ref _windowTitle, value);
    }
  }
  private static string __defaultWindowTitle = "Key Loader";
  private string _windowTitle = __defaultWindowTitle;

  /// <summary>
  /// The key server instance
  /// </summary>
  public KeyServerService KeyServer => ServerWidget.Server;

  /// <summary>
  /// The tab host, storing and managing the child tabs
  /// </summary>
  public TabHostViewModel<MainViewModel> TabHost { get; }

  /// <summary>
  /// <see cref="TabHost"/>, typed as its XAML-friendly superclass.
  /// </summary>
  public TabHostViewModel TabHostBase => TabHost;

  /// <summary>
  /// Callback called when the application activates or deactivates
  /// </summary>
  /// <param name="showing"></param>
  internal void ApplicationShowing(bool showing)
  {
    if(showing)
    {
      ServerWidget.UpdateServerActiveBasic();
    }
  }

  /// <summary>
  /// Callback when the application closes. Cancels <see cref="AppAwakeToken"/>.
  /// </summary>
  internal void ApplicationClosing()
  {
    if(!_modelAwakeTokenSource.IsCancellationRequested)
    {
      _modelAwakeTokenSource.Cancel();
    }
  }
}
