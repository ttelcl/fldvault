/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto;

/// <summary>
/// A "key" that is in fact not secret, but can be used for purposes
/// where a key would otherwise be required. The key consists of
/// 32 bytes that are all 0x00.
/// </summary>
public class NullKey: KeyBuffer
{
  /// <summary>
  /// Create a new NullKey
  /// </summary>
  public NullKey()
    : base(new byte[32])
  {
  }

  /// <summary>
  /// The guid of the null key, "ad7a6866-62f8-47bd-ac8f-c18b8e9f8e20".
  /// </summary>
  public static Guid NullKeyId = new Guid("ad7a6866-62f8-47bd-ac8f-c18b8e9f8e20");

  /// <summary>
  /// Check if the key ID is that of the null key
  /// </summary>
  public static bool IsNullKey(Guid guid) => guid == NullKeyId;
}
