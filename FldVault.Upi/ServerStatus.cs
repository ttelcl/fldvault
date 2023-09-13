/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Upi;

/// <summary>
/// The status of the embedded server
/// </summary>
public enum ServerStatus
{
  /// <summary>
  /// The server is not running and can not be started (because another server is running)
  /// </summary>
  Blocked,

  /// <summary>
  /// The server is not running but can be started
  /// </summary>
  CanStart,

  /// <summary>
  /// The server is running
  /// </summary>
  Running,

  /// <summary>
  /// The server has been requested to stop, but has not completed stopping
  /// </summary>
  Stopping,

}
