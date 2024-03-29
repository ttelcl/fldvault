﻿/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FldVault.Upi;

/// <summary>
/// Callback interface for the key server to notify the host
/// user interface of events. Note that all callbacks are made
/// on the server thread, so the implementation must be thread
/// aware.
/// All notifications are async to make thread transition easier
/// to implement.
/// </summary>
public interface IKeyServerHost
{
  /// <summary>
  /// Indicates that the server status has changed. Note that a change
  /// from <see cref="ServerStatus.CanStart"/> to <see cref="ServerStatus.Blocked"/>
  /// does not cause a this callback.
  /// </summary>
  /// <param name="upi">
  /// The server interface
  /// </param>
  /// <param name="status">
  /// The new status
  /// </param>
  Task ServerStatusChanged(IKeyServerUpi upi, ServerStatus status);

  /// <summary>
  /// Indicates that the status of a key has changed.
  /// </summary>
  /// <param name="upi">
  /// The server interface
  /// </param>
  /// <param name="keyId">
  /// The ID of the key that changed status
  /// </param>
  /// <param name="status">
  /// The new status.
  /// </param>
  Task KeyStatusChanged(IKeyServerUpi upi, Guid keyId, KeyStatus status);

  /// <summary>
  /// Indicates that the server received a request to publish a key or served a key
  /// </summary>
  /// <param name="upi">
  /// The server interface
  /// </param>
  /// <param name="keyId">
  /// The ID of the requested key
  /// </param>
  /// <param name="status">
  /// The current status of the key (useful to distinguish an unlock request from
  /// a reactivate request from a serve notification)
  /// </param>
  /// <param name="contextFile">
  /// If not null: the name of a file that uses the requested key. Useful for UI
  /// feedback and possibly for finding a seed for the key.
  /// </param>
  Task KeyLoadRequest(IKeyServerUpi upi, Guid keyId, KeyStatus status, string? contextFile);
}

