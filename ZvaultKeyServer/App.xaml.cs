﻿using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;

using FldVault.Core.Crypto;

using ZvaultKeyServer.Main;

namespace ZvaultKeyServer;

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
    Trace.TraceInformation($"App.App_Startup enter");
    MainViewModel.RegisterColors();
    var mainWindow = new MainWindow();
    MainModel = new MainViewModel(mainWindow.Dispatcher, _keyChain);
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
  private void Application_Activated(object sender, EventArgs e)
  {
    MainModel?.ApplicationShowing(true);
  }

  private void Application_Deactivated(object sender, EventArgs e)
  {
    MainModel?.ApplicationShowing(false);
  }
}
