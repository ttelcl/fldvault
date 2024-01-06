/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ZvaultKeyServer.Main.Keys;

/// <summary>
/// The various states of the auto-hide logic
/// </summary>
public enum AutohideState
{
  /// <summary>
  /// The key is not loaded, so there is nothing to (auto-)hide, nor
  /// any process to enable or disable.
  /// </summary>
  Inactive,

  /// <summary>
  /// Auto hiding is disabled. The key is loaded and published.
  /// </summary>
  Disabled,

  /// <summary>
  /// Auto hiding is enabled and would be counting down if the countdown
  /// hadn't been paused.
  /// The key is loaded and published.
  /// </summary>
  Paused,

  /// <summary>
  /// Auto hiding is enabled and counting down. The countdown is not finished yet.
  /// The key is loaded and published.
  /// </summary>
  Running,

  /// <summary>
  /// Auto hiding is enabled and has run to the end, but the model is not aware of that yet
  /// The key is loaded and should be hidden, but isn't.
  /// This is a transitional state that ideally doesn't actually happen.
  /// </summary>
  Hiding,

  /// <summary>
  /// Auto hiding is enabled and has run to the end.
  /// The key is loaded but hidden.
  /// </summary>
  Hidden,

}
