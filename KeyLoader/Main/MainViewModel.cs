using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using FldVault.KeyServer;

using KeyLoader.Main.MasterVaults;
using KeyLoader.Main.ServerWidget;
using KeyLoader.Main.TaskTab;

using MahApps.Metro.Controls.Dialogs;

using Microsoft.Win32;

namespace KeyLoader.Main;

/// <summary>
/// The main application viewmodel
/// </summary>
public class MainViewModel: ObservableObject, IRecipient<CurrentTabChangedMessage>
{
  private readonly CancellationTokenSource _modelAwakeTokenSource;

  /// <summary>
  /// Create a new <see cref="MainViewModel"/>. Called as part of the bootstrapping
  /// process in App.xaml.cs. This demo application uses the "ViewModel first" approach.
  /// </summary>
  public MainViewModel()
  {
    Messenger = WeakReferenceMessenger.Default;
    _modelAwakeTokenSource = new CancellationTokenSource();
    AppAwakeToken = _modelAwakeTokenSource.Token;
    TabHost = new TabHostViewModel<MainViewModel>(Messenger, this);
    ServerWidget = new ServerWidgetViewModel(this);
    ExitCommand = new RelayCommand(() => {
      ApplicationClosing(); // One of two paths calling it. The other is in App.
      var w = Application.Current.MainWindow;
      w?.Close();
    });
    Messenger.Register<CurrentTabChangedMessage>(this);
    OpenMasterFileCommand = new RelayCommand(OpenExistingMasterFile);
    CreateMasterFileCommand = new RelayCommand(CreateNewMasterFile);
  }

  /// <summary>
  /// Command to bind to the "File|Exit" menu. Closes the main window and
  /// thus the application.
  /// </summary>
  public ICommand ExitCommand { get; }

  /// <summary>
  /// Command to open an existing master file
  /// </summary>
  public ICommand OpenMasterFileCommand { get; }

  /// <summary>
  /// Command to create a new master file
  /// </summary>
  public ICommand CreateMasterFileCommand { get; }

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
  /// The <see cref="IMessenger"/> service to use for loosely coupled messaging
  /// </summary>
  public IMessenger Messenger { get; }

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

  /// <summary>
  /// Implements <see cref="IRecipient{TMessage}"/> for <see cref="CurrentTabChangedMessage"/>.
  /// </summary>
  /// <param name="message"></param>
  public void Receive(CurrentTabChangedMessage message)
  {
    WindowTitle =
      String.IsNullOrEmpty(message.NewTab?.Title)
      ? __defaultWindowTitle
      : $"{message.NewTab.Title} - {__defaultWindowTitle}";
  }

  private static readonly Guid __masterFileDialogGuid = Guid.Parse("0D101CAF-1047-400D-8345-2667897FBBC0");

  /// <summary>
  /// Asks the user for an existing file name to open and opens it
  /// </summary>
  public void OpenExistingMasterFile()
  {
    var dialog = new OpenFileDialog() {
      Title = "Open existing master key file",
      Filter = "Master key files (*.mzvlt)|*.mzvlt",
      AddExtension = true,
      CheckFileExists = true,
      ClientGuid = __masterFileDialogGuid,
    };
    if(dialog.ShowDialog() == true)
    {
      var tab = MasterTabViewModel.OpenExisting(this, dialog.FileName);
      TabHost.CurrentTab = tab;
    }
  }

  /// <summary>
  /// Asks the user for a new file name and starts the process of creating it
  /// </summary>
  public void CreateNewMasterFile()
  {
    var dialog = new SaveFileDialog() {
      Title = "Select the name for a new master key file",
      Filter = "Master key files (*.mzvlt)|*.mzvlt",
      AddExtension = true,
      ClientGuid = __masterFileDialogGuid,
    };
    if(dialog.ShowDialog() == true)
    {
      var fileName = dialog.FileName;
      if(File.Exists(fileName))
      {
        MessageBox.Show(
          "A file with that name already exists",
          "Error",
          MessageBoxButton.OK,
          MessageBoxImage.Error);
      }
      else
      {
        var tab = MasterTabViewModel.CreateNew(this, fileName);
        TabHost.CurrentTab = tab;
      }
    }
  }
}
