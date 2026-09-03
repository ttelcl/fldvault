using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;

using ControlzEx.Theming;

using KeyLoader.Converters;
using KeyLoader.Main;

namespace KeyLoader;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App: Application
{
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
    MainModel = new MainViewModel();
    var mainWindow = new MainWindow() {
      DataContext = MainModel,
    };
    InitializePrefixColors();
    Trace.TraceInformation($"App.App_Startup showing main window");
    mainWindow.Show();
    Trace.TraceInformation($"App.App_Startup done");
  }

  /// <summary>
  /// The main view model for the app
  /// </summary>
  public MainViewModel? MainModel { get; private set; }

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

  private void Application_Exit(object sender, ExitEventArgs e)
  {
    Trace.TraceInformation("Application_Exit: Cleanup");
    MainModel?.ApplicationClosing(); // in case that wasn't called already
  }

  private void Application_Activated(object sender, EventArgs e)
  {
    Trace.TraceInformation("Application_Activated");
    MainModel?.ApplicationShowing(true);
  }

  private void Application_Deactivated(object sender, EventArgs e)
  {
    Trace.TraceInformation("Application_Deactivated");
    MainModel?.ApplicationShowing(false);
  }

  private void InitializePrefixColors()
  {
    var cache = BrushCache.Default;
    cache.AddAlias("/TrueGreenFalseRed/True", "#00ff00");
    cache.AddAlias("/TrueGreenFalseRed/False", "#ff0000");
  }
}

