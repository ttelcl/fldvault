using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

using FldVault.KeyServer;

namespace KeyLoader.Main.ServerWidget;

/// <summary>
/// Represents the connection to the key server
/// </summary>
public sealed class ServerWidgetViewModel : ObservableObject
{

  /// <summary>
  /// Create a new <see cref="ServerWidgetViewModel"/> instance
  /// </summary>
  /// <param name="owner">
  /// The <see cref="MainViewModel"/> owning this <see cref="ServerWidgetViewModel"/>.
  /// </param>
  public ServerWidgetViewModel(MainViewModel owner)
  {
    Owner = owner;
    Server = new KeyServerService();
    UpdateServerActiveBasic();
  }

  /// <summary>
  /// The owning <see cref="MainViewModel"/>.
  /// </summary>
  public MainViewModel Owner { get; }
  
  /// <summary>
  /// The <see cref="KeyServerService"/> instance wrapped by this widget
  /// </summary>
  public KeyServerService Server { get; }

  /// <summary>
  /// The <see cref="CancellationToken"/> that gets canceled upon application shutdown.
  /// This is the token that gets used for all async operations.
  /// </summary>
  internal CancellationToken AppCancelationToken => Owner.AppAwakeToken;

  /// <summary>
  /// Whether or not the server appears to be active. This is a cached value
  /// of the latest server activity test result
  /// </summary>
  public bool ServerActive {
    get => _serverActive;
    private set {
      if(SetProperty(ref _serverActive, value))
      {
        OnServerActiveChanged();
      }
    }
  }
  private bool _serverActive;

  /// <summary>
  /// Simplified and synchronous server activity check. Works most of the time,
  /// and avoids asynchronous communication complexities. For full reliability,
  /// rely on the actual server communication methods.
  /// </summary>
  public void UpdateServerActiveBasic()
  {
    ServerActive = Server.ServerAvailable;
  }
  
  private void OnServerActiveChanged()
  {
    var active = ServerActive;
    if(active)
    {
      Trace.TraceInformation("Key Server is now 'active'");
    }
    else
    {
      Trace.TraceInformation("Key Server is now 'inactive'");
    }
  }
}
