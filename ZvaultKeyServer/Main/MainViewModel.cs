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
    ServerStatus = Server.ServerState;
  }

  public static void RegisterColors()
  {
    var cc = BrushCache.Default;
    cc.AddAlias("/Status/Fore/Unknown", "#CC808080");
    cc.AddAlias("/Status/Back/Unknown", "#28808080");
    cc.AddAlias("/Status/Fore/Seeded", "#EEDD9933");
    cc.AddAlias("/Status/Back/Seeded", "#28DD9933");
    cc.AddAlias("/Status/Fore/Hidden", "#EE6666DD");
    cc.AddAlias("/Status/Back/Hidden", "#286666DD");
    cc.AddAlias("/Status/Fore/Published", "#EE66CC44");
    cc.AddAlias("/Status/Back/Published", "#2866CC44");
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
}
