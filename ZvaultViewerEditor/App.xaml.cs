using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;

using ControlzEx.Theming;

using FldVault.Core.Crypto;

using ZvaultViewerEditor.Main;

namespace ZvaultViewerEditor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App: Application
{
  private readonly KeyChain _keyChain = new KeyChain();

  /// <summary>
  /// Instead of using a Startup Uri, create the window manually.
  /// This method is referenced in the header of app.xaml instead of
  /// a startup URI.
  /// </summary>
  private void App_Startup(object sender, StartupEventArgs e)
  {
    DispatcherUnhandledException += (s, e) =>
      ProcessUnhandledException(e);
    Trace.TraceInformation($"App.App_Startup enter");
    ThemeManager.Current.ChangeTheme(this, "Dark.Olive");
    var mainWindow = new MainWindow();
    MainModel = new MainViewModel(_keyChain);
    foreach(var arg in e.Args)
    {
      if(arg.EndsWith(".zvlt"))
      {
        MainModel.TryOpenVault(arg);
        break;
      }
    }
    mainWindow.DataContext = MainModel;
    Trace.TraceInformation($"App.App_Startup showing main window");
    mainWindow.Show();
    Trace.TraceInformation($"App.App_Startup exit");
  }

  public MainViewModel? MainModel { get; private set; }

  private void Application_Exit(object sender, ExitEventArgs e)
  {
    Trace.TraceInformation("Application_Exit: Destroying keychain");
    // zero out memory that is holding keys
    _keyChain.Dispose();
  }

  private void ProcessUnhandledException(
    System.Windows.Threading.DispatcherUnhandledExceptionEventArgs evt)
  {
    var ex = evt.Exception;
    Trace.TraceError($"Error: {ex}");
    MessageBox.Show(
      $"{ex.GetType().FullName}\n{ex.Message}",
      "Error",
      MessageBoxButton.OK,
      MessageBoxImage.Error);
    evt.Handled = MainWindow?.IsLoaded ?? false;
  }

}
