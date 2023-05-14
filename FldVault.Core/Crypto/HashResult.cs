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

namespace FldVault.Core.Crypto
{
  /// <summary>
  /// Utility class for storing the result of applying a cryptographic hash
  /// algorithm
  /// </summary>
  public class HashResult
  {
    private readonly byte[] _hash;

    /// <summary>
    /// Create a new HashResult
    /// </summary>
    public HashResult(ReadOnlySpan<byte> hash)
    {
      _hash = hash.ToArray();
    }

    /// <summary>
    /// Calculates the SHA256 hash of the provided bytes and stores the result
    /// in a new HashResult instance
    /// </summary>
    public static HashResult FromSha256(ReadOnlySpan<byte> bytesToHash)
    { 
      return new HashResult(SHA256.HashData(bytesToHash));
    }

    /// <summary>
    /// Calculates the SHA256 hash of the bytes in the buffer and stores the result
    /// in a new HashResult instance
    /// </summary>
    public static HashResult FromSha256(CryptoBuffer<byte> bufferToHash)
    {
      return FromSha256(bufferToHash.Span());
    }

    /// <summary>
    /// Return the stored hash
    /// </summary>
    public ReadOnlySpan<byte> HashBytes { get => _hash; }

    /// <summary>
    /// Derive a type 4 GUID from the first 16 bytes of the stored hash
    /// </summary>
    public Guid AsGuid { get => Conversions.BytesToGuid(_hash.AsSpan(0, 16)); }

  }
}
