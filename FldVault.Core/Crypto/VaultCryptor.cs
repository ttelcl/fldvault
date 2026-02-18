/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto;

/// <summary>
/// Provides an opiniated layer on top of <see cref="AesGcm"/>
/// for encryption and descryption.
/// </summary>
public class VaultCryptor: IDisposable
{
  /// <summary>
  /// Access safely via the Cryptor property
  /// </summary>
  private AesGcm? _aesgcm;

  /// <summary>
  /// Create a new VaultEncryptor. Normally invoked by
  /// <see cref="FldVault.Core.Zvlt2.VaultFile.CreateCryptor(KeyChain, NonceGenerator?)"/>
  /// </summary>
  /// <param name="keySource">
  /// The key chain providing the encryption key
  /// </param>
  /// <param name="keyId">
  /// The ID of the encryption key
  /// </param>
  /// <param name="vaultStamp">
  /// The time stamp of the vault
  /// </param>
  /// <param name="nonceGenerator">
  /// The nonce generator. If null, a new nonce generator instance is created.
  /// </param>
  /// <exception cref="ArgumentException"></exception>
  internal VaultCryptor(
    KeyChain keySource,
    Guid keyId,
    DateTime vaultStamp,
    NonceGenerator? nonceGenerator = null)
  {
    if(vaultStamp.Kind != DateTimeKind.Utc)
    {
      throw new ArgumentException("Expecting a UTC timestamp");
    }
    KeyId = keyId;
    VaultStamp = vaultStamp;
    NonceGenerator = nonceGenerator ?? new NonceGenerator();
    _aesgcm =
      keySource.TryMapKey(keyId, (kid, ibw) => new AesGcm(ibw.Bytes, 16))
      ?? throw new ArgumentException("The key was not found in the chain");
  }

  /// <summary>
  /// The ID of the key used in the associated vault
  /// </summary>
  public Guid KeyId { get; init; }

  /// <summary>
  /// The timestamp associated with the vault
  /// </summary>
  public DateTime VaultStamp { get; init; }

  /// <summary>
  /// The nonce generator
  /// </summary>
  public NonceGenerator NonceGenerator { get; init; }

  /// <summary>
  /// Wraps <see cref="AesGcm.Encrypt(ReadOnlySpan{byte}, ReadOnlySpan{byte}, Span{byte}, Span{byte}, ReadOnlySpan{byte})"/>
  /// adding additional checks, changing the nonce to be an output instead of an input,
  /// and changing the order of arguments.
  /// </summary>
  /// <param name="associatedData">
  /// The additional data that will be included in the calculated authentication tag
  /// and must be presented to the decryption
  /// </param>
  /// <param name="plainText">
  /// The content to be encrypted
  /// </param>
  /// <param name="cipherText">
  /// The buffer to receive the encrypted output (same size as <paramref name="plainText"/>)
  /// </param>
  /// <param name="nonce">
  /// The 12 byte buffer that will receive the generated nonce (note that this is an output,
  /// not an input like it is in the underlying method of <see cref="AesGcm"/>)
  /// </param>
  /// <param name="tag">
  /// The 16 byte buffer that will receive the calculated authentication tag
  /// </param>
  public void Encrypt(
    ReadOnlySpan<byte> associatedData,
    ReadOnlySpan<byte> plainText,
    Span<byte> cipherText,
    Span<byte> nonce,
    Span<byte> tag)
  {
    if(nonce.Length != 12)
    {
      throw new ArgumentOutOfRangeException(nameof(nonce), "Expecting the nonce buffer to be 12 bytes");
    }
    if(tag.Length != 16)
    {
      throw new ArgumentOutOfRangeException(nameof(nonce), "Expecting the tag buffer to be 16 bytes");
    }
    NonceGenerator.Next(nonce);
    Cryptor.Encrypt(nonce, plainText, cipherText, tag, associatedData);
  }

  /// <summary>
  /// Wraps <see cref="AesGcm.Decrypt(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, Span{byte}, ReadOnlySpan{byte})"/>
  /// adding additional checks and changing the order of arguments.
  /// </summary>
  /// <param name="associatedData">
  /// The additional data that was provided to the decryption.
  /// </param>
  /// <param name="nonce">
  /// The 12 byte nonce generated during encryption
  /// </param>
  /// <param name="tag">
  /// The 16 byte authentication tag calculated during encryption
  /// </param>
  /// <param name="cipherText">
  /// The buffer holding the ciphertext to be decrypted
  /// </param>
  /// <param name="plainText">
  /// The buffer to receive the decrypted plaintext (same size as <paramref name="cipherText"/>)
  /// </param>
  /// <exception cref="ArgumentOutOfRangeException"></exception>
  public void Decrypt(
    ReadOnlySpan<byte> associatedData,
    ReadOnlySpan<byte> nonce,
    ReadOnlySpan<byte> tag,
    ReadOnlySpan<byte> cipherText,
    Span<byte> plainText)
  {
    if(nonce.Length != 12)
    {
      throw new ArgumentOutOfRangeException(nameof(nonce), "Expecting the nonce buffer to be 12 bytes");
    }
    if(tag.Length != 16)
    {
      throw new ArgumentOutOfRangeException(nameof(nonce), "Expecting the tag buffer to be 16 bytes");
    }
    Cryptor.Decrypt(nonce, cipherText, tag, plainText, associatedData);
  }

  private AesGcm Cryptor {
    get {
      ObjectDisposedException.ThrowIf(_aesgcm == null, typeof(AesGcm));
      return _aesgcm;
    }
  }

  /// <summary>
  /// Clean up
  /// </summary>
  public void Dispose()
  {
    if(_aesgcm != null)
    {
      _aesgcm.Dispose();
      _aesgcm = null;
    }
  }
}
