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

using FldVault.Upi;
using FldVault.Upi.Implementation;

using ZvaultKeyServer.Server;
using ZvaultKeyServer.WpfUtilities;

namespace ZvaultKeyServer.Main;

public class MainViewModel: ViewModelBase
{
  public MainViewModel(Dispatcher dispatcher)
  {
    HostAdapter = new ServerHostAdapter(this, dispatcher);
    Server = new KeyServerUpi(null);
    CheckServerStateCommand = new DelegateCommand(p => { CheckStatus(); });
    StartServerCommand = new DelegateCommand(p => { StartServer(); }, p => CanStartServer);
    StopServerCommand = new DelegateCommand(p => {  StopServer(); }, p => CanStopServer);
  }

  public ServerHostAdapter HostAdapter { get; }

  public KeyServerUpi Server { get; }

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
    catch (Exception ex)
    {
      Trace.TraceError($"Error starting server {ex}");
      StatusMessage = ex.Message;
    }
    ServerStatus = Server.ServerState;
  }

  public void StopServer()
  {
    Server.StopServer();
  }

  public bool CanStopServer => Server.ServerActive;

  public bool CanStartServer => Server.ServerState == ServerStatus.CanStart;
}
