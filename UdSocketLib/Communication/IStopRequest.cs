/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UdSocketLib.Communication;

/// <summary>
/// An object that tracks a request to stop an operation.
/// </summary>
public interface IStopRequest
{
  /// <summary>
  /// Returns true if stopping was requested
  /// </summary>
  bool StopRequested { get; }

  /// <summary>
  /// Change the <see cref="StopRequested"/> flag to true.
  /// Once true the flag cannot be reset.
  /// </summary>
  void RequestStop();
}

