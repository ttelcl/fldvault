/*
 * (c) 2026  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.KeyServer.LightweightClient;

/// <summary>
/// Static helper methods related to <see cref="LightweightClient"/>,
/// useful enough to be public, but not close enough to be part of that
/// class' static API
/// </summary>
public static class LightweightClientHelpers
{
  /// <summary>
  /// The default short name for the key server Unix Domain socket
  /// (<c>zvlt-keyserver.sock</c>)
  /// </summary>
  public const string DefaultSocketName = "zvlt-keyserver.sock";

  /// <summary>
  /// The folder where the key server socket is created by default
  /// (<c>%LocalApplicationData%/.zvlt/sockets/</c>)
  /// </summary>
  public static string DefaultSocketFolder { get; } =
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".zvlt", "sockets");

  /// <summary>
  /// Get the full path to the socket pseudo-file
  /// </summary>
  /// <param name="socketName">
  /// Either the short name for the socket (without any path separators),
  /// or the full path to the socket, or null to use the default.
  /// </param>
  public static string ResolveSocketPath(string? socketName)
  {
    socketName ??= DefaultSocketName;
    if(socketName.IndexOfAny(__pathIndicatorChars)>=0)
    {
      return Path.GetFullPath(socketName);
    }
    else
    {
      if(!Directory.Exists(DefaultSocketFolder))
      {
        Directory.CreateDirectory(DefaultSocketFolder);
      }
      return Path.Combine(DefaultSocketFolder, socketName);
    }
  }

  /// <summary>
  /// Use PKBDF2 to convert a binary passphrase to a raw key.
  /// </summary>
  /// <param name="passphraseBytes">
  /// The passphrase (e.g. text encoded as UTF8). Well, or just any binary bytes you want to use
  /// as "passphrase" - UTF8 text is merely the usual case.
  /// </param>
  /// <param name="salt">
  /// The salt bytes. These should be high quality random bytes. The standard
  /// ZKEY model expects precisely <see cref="SaltLength"/> bytes.
  /// </param>
  /// <param name="key">
  /// The buffer to hold the resulting key bytes. This buffer should be
  /// <see cref="KeyLength"/> (32) bytes, as AES256 expects.
  /// </param>
  /// <param name="iterationCount">
  /// The number of PBKDF2 iterations (default 600000). Values below 500000 are
  /// rejected. Using a value other than 600000 breaks ZKEY compatibility.
  /// </param>
  public static void PassphraseToKey(
    ReadOnlySpan<byte> passphraseBytes,
    ReadOnlySpan<byte> salt,
    Span<byte> key,
    int iterationCount = __iterationCount)
  {
    if(key.Length != KeyLength)
    {
      throw new ArgumentOutOfRangeException(
        nameof(key),
        $"Expecting {nameof(key)} to be {KeyLength} bytes long");
    }
    if(salt.Length != SaltLength)
    {
      // allow different salt length, but trace a warning
      Trace.TraceWarning(
        $"Unusual salt length, {salt.Length} instead of {SaltLength} - the key conversion will not be ZKEY compatible");
    }
    if(iterationCount < 500000)
    {
      throw new ArgumentOutOfRangeException(
        nameof(iterationCount),
        "iteration counts below 500000 are deemed to be too insecure");
    }
    if(iterationCount != __iterationCount)
    {
      Trace.TraceWarning(
        "Using a nonstandard iteration count. The result key will not be ZKEY compatible");
    }
    Rfc2898DeriveBytes.Pbkdf2(passphraseBytes, salt, key, iterationCount, __algorithm);
  }

  /// <summary>
  /// Use PKBDF2 to convert a passphrase to a raw key.
  /// </summary>
  /// <param name="passphraseChars">
  /// The passphrase characters
  /// </param>
  /// <param name="salt">
  /// The salt bytes. These should be high quality random bytes. The standard
  /// ZKEY model expects precisely <see cref="SaltLength"/> bytes.
  /// </param>
  /// <param name="key">
  /// The buffer to hold the resulting key bytes. This buffer should be
  /// <see cref="KeyLength"/> (32) bytes, as AES256 expects.
  /// </param>
  /// <param name="iterationCount">
  /// The number of PBKDF2 iterations (default 600000). Values below 500000 are
  /// rejected. Using a value other than 600000 breaks ZKEY compatibility.
  /// </param>
  public static void PassphraseToKey(
    ReadOnlySpan<char> passphraseChars,
    ReadOnlySpan<byte> salt,
    Span<byte> key,
    int iterationCount = __iterationCount)
  {
    if(key.Length != KeyLength)
    {
      throw new ArgumentOutOfRangeException(
        nameof(key),
        $"Expecting {nameof(key)} to be {KeyLength} bytes long");
    }
    if(salt.Length != SaltLength)
    {
      // allow different salt length, but trace a warning
      Trace.TraceWarning(
        $"Unusual salt length, {salt.Length} instead of {SaltLength} - the key conversion will not be ZKEY compatible");
    }
    if(iterationCount < 500000)
    {
      throw new ArgumentOutOfRangeException(
        nameof(iterationCount),
        "iteration counts below 500000 are deemed to be too insecure");
    }
    if(iterationCount != __iterationCount)
    {
      Trace.TraceWarning(
        "Using a nonstandard iteration count. The result key will not be ZKEY compatible");
    }
    Rfc2898DeriveBytes.Pbkdf2(passphraseChars, salt, key, iterationCount, __algorithm);
  }

  /// <summary>
  /// Use PKBDF2 to convert a passphrase to a raw key.
  /// </summary>
  /// <param name="passphrase">
  /// The passphrase, in the form of a <see cref="SecureString"/>.
  /// </param>
  /// <param name="salt">
  /// The salt bytes. These should be high quality random bytes. The standard
  /// ZKEY model expects precisely <see cref="SaltLength"/> bytes.
  /// </param>
  /// <param name="key">
  /// The buffer to hold the resulting key bytes. This buffer should be
  /// <see cref="KeyLength"/> (32) bytes, as AES256 expects.
  /// </param>
  /// <param name="iterationCount">
  /// The number of PBKDF2 iterations (default 600000). Values below 500000 are
  /// rejected. Using a value other than 600000 breaks ZKEY compatibility.
  /// </param>
  public static void PassphraseToKey(
    SecureString passphrase,
    ReadOnlySpan<byte> salt,
    Span<byte> key,
    int iterationCount = __iterationCount)
  {
    if(key.Length != KeyLength)
    {
      throw new ArgumentOutOfRangeException(
        nameof(key),
        $"Expecting {nameof(key)} to be {KeyLength} bytes long");
    }
    if(salt.Length != SaltLength)
    {
      // allow different salt length, but trace a warning
      Trace.TraceWarning(
        $"Unusual salt length, {salt.Length} instead of {SaltLength} - the key conversion will not be ZKEY compatible");
    }
    if(iterationCount < 500000)
    {
      throw new ArgumentOutOfRangeException(
        nameof(iterationCount),
        "iteration counts below 500000 are deemed to be too insecure");
    }
    if(iterationCount != __iterationCount)
    {
      Trace.TraceWarning(
        "Using a nonstandard iteration count. The result key will not be ZKEY compatible");
    }
    Span<char> characters = stackalloc char[passphrase.Length];
    try
    {
      Rfc2898DeriveBytes.Pbkdf2(characters, salt, key, iterationCount, __algorithm);
    }
    finally
    {
      characters.Clear();
    }
  }

  /// <summary>
  /// Calculate the ZKEY style key key ID from raw key bytes
  /// </summary>
  /// <param name="key"></param>
  /// <returns></returns>
  public static Guid KeyGuid(ReadOnlySpan<byte> key)
  {
    Span<byte> hashBytes = stackalloc byte[32];
    SHA256.HashData(key, hashBytes);
    return HashToGuid(hashBytes);
  }

  /// <summary>
  /// Convert the result of a hashing operation to a GUID
  /// </summary>
  /// <param name="hashBytes">
  /// The bytes to convert. Only the first 16 bytes are used.
  /// </param>
  /// <returns>
  /// A type 4 GUID based on the given bytes, with 6 of the 128 bits set to
  /// conform to "type 4".
  /// </returns>
  private static Guid HashToGuid(this ReadOnlySpan<byte> hashBytes)
  {
    if(hashBytes.Length < 16)
    {
      throw new ArgumentOutOfRangeException(
        nameof(hashBytes),
        "Expecting at least 16 bytes as input");
    }
    // We need a temporary copy of the input to be able to set
    // the 6 bits that makes a type 4 GUID
    Span<byte> span = stackalloc byte[16];
    hashBytes[0..16].CopyTo(span);
    span[7] = (byte)(span[7] & 0x0F | 0x40);
    span[8] = (byte)(span[8] & 0x3F | 0x80);
    return new Guid(span);
  }

  private static void UnpackSecureString(SecureString ss, Span<char> characters)
  {
    if(ss.Length != characters.Length)
    {
      throw new ArgumentException(
        "Expecting both arguments to have the same length");
    }
    // https://stackoverflow.com/a/819705/271323
    var valuePtr = IntPtr.Zero;
    try
    {
      valuePtr = Marshal.SecureStringToGlobalAllocUnicode(ss);
      for(int i = 0; i < ss.Length; i++)
      {
        var ch = (char)(ushort)Marshal.ReadInt16(valuePtr, i * 2);
        characters[i] = ch;
      }
    }
    finally
    {
      Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
    }
  }

  /// <summary>
  /// The number of bytes expected for the salt
  /// </summary>
  public const int SaltLength = 64;

  /// <summary>
  /// The key length in bytes (32 for AES256)
  /// </summary>
  public const int KeyLength = 32;

  private static readonly char[] __pathIndicatorChars = ['/', '\\', ':'];

  // 600000 and SHA256 as found in
  // https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html#pbkdf2
  private const int __iterationCount = 600000;
  private static readonly HashAlgorithmName __algorithm = HashAlgorithmName.SHA256;

}
