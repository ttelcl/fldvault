/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto
{
  /// <summary>
  /// Utility implementing the inner parts of encrypting a single segment
  /// </summary>
  public class VaultSegmentEncryptor: IDisposable
  {
    private readonly NonceGenerator _nonceGenerator;
    private readonly byte[] _associatedData;
    private readonly byte[] _authenticationTag;
    private readonly AesGcm _cryptor;

    /// <summary>
    /// Create a new VaultSegmentEncryptor
    /// </summary>
    public VaultSegmentEncryptor(
      KeyBuffer key,
      NonceGenerator nonceGenerator,
      long totalLength, 
      DateTime stamp)
    {
      _nonceGenerator = nonceGenerator;
      _associatedData = new byte[16];
      _authenticationTag = new byte[16];
      Reset(totalLength, stamp);
      if(key.Bytes.Length != 32)
      {
        throw new ArgumentException(
          "Expecting a 256 bit key");
      }
      _cryptor = new AesGcm(key.Bytes);
    }

    /// <summary>
    /// Reset the associated data buffer to an initial state,
    /// enabling reuse of this object.
    /// </summary>
    public void Reset(long totalLength, DateTime stamp)
    {
      if(stamp.Kind != DateTimeKind.Utc)
      {
        throw new ArgumentException(
          "Expecting the time stamp to be in UTC");
      }
      BinaryPrimitives.WriteInt64LittleEndian(_associatedData.AsSpan().Slice(0, 8), totalLength);
      BinaryPrimitives.WriteInt64LittleEndian(_associatedData.AsSpan().Slice(8, 8), stamp.Ticks);
      _authenticationTag.AsSpan().Clear();
    }

    /// <summary>
    /// Get the most recently generated authentication tag
    /// </summary>
    public ReadOnlySpan<byte> AuthenticationTag { get =>  _authenticationTag; }

    /// <summary>
    /// A wrapper for AesGcm.Encrypt, representing some of its arguments differently.
    /// Here, the nonce is an output instead of an input, and the associated data
    /// is statefully provided by this class (not visible to the caller)
    /// </summary>
    public void Encrypt(
      ReadOnlySpan<byte> plaintext,
      Span<byte> ciphertext,
      Span<byte> nonce,
      Span<byte> authenticationtag)
    {
      if(plaintext.Length != ciphertext.Length)
      {
        throw new ArgumentException(
          "the plaintext and ciphertext buffers should be equal in size");
      }
      if(nonce.Length != 12)
      {
        throw new ArgumentException(
          "The nonce buffer must be 12 bytes in size");
      }
      if(authenticationtag.Length != 16)
      {
        throw new ArgumentException(
          "The authentication tag buffer must be 16 bytes in size");
      }
      _nonceGenerator.Next(nonce);
      _cryptor.Encrypt(nonce, plaintext, ciphertext, _authenticationTag, _associatedData);
      _authenticationTag.CopyTo(authenticationtag);
      _authenticationTag.AsSpan().CopyTo(_associatedData);
    }

    /// <summary>
    /// Clean up
    /// </summary>
    public void Dispose()
    {
      if(_cryptor != null)
      {
        _cryptor.Dispose();
      }
    }
  }

}
