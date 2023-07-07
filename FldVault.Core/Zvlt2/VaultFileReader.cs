/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.BlockFiles;
using FldVault.Core.Crypto;
using FldVault.Core.Utilities;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Represents a VaultFile that is open for reading. This class wraps
/// the underlying stream and takes care of disposing it when closed.
/// </summary>
public class VaultFileReader: IDisposable
{
  private readonly VaultCryptor? _cryptor;
  private readonly ByteCryptoBuffer _buffer;
  private readonly Stream _stream;
  private bool _disposed = false;

  /// <summary>
  /// Create a new VaultFileReader
  /// </summary>
  /// <param name="vault">
  /// The vault descriptor describing the vault file to open
  /// </param>
  /// <param name="cryptor">
  /// The encryption key for the vault. This can be null, but the resulting
  /// VaultFileReader cannot handle encrypted content in that case.
  /// </param>
  public VaultFileReader(
    VaultFile vault,
    VaultCryptor? cryptor)
  {
    Vault = vault;
    _cryptor = cryptor;
    if(_cryptor!=null && Vault.KeyId != _cryptor.KeyId)
    {
      throw new ArgumentException("The key does not match the vault file");
    }
    _buffer = new ByteCryptoBuffer(VaultFormat.VaultChunkSize);
    _stream = File.OpenRead(Vault.FileName);
  }

  /// <summary>
  /// The vault file descriptor
  /// </summary>
  public VaultFile Vault { get; init; }

  /// <summary>
  /// Move the position of the read pointer to the start of the
  /// given block, read the header, and validate that the header is as
  /// described. After this call the stream is positioned at the start
  /// of the block content.
  /// </summary>
  /// <param name="blockInfo">
  /// The descriptor of the block to seek to.
  /// </param>
  public void SeekBlock(IBlockInfo blockInfo)
  {
    CheckDisposed();
    _stream.Position = blockInfo.Offset;
    Span<byte> blockHeader = stackalloc byte[8];
    if(_stream.Read(blockHeader) < blockHeader.Length)
    {
      throw new EndOfStreamException();
    }
    new SpanReader()
      .ReadI32(blockHeader, out var kind)
      .ReadI32(blockHeader, out var size)
      .CheckEmpty(blockHeader);
    if(kind != blockInfo.Kind)
    {
      throw new InvalidOperationException("Invalid block data: the block kind did not match");
    }
    if(size != blockInfo.Size)
    {
      throw new InvalidOperationException("Invalid block data: the block size did not match");
    }
  }

  /// <summary>
  /// Read bytes from the current position of the stream, completely filling
  /// the given span (and throwing an <see cref="EndOfStreamException"/> if
  /// the read failed).
  /// </summary>
  public void ReadSpan(Span<byte> span)
  {
    CheckDisposed();
    var n = _stream.Read(span);
    if(n != span.Length)
    {
      throw new EndOfStreamException(
        "Unexpected end of stream");
    }
  }

  /// <summary>
  /// Assuming the stream is pointing to a nonce-authenticationtag-ciphertext
  /// sub-block, read those parts and decrypt them.
  /// </summary>
  /// <param name="associatedData">
  /// The associated data. This must exactly match the associated data provided
  /// during encryption.
  /// </param>
  /// <param name="authTagOut">
  /// The buffer to receive the authentication tag
  /// </param>
  /// <param name="plaintext">
  /// The buffer to receive the decrypted content. The size of this also
  /// determines the number of ciphertext bytes to read from the stream.
  /// </param>
  /// <param name="verifyEndBlock">
  /// If not null, it is verified that the stream points to the end of this block
  /// after all components have been read.
  /// </param>
  public void DecryptFragment(
    ReadOnlySpan<byte> associatedData,
    Span<byte> authTagOut,
    Span<byte> plaintext,
    IBlockInfo? verifyEndBlock = null)
  {
    CheckDisposed();
    var cryptor = CheckCryptor();
    if(plaintext.Length > _buffer.Length)
    {
      throw new ArgumentException(
        "plaintext buffer size is larger than supported");
    }
    Span<byte> nonce = stackalloc byte[12];
    Span<byte> ciphertext = _buffer.Span(0, plaintext.Length);
    ReadSpan(nonce);
    ReadSpan(authTagOut);
    ReadSpan(ciphertext);
    verifyEndBlock?.VerifyBlockEnd(_stream);
    cryptor.Decrypt(associatedData, nonce, authTagOut, ciphertext, plaintext);
  }

  private void CheckDisposed()
  {
    if(_disposed)
    {
      throw new ObjectDisposedException(nameof(VaultFileReader));
    }
  }

  private VaultCryptor CheckCryptor()
  {
    if(_cryptor == null)
    {
      throw new InvalidOperationException(
        "No encryption key provided");
    }
    return _cryptor;
  }

  /// <summary>
  /// Dispose the underlying file stream and mark this object as disposed
  /// </summary>
  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      _buffer?.Dispose();
      _stream?.Dispose();
    }
  }
}
