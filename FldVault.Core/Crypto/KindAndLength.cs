/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto
{
  /// <summary>
  /// A struct that packs a two byte "kind" and a six byte "length" field
  /// into one long integer. The LSB of the "kind" doubles as "secret"
  /// flag
  /// </summary>
  public struct KindAndLength
  {
    private readonly long _value;

    /// <summary>
    /// Create a new KindAndLength
    /// </summary>
    public KindAndLength(
      short kind,
      long length)
    {
      if(kind < 0)
      {
        throw new ArgumentOutOfRangeException(
          nameof(kind), "Expecting a non-negative value for 'kind'");
      }
      if(length < 0 || length > 0x00010000_00000000L)
      {
        throw new ArgumentOutOfRangeException(
          nameof(length), "Expecting length to be non-negative and less than 2^48");
      }
      if((kind & 1)!=0 && length > 0x40000)
      {
        throw new ArgumentException(
          "For odd (secret) kinds, the length must be no more than 256Kb");
      }
      _value = length | ((long)kind<<48);
    }

    /// <summary>
    /// The kind indicator. Odd kinds indicate "secret" segments that must
    /// not decrypted to persistent storage, only to memory
    /// </summary>
    public short Kind { get => (short)(_value >> 48); }

    /// <summary>
    /// True to indicate a segment that must only be unpacked to memory,
    /// never to a file or other persistent storage.
    /// </summary>
    public bool IsSecret { get => (_value & 0x00010000_00000000L) != 0; }

    /// <summary>
    /// The length of the segment content
    /// </summary>
    public long Length { get => _value & 0x0000FFFF_FFFFFFFFL; }

    /// <summary>
    /// The value packing the other fields into one.
    /// </summary>
    public long PackedValue { get => _value; }
  }
}