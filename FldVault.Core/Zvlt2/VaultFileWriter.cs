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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using static System.Reflection.Metadata.BlobBuilder;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Wraps a vault file, an open writable stream for it, and a vault cryptor.
/// The stream is opened in the constructor, and closed when disposed
/// </summary>
public class VaultFileWriter: IDisposable
{
  private readonly VaultCryptor _cryptor;
  private readonly ByteCryptoBuffer _buffer;
  private readonly ByteCryptoBuffer _compressionBuffer;
  private readonly Stream _stream;
  private bool _disposed = false;

  /// <summary>
  /// Create a new VaultFileWriter
  /// </summary>
  public VaultFileWriter(
    VaultFile vault,
    VaultCryptor cryptor)
  {
    Vault = vault;
    _cryptor = cryptor;
    if(Vault.KeyId != _cryptor.KeyId)
    {
      throw new ArgumentException("The key does not match the vault file");
    }
    _buffer = new ByteCryptoBuffer(VaultFormat.VaultChunkSize);
    _compressionBuffer = new ByteCryptoBuffer(VaultFormat.VaultChunkSize);
    // We can assume the file exists: VaultFile already takes care of that
    _stream = File.OpenWrite(Vault.FileName);
    _stream.Position = _stream.Length;
  }

  /// <summary>
  /// The vault file descriptor
  /// </summary>
  public VaultFile Vault { get; init; }

  /// <summary>
  /// Append an unauthenticated comment block
  /// </summary>
  /// <param name="comment">
  /// The comment to add
  /// </param>
  public BlockInfo AppendComment(string comment)
  {
    CheckDisposed();
    var bytes = Encoding.UTF8.GetBytes(comment);
    var bi = new BlockInfo(BlockType.UnauthenticatedComment);
    // Writing the block will take care of setting the size and offset fields
    _stream.Position = _stream.Length;
    bi.WriteSync(_stream, bytes);
    Vault.Blocks.Add(bi);
    return bi;
  }

  /// <summary>
  /// Append an encrypted version of the "file" (provided here as a stream)
  /// to the vault.
  /// </summary>
  /// <param name="source">
  /// The stream providing the file content
  /// </param>
  /// <param name="compression">
  /// The compression option, default <see cref="ZvltCompression.Auto"/>.
  /// </param>
  /// <param name="metadata">
  /// The metadata object describing the file (name, size, timestamp and
  /// possibly custom properties)
  /// </param>
  /// <param name="utcStampOverride">
  /// Default null. If not null, this is a UTC timestamp used as the time
  /// recorded as encryption time. If null, the current time is used.
  /// </param>
  /// <param name="fileIdOverride">
  /// Default null. If not null this will be used as file element
  /// identifier (it is your responsibility to ensure it is unique).
  /// If null a random GUID is generated instead.
  /// </param>
  /// <param name="chunkSize">
  /// The size of the chunks the input is split in; each chunk is
  /// indepently processed and stored as one block.
  /// Default value is <see cref="VaultFormat.VaultChunkSize"/>.
  /// </param>
  /// <returns>
  /// A block element tree containing the file header as root element and
  /// the other elements as children
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown when the metadata specifies a file size that does not match
  /// the actual length.
  /// </exception>
  public BlockElement AppendFile(
    Stream source,
    FileMetadata metadata,
    ZvltCompression compression = ZvltCompression.Auto,
    DateTime? utcStampOverride = null,
    Guid? fileIdOverride = null,
    int chunkSize = VaultFormat.VaultChunkSize)
  {
    CheckDisposed();
    if(chunkSize > VaultFormat.VaultChunkSize)
    {
      throw new ArgumentOutOfRangeException(
        nameof(chunkSize),
        $"The chunk size ({chunkSize}) is larger than the maximum ({VaultFormat.VaultChunkSize})");
    }
    if(chunkSize < 0x10000)
    {
      throw new ArgumentOutOfRangeException(
        nameof(chunkSize),
        $"The chunk size ({chunkSize}) is smaller than the minimum ({0x10000})");
    }
    var stamp = EpochTicks.FromUtc(utcStampOverride ?? DateTime.UtcNow);
    var fh = new FileHeader(stamp, fileIdOverride ?? Guid.NewGuid());
    var rootElement = AppendFileHeaderBlock(fh);
    Span<byte> tagOut = stackalloc byte[16];
    var metaElement = AppendFileMetaBlock(fh, metadata, tagOut);
    rootElement.AddChild(metaElement);
    // We need a second buffer, because _buffer is already in use by the
    // private methods we will call
    using(var sourceBuffer = new ByteCryptoBuffer(chunkSize))
    {
      long written = 0L; // the number of *input* bytes written
      int n;
      Span<byte> tagIn = stackalloc byte[16];
      while((n = source.Read(sourceBuffer.Span())) > 0)
      {
        tagOut.CopyTo(tagIn);
        written += n;
        var plaintext = sourceBuffer.Span(0, n); // only correct if not compressing

        if(compression == ZvltCompression.Auto || compression == ZvltCompression.On)
        {
          var compressedSize = VaultCompressor.Compress(sourceBuffer, n, _compressionBuffer);
          switch(compression)
          {
            case ZvltCompression.Auto:
              if(
                compressedSize >= 0
                && (compressedSize + 256) * 100 < n * 92) // require a compression to less than 92% to enable compression
              {
                plaintext = _compressionBuffer.Span(0, compressedSize);
                compression = ZvltCompression.On;
              }
              else
              {
                // leave plaintext as-is: uncompressed
                compression = ZvltCompression.Off;
              }
              break;
            case ZvltCompression.Off:
              throw new InvalidOperationException(
                "Unexpected compression mode");
            case ZvltCompression.On:
              if(compressedSize >= 0
                && (compressedSize + 256) * 100 < n * 98) // now require compression to less than 98% (with 256 offset)
              {
                plaintext = _compressionBuffer.Span(0, compressedSize);
              } // else keep the uncompressed data as encryption plaintext
              break;
            default:
              throw new InvalidOperationException(
                "Unrecognized compression mode");
          }
        }

        var contentElement = AppendFileContentBlock(plaintext, n, tagIn, tagOut);
        rootElement.AddChild(contentElement);
      }
      var terminatorElement = AppendTerminator();
      rootElement.AddChild(terminatorElement);
      if(metadata.Size.HasValue && metadata.Size.Value != written)
      {
        throw new InvalidOperationException(
          $"The number of bytes from the source file ({written}) did not match the specified amount ({metadata.Size.Value})");
      }
    }
    return rootElement;
  }

  /// <summary>
  /// Append the specified file
  /// </summary>
  /// <param name="filename">
  /// The name of the file
  /// </param>
  /// <param name="compression">
  /// The compression option, default <see cref="ZvltCompression.Auto"/>.
  /// </param>
  /// <param name="additionalMetadata">
  /// If not null: additional key-value pairs recorded as metadata.
  /// Note that the keys 'name', 'stamp' and 'size' are reserved and not allowed.
  /// </param>
  /// <param name="utcStampOverride">
  /// Default null. If not null, this is a UTC timestamp used as the time
  /// recorded as encryption time. If null, the current time is used.
  /// </param>
  /// <param name="fileIdOverride">
  /// Default null. If not null this will be used as file element
  /// identifier (it is your responsibility to ensure it is unique).
  /// If null a random GUID is generated instead.
  /// </param>
  /// <param name="chunkSize">
  /// The size of the chunks the input is split in; each chunk is
  /// indepently processed and stored as one block.
  /// Default value is the maximum, <see cref="VaultFormat.VaultChunkSize"/>.
  /// </param>
  /// <returns>
  /// A block element tree containing the file header as root element and
  /// the other elements as children
  /// </returns>
  /// <remarks>
  /// <para>
  /// The "name" recorded in the metadata is the plain name of the file, without
  /// any path components.
  /// </para>
  /// </remarks>
  public BlockElement AppendFile(
    string filename,
    ZvltCompression compression = ZvltCompression.Auto,
    IDictionary<string, JToken?>? additionalMetadata = null,
    DateTime? utcStampOverride = null,
    Guid? fileIdOverride = null,
    int chunkSize = VaultFormat.VaultChunkSize)
  {
    CheckDisposed();
    if(additionalMetadata != null)
    {
      if(additionalMetadata.ContainsKey("name"))
      {
        throw new InvalidOperationException("The additional metadata must not contain the reserved key 'name'");
      }
      if(additionalMetadata.ContainsKey("stamp"))
      {
        throw new InvalidOperationException("The additional metadata must not contain the reserved key 'stamp'");
      }
      if(additionalMetadata.ContainsKey("size"))
      {
        throw new InvalidOperationException("The additional metadata must not contain the reserved key 'size'");
      }
    }
    if(!File.Exists(filename))
    {
      throw new FileNotFoundException("File to add was not found", filename);
    }
    var fileInfo = new FileInfo(filename);
    var metadata = new FileMetadata(
      fileInfo.Name,
      EpochTicks.FromUtc(fileInfo.LastWriteTimeUtc),
      fileInfo.Length);
    if(additionalMetadata != null)
    {
      foreach(var kvp in additionalMetadata)
      {
        metadata.OtherFields[kvp.Key] = kvp.Value;
      }
    }
    using(var stream = File.OpenRead(filename))
    {
      return AppendFile(stream, metadata, compression, utcStampOverride, fileIdOverride, chunkSize);
    }
  }

  /// <summary>
  /// Append a key transform block, storing the given key (encrypted
  /// by this vault file's key).
  /// </summary>
  /// <param name="keyBytes">
  /// The 32-byte key to store
  /// </param>
  /// <returns>
  /// A BlockElement representing the newly added block
  /// </returns>
  /// <exception cref="ArgumentException">
  /// Thrown if the key is not 32 bytes long
  /// </exception>
  public BlockElement AppendChildKey(
    ReadOnlySpan<byte> keyBytes)
  {
    if(keyBytes.Length != 32)
    {
      throw new ArgumentException(
        "Expecting the key to be 32 bytes long", nameof(keyBytes));
    }
    CheckDisposed();
    var keyId = HashResult.FromSha256(keyBytes).AsGuid;
    _stream.Position = _stream.Length;
    var bi = new BlockInfo(Zvlt2BlockType.KeyTransform, 84, _stream.Position);
    Span<byte> span = stackalloc byte[bi.Size];
    Span<byte> nonce = stackalloc byte[12];
    Span<byte> cipherText = _buffer.Span(0, keyBytes.Length);
    Span<byte> idBytes = stackalloc byte[16];
    Span<byte> tagOut = stackalloc byte[16];
    keyId.TryWriteBytes(idBytes);
    _cryptor.Encrypt(idBytes, keyBytes, cipherText, nonce, tagOut);
    new SpanWriter()
      .WriteI32(span, bi.Kind)
      .WriteI32(span, bi.Size)
      .WriteGuid(span, keyId)
      .WriteSpan(span, nonce)
      .WriteSpan(span, tagOut)
      .WriteSpan(span, cipherText)
      .CheckFull(span);
    _stream.Write(span);
    bi.VerifyBlockEnd(_stream);
    Vault.Blocks.Add(bi);
    return new BlockElement(bi);
  }

  /// <summary>
  /// Append a key transform block, storing the specified key (encrypted
  /// by this vault file's key).
  /// </summary>
  /// <param name="keyId">
  /// The id of the key to store. This key must exist in <paramref name="keyChain"/>
  /// </param>
  /// <param name="keyChain">
  /// The key chain holding the key to store
  /// </param>
  /// <returns>
  /// A BlockElement representing the newly added block
  /// </returns>
  /// <exception cref="ArgumentException">
  /// Thrown if the key is not found in the key chain
  /// </exception>
  public BlockElement AppendChildKey(
    Guid keyId,
    KeyChain keyChain)
  {
    var blockElement = 
      keyChain.TryMapKey(keyId, (kid, ibw) => AppendChildKey(ibw.Bytes));
    return blockElement ?? throw new ArgumentException(
        "The key was not found in the key chain",
        nameof(keyId));
  }

  /// <summary>
  /// Append a clone of the specified element from a compatible vault file.
  /// To be compatible, the source vault must have the same key and creation
  /// stamp as this vault.
  /// </summary>
  /// <param name="sourceVault"></param>
  /// <param name="sourceElement"></param>
  /// <returns></returns>
  public BlockElement AppendCloneElement(
    VaultFileReader sourceVault,
    IBlockElement sourceElement)
  {
    if(!IsCompatibleSource(sourceVault))
    {
      throw new ArgumentException(
        "The source vault is not compatible with this vault",
        nameof(sourceVault));
    }
    var rootBlock = AppendCloneBlock(sourceVault, sourceElement.Block);
    var rootElement = new BlockElement(rootBlock);
    foreach(var child in sourceElement.Children)
    {
      // Recursive!
      // But the expectation is that the children don't have children themselves.
      var childElement = AppendCloneElement(sourceVault, child);
      rootElement.AddChild(childElement);
    }
    return rootElement;
  }

  /// <summary>
  /// Append a clone of the specified block from a compatible vault file.
  /// To be compatible, the source vault must have the same key and creation
  /// stamp as this vault.
  /// </summary>
  /// <param name="sourceVault"></param>
  /// <param name="sourceBlock"></param>
  /// <returns></returns>
  public BlockInfo AppendCloneBlock(
    VaultFileReader sourceVault,
    IBlockInfo sourceBlock)
  {
    if(!IsCompatibleSource(sourceVault))
    {
      throw new ArgumentException(
        "The source vault is not compatible with this vault",
        nameof(sourceVault));
    }
    throw new NotImplementedException();
  }

  /// <summary>
  /// Test if the source vault is compatible with this vault, enabling
  /// cloning blocks and elements directly, without reencoding. To enable
  /// this scenario, the source vault must have the same key and creation
  /// stamp as this vault.
  /// </summary>
  /// <param name="sourceVault">
  /// The source vault to test
  /// </param>
  /// <returns>
  /// True if the source vault is compatible with this vault
  /// </returns>
  public bool IsCompatibleSource(VaultFile sourceVault)
  {
    return sourceVault.KeyId == Vault.KeyId
      && sourceVault.Header.TimeStamp == Vault.Header.TimeStamp;
  }

  /// <summary>
  /// Test if the source vault is compatible with this vault, enabling
  /// cloning blocks and elements directly, without reencoding. To enable
  /// this scenario, the source vault must have the same key and creation
  /// stamp as this vault.
  /// </summary>
  /// <param name="sourceVaultReader">
  /// A reader on the source vault to test
  /// </param>
  /// <returns>
  /// True if the source vault is compatible with this vault
  /// </returns>
  public bool IsCompatibleSource(VaultFileReader sourceVaultReader)
  {
    return IsCompatibleSource(sourceVaultReader.Vault);
  }

  /// <summary>
  /// Clean up
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

  private void CheckDisposed()
  {
    if(_disposed)
    {
      throw new ObjectDisposedException(nameof(VaultFileWriter));
    }
  }

  private BlockElement AppendFileHeaderBlock(
    FileHeader fileHeader)
  {
    _stream.Position = _stream.Length;
    var bi = new BlockInfo(Zvlt2BlockType.FileHeader, 32, _stream.Position);
    Span<byte> span = stackalloc byte[bi.Size];
    new SpanWriter()
      .WriteI32(span, bi.Kind)
      .WriteI32(span, bi.Size)
      .WriteI64(span, fileHeader.EncryptionStamp)
      .WriteGuid(span, fileHeader.FileId)
      .CheckFull(span);
    _stream.Write(span);
    bi.VerifyBlockEnd(_stream);
    Vault.Blocks.Add(bi);
    return new BlockElement(bi);
  }

  private BlockElement AppendFileMetaBlock(
    FileHeader fileHeader,
    FileMetadata metadata,
    Span<byte> tagOut)
  {
    var json = JsonConvert.SerializeObject(metadata);
    var plaintext = Encoding.UTF8.GetBytes(json); // not much point in wrapping it in a CryptoBuffer
    var ciphertext = _buffer.Span(0, plaintext.Length);
    var size = 36 + plaintext.Length;
    _stream.Position = _stream.Length;
    var bi = new BlockInfo(Zvlt2BlockType.FileMetadata, size, _stream.Position);
    Span<byte> nonce = stackalloc byte[12];
    Span<byte> aux = stackalloc byte[24];
    new SpanWriter()
      .WriteI32(aux, bi.Kind)
      .WriteI32(aux, bi.Size)
      .WriteI64(aux, fileHeader.EncryptionStamp)
      .WriteI64(aux, EpochTicks.FromUtc(Vault.Header.TimeStamp))
      .CheckFull(aux);
    _cryptor.Encrypt(aux, plaintext, ciphertext, nonce, tagOut);
    Span<byte> header = stackalloc byte[8];
    bi.FormatBlockHeader(header);
    _stream.Write(header);
    _stream.Write(nonce);
    _stream.Write(tagOut);
    _stream.Write(ciphertext);
    bi.VerifyBlockEnd(_stream);
    Vault.Blocks.Add(bi);
    return new BlockElement(bi);
  }

  private BlockElement AppendFileContentBlock(
    ReadOnlySpan<byte> plainText,
    int contentLength,
    ReadOnlySpan<byte> tagIn,
    Span<byte> tagOut)
  {
    var fch = FileContentHeader.Create(_stream, contentLength, plainText.Length + 40, out var bi);
    Span<byte> nonce = stackalloc byte[12];
    Span<byte> cipherText = _buffer.Span(0, plainText.Length);
    _cryptor.Encrypt(tagIn, plainText, cipherText, nonce, tagOut);
    fch.Write(_stream);
    _stream.Write(nonce);
    _stream.Write(tagOut);
    _stream.Write(cipherText);
    bi.VerifyBlockEnd(_stream);
    Vault.Blocks.Add(bi);
    return new BlockElement(bi);
  }

  private BlockElement AppendTerminator()
  {
    var bi = new BlockInfo(BlockType.ImpliedGroupEnd);
    bi.WriteSync(_stream, Span<byte>.Empty);
    Vault.Blocks.Add(bi);
    return new BlockElement(bi);
  }

}
