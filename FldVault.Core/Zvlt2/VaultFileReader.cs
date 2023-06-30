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
  private readonly VaultCryptor _cryptor;
  private readonly Stream _stream;
  private bool _disposed = false;

  /// <summary>
  /// Create a new VaultFileReader
  /// </summary>
  /// <param name="vault">
  /// The vault descriptor describing the vault file to open
  /// </param>
  /// <param name="cryptor">
  /// The encryption key for the vault
  /// </param>
  public VaultFileReader(
    VaultFile vault,
    VaultCryptor cryptor)
  {
    Vault = vault;
    _cryptor = cryptor;
    if(Vault.KeyId != _cryptor.KeyId)
    {
      throw new ArgumentException("The key does not match the vault file");
    }
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

  private void CheckDisposed()
  {
    if(_disposed)
    {
      throw new ObjectDisposedException(nameof(VaultFileReader));
    }
  }

  /// <summary>
  /// Dispose the underlying file stream and mark this object as disposed
  /// </summary>
  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      _stream.Dispose();
    }
  }
}
