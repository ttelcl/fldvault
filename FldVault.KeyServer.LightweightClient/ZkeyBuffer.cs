/*
 * (c) 2026  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.KeyServer.LightweightClient;

/// <summary>
/// Buffers a ZKEY style raw key. Disposing it clears the buffer bytes.
/// </summary>
public class ZkeyBuffer: IDisposable
{
  private readonly byte[] _key;
  private bool _disposed;
  private bool _idIsValid;
  private Guid _id;

  /// <summary>
  /// Create a new ZkeyBuffer
  /// </summary>
  public ZkeyBuffer()
  {
    _key = new byte[LightweightClientHelpers.KeyLength];
    _idIsValid = false;
  }

  /// <summary>
  /// The ID of the currently loaded key. Calculated upon first access
  /// </summary>
  public Guid KeyId {
    get {
      ObjectDisposedException.ThrowIf(_disposed, this);
      if(!_idIsValid)
      {
        RecalculateId();
      }
      return _id;
    }
  }

  /// <summary>
  /// Get a read-only view on the currently loaded key.
  /// </summary>
  public ReadOnlySpan<byte> Key {
    get {
      ObjectDisposedException.ThrowIf(_disposed, this);
      return _key;
    }
  }

  /// <summary>
  /// Copy the given raw key bytes into this buffer
  /// </summary>
  /// <param name="key">
  /// The 32 byte key to copy.
  /// </param>
  public void LoadFrom(ReadOnlySpan<byte> key)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    if(key.Length != LightweightClientHelpers.KeyLength)
    {
      throw new ArgumentOutOfRangeException(nameof(key));
    }
    key.CopyTo(_key);
    _idIsValid = false;
  }

  /// <summary>
  /// Load this buffer from a ZKEY style binary passphrase and salt
  /// </summary>
  /// <param name="passphraseBytes">
  /// The bytes of the binary passphrase
  /// </param>
  /// <param name="salt">
  /// The random salt bytes for this key (length <see cref="LightweightClientHelpers.SaltLength"/>)
  /// </param>
  public void LoadFrom(
    ReadOnlySpan<byte> passphraseBytes,
    ReadOnlySpan<byte> salt)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    LightweightClientHelpers.PassphraseToKey(passphraseBytes, salt, _key);
    _idIsValid = false;
    RecalculateId();
  }

  /// <summary>
  /// Load this buffer from a ZKEY style passphrase (as character span) and salt
  /// </summary>
  /// <param name="passphraseCharacters">
  /// The characters of the passphrase
  /// </param>
  /// <param name="salt">
  /// The random salt bytes for this key (length <see cref="LightweightClientHelpers.SaltLength"/>)
  /// </param>
  public void LoadFrom(
    ReadOnlySpan<char> passphraseCharacters,
    ReadOnlySpan<byte> salt)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    LightweightClientHelpers.PassphraseToKey(passphraseCharacters, salt, _key);
    _idIsValid = false;
    RecalculateId();
  }

  /// <summary>
  /// Load this buffer from a ZKEY style passphrase (as <see cref="SecureString"/>) and salt
  /// </summary>
  /// <param name="passphrase">
  /// The passphrase, as a <see cref="SecureString"/>.
  /// </param>
  /// <param name="salt">
  /// The random salt bytes for this key (length <see cref="LightweightClientHelpers.SaltLength"/>)
  /// </param>
  public void LoadFrom(
    SecureString passphrase,
    ReadOnlySpan<byte> salt)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    LightweightClientHelpers.PassphraseToKey(passphrase, salt, _key);
    _idIsValid = false;
    RecalculateId();
  }

  private void RecalculateId()
  {
    if(!_idIsValid)
    {
      _id = LightweightClientHelpers.KeyGuid(_key);
      _idIsValid = true;
    }
  }

  /// <summary>
  /// Clear the buffer and mark this object as disposed.
  /// </summary>
  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      Array.Clear(_key);
      GC.SuppressFinalize(this);
    }
  }
}
