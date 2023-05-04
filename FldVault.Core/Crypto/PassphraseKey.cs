/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto
{
  /// <summary>
  /// Stores key data derived from a passphrase (or password),
  /// using Rfc2898DeriveBytes
  /// </summary>
  public class PassphraseKey: KeyBuffer
  {
    private readonly byte[] _salt;

    // 600000 and SHA256 as found in
    // https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html#pbkdf2
    private const int __iterationCount = 600000;
    private static readonly HashAlgorithmName __algorithm = HashAlgorithmName.SHA256;
    private const int __saltlength = 64;

    /// <summary>
    /// Create a new PassphraseKey, copying the key and salt.
    /// The copy of the key will be cleared upon disposal
    /// (the salt isn't since it isn't considered to be a secret)
    /// </summary>
    private PassphraseKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> salt)
      : base(key)
    {
      _salt = salt.ToArray();
    }

    /// <summary>
    /// Create a PassphraseKey from a "password" given as byte span and
    /// a predefined salt (for reconstructing a previously used key).
    /// </summary>
    /// <param name="keyLength">
    /// The number of bytes to derive as resulting key
    /// </param>
    /// <param name="passbytes">
    /// The bytes used as "password"
    /// </param>
    /// <param name="salt">
    /// The salt bytes
    /// </param>
    /// <returns>
    /// A new PassphraseKey instance
    /// </returns>
    public static PassphraseKey FromBytes(
      int keyLength,
      CryptoBuffer<byte> passbytes,
      ReadOnlySpan<byte> salt)
    {
      using(var keyBuffer = new CryptoBuffer<byte>(keyLength))
      {
        Rfc2898DeriveBytes.Pbkdf2(passbytes.Span(), salt, keyBuffer.Span(), __iterationCount, __algorithm);
        return new PassphraseKey(keyBuffer.Span(), salt);
      }
    }

    /// <summary>
    /// Create a PassphraseKey from a "password" given as byte span and
    /// a newly created salt (for first use of a new key).
    /// </summary>
    /// <param name="keyLength">
    /// The number of bytes to derive as resulting key
    /// </param>
    /// <param name="passbytes">
    /// The bytes used as "password"
    /// </param>
    /// <returns>
    /// A new PassphraseKey instance
    /// </returns>
    public static PassphraseKey FromBytes(
      int keyLength,
      CryptoBuffer<byte> passbytes)
    {
      var salt = new byte[__saltlength];
      RandomNumberGenerator.Fill(salt);
      return FromBytes(keyLength, passbytes, salt);
    }

    /// <summary>
    /// Create a PassphraseKey from a "password" given as character span and
    /// a predefined salt (for reconstructing a previously used key).
    /// </summary>
    /// <param name="keyLength">
    /// The number of bytes to derive as resulting key
    /// </param>
    /// <param name="passchars">
    /// The characters used as "password"
    /// </param>
    /// <param name="salt">
    /// The salt bytes
    /// </param>
    /// <returns>
    /// A new PassphraseKey instance
    /// </returns>
    public static PassphraseKey FromCharacters(
      int keyLength,
      CryptoBuffer<char> passchars,
      ReadOnlySpan<byte> salt)
    {
      using(var keyBuffer = new CryptoBuffer<byte>(keyLength))
      {
        Rfc2898DeriveBytes.Pbkdf2(passchars.Span(), salt, keyBuffer.Span(), __iterationCount, __algorithm);
        return new PassphraseKey(keyBuffer.Span(), salt);
      }
    }

    /// <summary>
    /// Create a PassphraseKey from a "password" given as character span and
    /// a newly created salt (for first use of a new key).
    /// </summary>
    /// <param name="keyLength">
    /// The number of bytes to derive as resulting key
    /// </param>
    /// <param name="passchars">
    /// The characters used as "password"
    /// </param>
    /// <returns>
    /// A new PassphraseKey instance
    /// </returns>
    public static PassphraseKey FromCharacters(
      int keyLength,
      CryptoBuffer<char> passchars)
    {
      var salt = new byte[__saltlength];
      RandomNumberGenerator.Fill(salt);
      return FromCharacters(keyLength, passchars, salt);
    }

    /// <summary>
    /// Create a PassphraseKey from a "passphrase" given as SecureString and
    /// a predefined salt (for reconstructing a previously used key).
    /// </summary>
    /// <param name="keyLength">
    /// The number of bytes to derive as resulting key
    /// </param>
    /// <param name="passphrase">
    /// The passphrase / password
    /// </param>
    /// <param name="salt">
    /// The salt bytes
    /// </param>
    /// <returns>
    /// A new PassphraseKey instance
    /// </returns>
    public static PassphraseKey FromSecureString(
      int keyLength,
      SecureString passphrase,
      ReadOnlySpan<byte> salt)
    {
      using(var characters = UnpackSecureString(passphrase))
      {
        return FromCharacters(keyLength, characters, salt);
      }
    }

    /// <summary>
    /// Create a PassphraseKey from a "passphrase" given as SecureString and
    /// a newly created salt (for first use of a new key).
    /// </summary>
    /// <param name="keyLength">
    /// The number of bytes to derive as resulting key
    /// </param>
    /// <param name="passphrase">
    /// The passphrase / password
    /// </param>
    /// <returns>
    /// A new PassphraseKey instance
    /// </returns>
    public static PassphraseKey FromSecureString(
      int keyLength,
      SecureString passphrase)
    {
      using(var characters = UnpackSecureString(passphrase))
      {
        return FromCharacters(keyLength, characters);
      }
    }

    /// <summary>
    /// Return a view on the salt bytes stored in this object
    /// </summary>
    public ReadOnlySpan<byte> Salt { get => _salt; }

    private static CryptoBuffer<char> UnpackSecureString(SecureString ss)
    {
      var characters = new CryptoBuffer<char>(ss.Length);
      UnpackSecureString(ss, characters);
      return characters;
    }

    private static void UnpackSecureString(SecureString ss, CryptoBuffer<char> characters)
    {
      if(ss.Length != characters.Length)
      {
        throw new ArgumentException(
          "Expecting both arguments to have the same length");
      }
      // https://stackoverflow.com/a/819705/271323
      var valuePtr = IntPtr.Zero;
      var charspan = characters.Span();
      try
      {
        valuePtr = Marshal.SecureStringToGlobalAllocUnicode(ss);
        for(int i = 0; i < ss.Length; i++)
        {
          var ch = (char)(ushort)Marshal.ReadInt16(valuePtr, i * 2);
          charspan[i] = ch;
        }
      }
      finally
      {
        Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
      }
    }

  }
}
