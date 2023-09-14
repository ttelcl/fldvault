/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Upi;

/// <summary>
/// Key server UPI interface. A newly created instance implementing this interface
/// is initially not running the server (but does allocate the shared state that
/// the server requires)
/// </summary>
public interface IKeyServerUpi: IDisposable
{
  /// <summary>
  /// Start the server thread. Can only be successfully called if 
  /// <see cref="ServerState"/> is <see cref="ServerStatus.CanStart"/>.
  /// </summary>
  /// <param name="hostCallbacks">
  /// The interface containing the host callbacks. Host callbacks are called on
  /// the server thread.
  /// </param>
  /// <returns>
  /// The new server status (<see cref="ServerStatus.Running"/> if successful)
  /// </returns>
  ServerStatus StartServer(IKeyServerHost hostCallbacks);

  /// <summary>
  /// Retrieve the current server status
  /// </summary>
  ServerStatus ServerState { get; }

  /// <summary>
  /// Request the server to stop if it was running. This call does not wait
  /// for the termination to complete, it just initiates the termination.
  /// </summary>
  void StopServer();

  /// <summary>
  /// Wait for the server stop request to complete.
  /// If necessary this call includes a call to <see cref="StopServer"/>.
  /// </summary>
  /// <param name="timeout">
  /// The maximum time in milliseconds to wait for the stop procedure to complete
  /// </param>
  /// <returns>
  /// True on success, false on timeout.
  /// </returns>
  bool WaitForServerStop(int timeout);

  /// <summary>
  /// Return Key information for all keys that the key server is aware of.
  /// </summary>
  IReadOnlyList<IKeyInfo> ListKeys();

  /// <summary>
  /// Retrieve the status of the key in the server
  /// </summary>
  /// <param name="keyId">
  /// The ID of the key to retrieve the status of.
  /// </param>
  /// <returns></returns>
  KeyStatus GetKeyStatus(Guid keyId);

  /// <summary>
  /// Try to unlock the identified key with the given passphrase
  /// </summary>
  /// <param name="keyId">
  /// The ID of the key to unlock
  /// </param>
  /// <param name="passphrase">
  /// The passphrase to try
  /// </param>
  /// <param name="publish">
  /// True to put the unlocked key in the Published state upon successful unlock,
  /// False to put it in the WithHeld state instead. If false is passed
  /// and the key was already published this is ignored. 
  /// </param>
  /// <returns>
  /// The new status or failure reason. <see cref="KeyStatus.Unknown"/> indicates
  /// the key is not known or cannot be unlocked with a passphrase.
  /// <see cref="KeyStatus.Seeded"/> indicates the passphrase was wrong.
  /// <see cref="KeyStatus.WithHeld"/> or <see cref="KeyStatus.Published"/> indicate
  /// either a successful unlock, or that the key was already unlocked.
  /// </returns>
  KeyStatus TryUnlockKey(Guid keyId, SecureString passphrase, bool publish);

  /// <summary>
  /// Try to change the status of a key. The precise effect depends on the
  /// <paramref name="status"/> value and the current status. This is primarily
  /// intended to swap between <see cref="KeyStatus.WithHeld"/> and 
  /// <see cref="KeyStatus.Published"/>, but can also be used to re-lock the
  /// key (forget the actual key value) via <see cref="KeyStatus.Seeded"/> or
  /// even completely forget the key via <see cref="KeyStatus.Unknown"/>.
  /// </summary>
  /// <param name="keyId">
  /// The id of the key to change.
  /// </param>
  /// <param name="status">
  /// The intended new status
  /// </param>
  /// <returns>
  /// The actual new status
  /// </returns>
  KeyStatus ChangeStatus(Guid keyId, KeyStatus status);

  /// <summary>
  /// In case the key is unknown, try to find a key seed for it based
  /// on the given target file. In case the key is already known,
  /// this method is mostly a NOP (but still may associate the file with the key)
  /// </summary>
  /// <param name="keyId">
  /// The key ID of the key to find
  /// </param>
  /// <param name="targetFile">
  /// The target *.zvlt or *.key-info file that is expected to provide
  /// the key seed information for the key.
  /// </param>
  /// <returns>
  /// The key status afterward. <see cref="KeyStatus.Unknown"/> indicates
  /// failure.
  /// </returns>
  KeyStatus PrepareKey(Guid keyId, string targetFile);

  /// <summary>
  /// Check if the given file is a ZVault file, and if it is return some information
  /// about it.
  /// </summary>
  /// <param name="vaultFile">
  /// The name of the file to check. Normally the extension would be *.zvlt, but
  /// that is not a requirement.
  /// </param>
  /// <returns>
  /// Null if the file is not recognized, a vault decriptor otherwise
  /// </returns>
  VaultInfo? IsVault(string vaultFile);
}
