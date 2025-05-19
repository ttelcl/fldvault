/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using FldVault.Core.Crypto;
using FldVault.Upi;
using FldVault.Upi.Implementation;
using FldVault.Upi.Implementation.Keys;

using ZvaultKeyServer.Converters;
using ZvaultKeyServer.Main.Keys;
using ZvaultKeyServer.Server;
using ZvaultKeyServer.WpfUtilities;

namespace ZvaultKeyServer.Main;

public class MainViewModel: ViewModelBase, IStatusMessage
{
  private readonly DispatcherTimer _timer;

  public MainViewModel(
    Dispatcher dispatcher,
    KeyChain keyChain)
  {
    KeyChain = keyChain;
    KeyStates = new KeyStateStore(KeyChain);
    HostAdapter = new ServerHostAdapter(this, dispatcher);
    Server = new KeyServerUpi(KeyStates, null);
    KeysViewModel = new KeysViewModel(KeyStates, this);
    CheckServerStateCommand = new DelegateCommand(p => { CheckStatus(); });
    StartServerCommand = new DelegateCommand(p => { StartServer(); }, p => CanStartServer);
    StopServerCommand = new DelegateCommand(p => { StopServer(); }, p => CanStopServer);
    TryFixServerCommand = new DelegateCommand(p => {
      if(MessageBox.Show(
        "Are you sure?\n(this WILL crash the other server if there is one)",
        "Confirmation",
        MessageBoxButton.OKCancel,
        MessageBoxImage.Warning) == MessageBoxResult.OK)
      {
        if(Server.TryFixSocket())
        {
          ServerStatus = Server.ServerState;
          StatusMessage = "Unblocked server (use Server|Start to start this app's server)";
        }
        else
        {
          StatusMessage = "Failed to unblock server";
        }
      }
      else
      {
        StatusMessage = "Unblocking canceled";
      }
    }, p => Server.ServerState == ServerStatus.Blocked);
    _timer = new DispatcherTimer(DispatcherPriority.Background);
    _timer.Interval = TimeSpan.FromSeconds(1);
    _timer.Tick += TimerTick;
    _timer.Start();
    ServerStatus = Server.ServerState;
  }

  private void TimerTick(object? sender, EventArgs e)
  {
    KeysViewModel.TimerTick();
  }

  public static void RegisterColors()
  {
    /*
     * Olive = rgb(94, 115, 87) = #5e7357
     */
    var cc = BrushCache.Default;
    cc.AddAlias("/Status/Back/Unknown", "#28A0A0A0");
    cc.AddAlias("/Status/Back/Seeded", "#28DD9933");
    cc.AddAlias("/Status/Back/Hidden", "#288888EE");
    cc.AddAlias("/Status/Back/Published", "#2866CC44");
    cc.AddAlias("/Status/Fore/Unknown", "#EEA0A0A0");
    cc.AddAlias("/Status/Fore/Seeded", "#EEDD9933");
    cc.AddAlias("/Status/Fore/Hidden", "#EE8888EE");
    cc.AddAlias("/Status/Fore/Published", "#EE66CC44");
    cc.AddAlias("/Status/Full/Unknown", "#FFA0A0A0");
    cc.AddAlias("/Status/Full/Seeded", "#FFd29233");
    cc.AddAlias("/Status/Full/Hidden", "#FF6262d3");
    cc.AddAlias("/Status/Full/Published", "#FF62c342");
  }

  public ServerHostAdapter HostAdapter { get; }

  public KeyServerUpi Server { get; }

  public KeyChain KeyChain { get; }

  public KeyStateStore KeyStates { get; }

  public KeysViewModel KeysViewModel { get; }

  public ICommand ExitCommand { get; } = new DelegateCommand(p => {
    var w = Application.Current.MainWindow;
    w?.Close();
  });

  public ICommand CheckServerStateCommand { get; }

  public ICommand StartServerCommand { get; }

  public ICommand StopServerCommand { get; }

  public ICommand TryFixServerCommand { get; }

  public string StatusMessage {
    get => _statusMessage;
    set {
      if(SetInstanceProperty(ref _statusMessage, value))
      {
      }
    }
  }
  private string _statusMessage = "Welcome";

  public ServerStatus ServerStatus {
    get => _status;
    set {
      if(SetValueProperty(ref _status, value))
      {
        RaisePropertyChanged(nameof(ServerStatusText));
        Trace.TraceInformation($"Server Status now is '{_status}'");
      }
    }
  }
  private ServerStatus _status = ServerStatus.Blocked;

  public string ServerStatusText {
    get => _status.ToString();
  }

  public void OnClosing(CancelEventArgs e)
  {
    if(Server.ServerActive)
    {
      HostAdapter.Stopping = true;
      Trace.TraceWarning("Last-ditch attempt to stop the server");
      Server.WaitForServerStop(1500);
    }
    Server.Dispose(); // that's merely a server kill
  }

  public void CheckStatus()
  {
    ServerStatus = Server.ServerState;
  }

  public async void StartServer()
  {
    Trace.TraceInformation("Starting server.");
    StatusMessage = "Starting server";
    try
    {
      await Server.StartServer(HostAdapter);
      StatusMessage = "Server started";
    }
    catch(Exception ex)
    {
      Trace.TraceError($"Error starting server {ex}");
      StatusMessage = ex.Message;
    }
    await Server.SyncServerStatus(HostAdapter, ServerStatus);
  }

  public async void StopServer()
  {
    Trace.TraceInformation("Stopping server.");
    StatusMessage = "Stopping server";
    Server.StopServer();
    if(Server.WaitForServerStop(1000))
    {
      Trace.TraceInformation("Server successfully stopped");
      StatusMessage = "Server stopped";
    }
    else
    {
      Trace.TraceInformation("Server stop timed out");
      StatusMessage = "Server stop FAILED";
    }
    await Server.SyncServerStatus(HostAdapter, ServerStatus);
  }

  public bool CanStopServer => Server.ServerActive;

  public bool CanStartServer => Server.ServerState == ServerStatus.CanStart;

  public void ApplicationShowing(bool showing)
  {
    KeysViewModel.ApplicationShowing(showing);
  }
}
