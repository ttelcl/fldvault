/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ZvaultKeyServer.WpfUtilities;

// Inspired by
// https://medium.com/criteo-engineering/switching-back-to-the-ui-thread-in-wpf-uwp-in-modern-c-5dc1cc8efa5e

public static class DispatcherExtensions
{
  /// <summary>
  /// Await the returned object to switch to the dispatcher's thread
  /// </summary>
  /// <returns>
  /// An object you can await to switch to the thread associated with
  /// <paramref name="dispatcher"/>.
  /// </returns>
  public static SwitchToUiAwaitable SwitchToUi(this Dispatcher dispatcher)
  {
    return new SwitchToUiAwaitable(dispatcher);
  }

  /// <summary>
  /// Await the returned object to switch to the thread for the dispatcher
  /// associated with the object
  /// </summary>
  /// <param name="dispatcherObject">
  /// An object deriving from <see cref="DispatcherObject"/>. Most WPF objects
  /// fit that description.
  /// </param>
  /// <returns>
  /// An object you can await to switch to the thread associated with
  /// <paramref name="dispatcherObject"/>.
  /// </returns>
  public static SwitchToUiAwaitable SwitchToUi(this DispatcherObject dispatcherObject)
  {
    return new SwitchToUiAwaitable(dispatcherObject.Dispatcher);
  }

  public readonly struct SwitchToUiAwaitable: INotifyCompletion
  {
    private readonly Dispatcher _dispatcher;

    public SwitchToUiAwaitable(Dispatcher dispatcher)
    {
      _dispatcher = dispatcher;
    }

    public SwitchToUiAwaitable GetAwaiter()
    {
      return this;
    }

    public void GetResult()
    {
    }

    public bool IsCompleted => _dispatcher.CheckAccess();

    public void OnCompleted(Action continuation)
    {
      _dispatcher.BeginInvoke(continuation);
    }
  }
}