using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;

using ZvaultKeyServer.Main;

namespace ZvaultKeyServer;

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
    Trace.TraceInformation($"App.App_Startup enter");
    var mainWindow = new MainWindow();
    //MainModel = new MainViewModel();
    //mainWindow.DataContext = MainModel;
    Trace.TraceInformation($"App.App_Startup showing main window");
    mainWindow.Show();
    Trace.TraceInformation($"App.App_Startup exit");
  }

  //public MainViewModel? MainModel { get; private set; }
}
