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
/// Initialization of the embedded key server
/// </summary>
public interface IKeyServerFactory
{
  /// <summary>
  /// Start the embedded key server.
  /// </summary>
  /// <param name="host">
  /// The host interface (implemented by the caller of this method).
  /// This interface must be ready to receive callbacks from any thread.
  /// </param>
  /// <param name="socketName">
  /// An optional alternative key server socket name.
  /// </param>
  /// <returns>
  /// The interface for communicating from the key server host to the
  /// created key server.
  /// </returns>
  IKeyServerUpi StartServer(IKeyServerHost host, string? socketName = null);
}
