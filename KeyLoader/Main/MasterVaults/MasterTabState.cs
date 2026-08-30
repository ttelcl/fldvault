using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLoader.Main.MasterVaults;

/// <summary>
/// The various states a master vault tab can be in
/// </summary>
public enum MasterTabState
{
  /// <summary>
  /// Undefined state
  /// </summary>
  Panic = 0,

  /// <summary>
  /// Creating a key for a new master vault, first entry
  /// (expected next state: <see cref="ConfirmingKey"/>)
  /// </summary>
  CreatingKey,

  /// <summary>
  /// Confirming the new master vault key
  /// (expected next state: <see cref="Editing"/>)
  /// </summary>
  ConfirmingKey,

  /// <summary>
  /// Waiting for the master key of an existing file
  /// (expected next state: <see cref="UsingMaster"/>)
  /// </summary>
  AwaitingKey,

  /// <summary>
  /// Using the master key file content (sending selected or all child keys
  /// to server)
  /// (expected next state: <see cref="Editing"/>, or close)
  /// </summary>
  UsingMaster,

  /// <summary>
  /// Editing an existing or new master key file
  /// (expected next state: <see cref="UsingMaster"/>, after save. or save and close)
  /// </summary>
  Editing,

  /// <summary>
  /// Closed. No more interactions
  /// </summary>
  Closed,
}
