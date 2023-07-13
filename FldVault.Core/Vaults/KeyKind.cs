/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Vaults
{
  /// <summary>
  /// Recognized key source kinds as constant strings. 
  /// Used as secondary file extensions for *.{KeyKind}.key-info files
  /// </summary>
  public static class KeyKind
  {
    /// <summary>
    /// Indicates a key-info that contains the information to turn
    /// a passphrase into a key.
    /// </summary>
    public const string Passphrase = "pass";

    /// <summary>
    /// Indicates a key-info that contains the key encrypted with
    /// another key.
    /// </summary>
    public const string Link = "link";

    /// <summary>
    /// Indicates a stub key-info used to identify the null key
    /// (key ID "ad7a6866-62f8-47bd-ac8f-c18b8e9f8e20", containing
    /// 32 0x00 bytes).
    /// </summary>
    public const string Null = "null";
  }
}
