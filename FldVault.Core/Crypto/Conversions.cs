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
  /// Static conversion methods
  /// </summary>
  public static class Conversions
  {

    /// <summary>
    /// Create a type 4 GUID from a span of 16 bytes.
    /// Of the 128 input bits, 6 will be adjusted to make
    /// a type 4 GUID.
    /// </summary>
    /// <param name="bytes">
    /// The 16 input bytes
    /// </param>
    /// <returns>
    /// A new GUID
    /// </returns>
    public static Guid BytesToGuid(ReadOnlySpan<byte> bytes)
    {
      if(bytes.Length != 16)
      {
        throw new ArgumentException(
          "Expecting 16 bytes as input", nameof(bytes));
      }
      // We need a temporary copy of the input to be able to set
      // the 6 bits that makes a type 4 GUID
      Span<byte> span = stackalloc byte[16];
      bytes.CopyTo(span);
      span[7] = (byte)(span[7] & 0x0F | 0x40);
      span[8] = (byte)(span[8] & 0x3F | 0x80);
      return new Guid(span);
    }

    /// <summary>
    /// Return the input bytes as a lower case hexadecimal string
    /// </summary>
    public static string BytesToHex(ReadOnlySpan<byte> bytes)
    {
      return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Return the input bytes as a base64 string
    /// </summary>
    public static string BytesToBase64(ReadOnlySpan<byte> bytes)
    {
      return Convert.ToBase64String(bytes);
    }

  }
}
