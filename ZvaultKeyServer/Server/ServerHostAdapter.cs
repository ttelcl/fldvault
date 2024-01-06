/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

using FldVault.Upi;

using ZvaultKeyServer.Main;
using ZvaultKeyServer.WpfUtilities;

namespace ZvaultKeyServer.Server;

public class ServerHostAdapter: IKeyServerHost
{
  /// <summary>
  /// Creates a new <see cref="ServerHostAdapter"/>.
  /// This should be called on the GUI thread
  /// </summary>
  /// <param name="mainModel">
  /// Entrypoint to the main UI state.
  /// </param>
  /// <param name="dispatcher">
  /// The dispatcher to use to redirect the callbacks,
  /// or null to use Dispatcher.CurrentDispatcher
  /// </param>
  public ServerHostAdapter(
    MainViewModel mainModel,
    Dispatcher? dispatcher = null)
  {
    Dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
    MainModel = mainModel;
  }

  /// <summary>
  /// A flag that if set, stops event processing, ignoring events.
  /// </summary>
  public bool Stopping { get; set; }

  public MainViewModel MainModel { get; }

  public Dispatcher Dispatcher { get; }

  public async Task KeyLoadRequest(
    IKeyServerUpi upi, Guid keyId, KeyStatus status, string? contextFile)
  {
    var now = DateTimeOffset.UtcNow;
    await Dispatcher.SwitchToUi();
    if(Stopping)
    {
      return;
    }
    Trace.TraceInformation(
      $"Callback: KeyLoadRequest {keyId} '{status}' ({contextFile??String.Empty})");
    var kvm = MainModel.KeysViewModel;
    var key = kvm.FindKey(keyId);
    if(key != null)
    {
      var needSort = key.SyncModel();
      key.ApplyGracePeriod();
      if(needSort)
      {
        kvm.Resort();
      }
    }
  }

  public async Task KeyStatusChanged(
    IKeyServerUpi upi, Guid keyId, KeyStatus status)
  {
    await Dispatcher.SwitchToUi();
    if(Stopping)
    {
      return;
    }
    Trace.TraceInformation($"Callback: KeyStatusChanged {keyId} '{status}'");
    var kvm = MainModel.KeysViewModel;
    kvm.SyncModel();
  }

  public async Task ServerStatusChanged(
    IKeyServerUpi upi, ServerStatus status)
  {
    await Dispatcher.SwitchToUi();
    if(Stopping)
    {
      return;
    }
    Trace.TraceInformation($"Callback: ServerStatusChanged '{status}'");
    MainModel.ServerStatus = status;
  }
}
