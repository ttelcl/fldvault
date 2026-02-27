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
public sealed class ZkeyBuffer: IDisposable
{
  private readonly byte[] _key;
  private readonly Guid _id;
  private bool _disposed;

  /// <summary>
  /// Create a new <see cref="ZkeyBuffer"/> containing the given raw key.
  /// </summary>
  /// <param name="key">
  /// The raw key, 32 bytes long. This array is stored as-is, it is not copied
  /// (which is fine for a _private_ constructor, but callers need to be aware of it).
  /// </param>
  private ZkeyBuffer(byte[] key)
  {
    if(key.Length != LightweightClientHelpers.KeyLength)
    {
      throw new ArgumentOutOfRangeException(nameof(key));
    }
    _key = key;
    _id = LightweightClientHelpers.KeyGuid(_key);
  }

  /// <summary>
  /// The ID of the currently loaded key. Calculated upon first access
  /// </summary>
  public Guid KeyId {
    get {
      ObjectDisposedException.ThrowIf(_disposed, this);
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
  public static ZkeyBuffer FromRaw(ReadOnlySpan<byte> key)
  {
    if(key.Length != LightweightClientHelpers.KeyLength)
    {
      throw new ArgumentOutOfRangeException(nameof(key));
    }
    return new ZkeyBuffer(key.ToArray());
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
  public static ZkeyBuffer FromPassphraseBytes(
    ReadOnlySpan<byte> passphraseBytes,
    ReadOnlySpan<byte> salt)
  {
    var key = new byte[LightweightClientHelpers.KeyLength];
    LightweightClientHelpers.PassphraseToKey(passphraseBytes, salt, key);
    return new ZkeyBuffer(key);
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
  public static ZkeyBuffer FromPassphraseCharacters(
    ReadOnlySpan<char> passphraseCharacters,
    ReadOnlySpan<byte> salt)
  {
    var key = new byte[LightweightClientHelpers.KeyLength];
    LightweightClientHelpers.PassphraseToKey(passphraseCharacters, salt, key);
    return new ZkeyBuffer(key);
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
  public static ZkeyBuffer FromSecurePassphrase(
    SecureString passphrase,
    ReadOnlySpan<byte> salt)
  {
    var key = new byte[LightweightClientHelpers.KeyLength];
    LightweightClientHelpers.PassphraseToKey(passphrase, salt, key);
    return new ZkeyBuffer(key);
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
