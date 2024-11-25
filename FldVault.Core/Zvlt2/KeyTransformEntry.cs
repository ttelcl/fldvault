/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.BlockFiles;
using FldVault.Core.Crypto;
using FldVault.Core.Utilities;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Describes and caches a KeyTransform block in a ZVLT file, providing
/// an API to read its contents.
/// </summary>
public class KeyTransformEntry
{
  private readonly byte[] _contentBytes;

  /// <summary>
  /// Create a new KeyTransformEntry
  /// </summary>
  private KeyTransformEntry(
    VaultFile vault,
    ReadOnlySpan<byte> contentBytes)
  {
    Vault = vault;
    if(contentBytes.Length != 76)
    {
      throw new ArgumentException(
        $"Expecting a block content of size 76 (84-8), but got {contentBytes.Length}");
    }
    _contentBytes = contentBytes.ToArray();
    TargetKey = new Guid(contentBytes[..16]);
  }

  /// <summary>
  /// Create a new <see cref="KeyTransformEntry"/> by reading it from a VaultFileReader,
  /// and caching its encrypted content.
  /// </summary>
  /// <param name="reader">
  /// The open vault reader to read the block from
  /// </param>
  /// <param name="blockInfo">
  /// The pointer to the block in the vault file
  /// </param>
  /// <returns>
  /// The new <see cref="KeyTransformEntry"/>
  /// </returns>
  /// <exception cref="ArgumentException"></exception>
  public static KeyTransformEntry ReadFrom(
    VaultFileReader reader,
    IBlockInfo blockInfo)
  {
    if(blockInfo.Kind != Zvlt2BlockType.KeyTransform)
    {
      throw new ArgumentException(
        "Expecting a KeyTransform block");
    }
    if(blockInfo.Size != 84)
    {
      throw new ArgumentException(
        $"Expecting a block of size 84, but got {blockInfo.Size}");
    }
    Span<byte> contentBuffer = stackalloc byte[76];
    reader.SeekBlock(blockInfo);
    reader.ReadSpan(contentBuffer);
    return new KeyTransformEntry(
      reader.Vault,
      contentBuffer);
  }

  /// <summary>
  /// The vault file descriptor (implying the master encryption key)
  /// </summary>
  public VaultFile Vault { get; }

  /// <summary>
  /// The id of the key that this block provides.
  /// </summary>
  public Guid TargetKey { get; }

  /// <summary>
  /// Import the key described by this block into a key chain, using the
  /// existing cryptor to decrypt the key.
  /// </summary>
  /// <param name="keyChain">
  /// The key chain to import the key into
  /// </param>
  /// <param name="cryptor">
  /// The cryptor to use to decrypt the key (must match the vault file)
  /// </param>
  /// <returns>
  /// True if the key was imported, false if it was already present in the key chain.
  /// </returns>
  /// <exception cref="ArgumentException"></exception>
  /// <exception cref="InvalidOperationException"></exception>
  public bool ImportKey(KeyChain keyChain, VaultCryptor cryptor)
  {
    if(cryptor.KeyId != Vault.KeyId)
    {
      throw new ArgumentException(
        "The cryptor does not match the vault file");
    }
    if(keyChain.ContainsKey(TargetKey))
    {
      return false;
    }
    using(var masterKey = keyChain.FindCopy(Vault.KeyId))
    {
      if(masterKey == null)
      {
        throw new InvalidOperationException(
          "The master key for the vault is not in the key chain");
      }
      Span<byte> contentBytes = _contentBytes;
      var associatedData = contentBytes[0..16];
      var nonce = contentBytes[16..28];
      var tag = contentBytes[28..44];
      var cipherText = contentBytes[44..];
      using var plainTextBuffer = new CryptoBuffer<byte>(cipherText.Length);
      cryptor.Decrypt(associatedData, nonce, tag, cipherText, plainTextBuffer.Span());
      keyChain.PutCopy(plainTextBuffer);
      return true;
    }
  }

  /// <summary>
  /// Import the key described by this block into a key chain, using the
  /// decryptor from the vault file to decrypt the key.
  /// </summary>
  public bool ImportKey(KeyChain keyChain, VaultFileReader reader)
  {
    // The reader does not expose the cryptor, so we have to use a method
    // in the reader. It, in turn, calls the overload above.
    return reader.ImportChildKey(keyChain, this);
  }

  /// <summary>
  /// Import the key described by this block into a key chain, using a newly
  /// created cryptor to decrypt the key. For efficiency, use the other overloads
  /// instead of this one.
  /// </summary>
  /// <param name="keyChain"></param>
  /// <returns></returns>
  public bool ImportKey(KeyChain keyChain)
  {
    using var cryptor = Vault.CreateCryptor(keyChain);
    return ImportKey(keyChain, cryptor);
  }

}
