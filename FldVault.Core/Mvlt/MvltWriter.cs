/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FldVault.Core.Crypto;
using FldVault.Core.Utilities;
using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FldVault.Core.Mvlt;

/// <summary>
/// Contains the logic to write MVLT files.
/// </summary>
public class MvltWriter: IDisposable
{
  private bool _disposed = false;
  private readonly Stream _output;
  private readonly bool _ownsOutput;
  private readonly VaultCryptor _cryptor;
  private readonly byte[] _lastTag;
  private long _totalLength;
  private ByteCryptoBuffer _blockBuffer;
  private ByteCryptoBuffer _uncompressedBuffer;
  private ByteCryptoBuffer _compressedBuffer;

  /// <summary>
  /// Create a new MvltWriter
  /// </summary>
  public MvltWriter(
    KeyChain keyChain,
    PassphraseKeyInfoFile keydescriptor,
    Stream output,
    bool ownsOutput,
    NonceGenerator? nonceGenerator = null)
  {
    _output = output;
    _ownsOutput=ownsOutput;
    Phase = MvltPhase.BeforeHeader;
    var now = DateTime.UtcNow;
    TimeStamp = EpochTicks.FromUtc(now);
    KeyDescriptor = keydescriptor;
    var cryptor = new VaultCryptor(
      keyChain,
      keydescriptor.KeyId,
      now,
      nonceGenerator);
    _cryptor = cryptor;
    _lastTag = new byte[16];
    _blockBuffer = new ByteCryptoBuffer(MvltFormat.MvltChunkSize + 0x010000);
    _uncompressedBuffer = new ByteCryptoBuffer(MvltFormat.MvltChunkSize + 0x010000);
    _compressedBuffer = new ByteCryptoBuffer(MvltFormat.MvltChunkSize + 0x010000);
  }

  /// <summary>
  /// Compresses and encrypts the source stream and writes it to the sink stream
  /// as an MVLT file. The sink stream must be writable.
  /// </summary>
  /// <param name="source">
  /// The source stream to read from. The source stream must be readable.
  /// It does not need to be seekable.
  /// </param>
  /// <param name="sink">
  /// The sink stream to write to. The sink stream must be writable.
  /// It does not need to be seekable.
  /// </param>
  /// <param name="keyChain">
  /// The key chain providing the encryption key.
  /// </param>
  /// <param name="keyDescriptor">
  /// The key descriptor. This is used to select the key from <paramref name="keyChain"/>
  /// and is also embedded in the MVLT file.
  /// </param>
  /// <param name="ownsSink">
  /// True if the sink stream should be disposed when this method completes.
  /// </param>
  /// <param name="timeStamp">
  /// Optional. The time stamp to record for the encrypted stream, so
  /// it can be set as modified time on the file when it is decrypted again.
  /// This sets the 'modified' field in the preamble block.
  /// </param>
  /// <param name="metadata">
  /// Optional. The metadata to write in the preamble block. If both
  /// <paramref name="metadata"/> and <paramref name="timeStamp"/> are set, the
  /// 'modified' field is set to the value of <paramref name="timeStamp"/>.
  /// </param>
  /// <param name="ct">
  /// Cancellation token to cancel this asynchronous operation.
  /// </param>
  /// <returns></returns>
  public static async Task CompressAndEncrypt(
    Stream source,
    Stream sink,
    KeyChain keyChain,
    PassphraseKeyInfoFile keyDescriptor,
    bool ownsSink,
    DateTimeOffset? timeStamp = null,
    JObject? metadata = null,
    CancellationToken ct = default)
  {
    using var writer = new MvltWriter(
      keyChain,
      keyDescriptor,
      sink,
      ownsSink,
      null);
    await writer.WriteHeader(ct);
    metadata ??= new JObject();
    if(timeStamp.HasValue)
    {
      metadata["modified"] = timeStamp.Value.ToString("o");
    }
    await writer.WritePreamble(metadata, ct);
    await writer.WriteStreamData(source, ct);
    await writer.WriteTerminator(new JObject(), ct);
  }

  /// <summary>
  /// Compresses and encrypts the source stream and writes it to the sink file
  /// </summary>
  /// <param name="source">
  /// The source stream to read from. The source stream must be readable.
  /// </param>
  /// <param name="sinkName">
  /// The name of the output *.mvlt file. Consider using
  /// <see cref="DeriveMvltFileName(string, Guid)"/> to generate this name.
  /// </param>
  /// <param name="keyChain">
  /// The key chain providing the encryption key.
  /// </param>
  /// <param name="keyDescriptor">
  /// The key descriptor. This is used to select the key from <paramref name="keyChain"/>
  /// and is also embedded in the MVLT file.
  /// </param>
  /// <param name="timeStamp">
  /// Optional. The time stamp to record for the encrypted stream, so
  /// it can be set as modified time on the file when it is decrypted again.
  /// This sets the 'modified' field in the preamble block.
  /// </param>
  /// <param name="metadata">
  /// Optional. The metadata to write in the preamble block. If both
  /// <paramref name="metadata"/> and <paramref name="timeStamp"/> are set, the
  /// 'modified' field is set to the value of <paramref name="timeStamp"/>.
  /// </param>
  /// <param name="ct">
  /// Cancellation token to cancel this asynchronous operation.
  /// </param>
  /// <returns></returns>
  public static async Task CompressAndEncrypt(
    Stream source,
    string sinkName,
    KeyChain keyChain,
    PassphraseKeyInfoFile keyDescriptor,
    DateTimeOffset? timeStamp = null,
    JObject? metadata = null,
    CancellationToken ct = default)
  {
    var tmpName = sinkName + ".tmp";
    using(var sink = File.Create(tmpName))
    {
      await CompressAndEncrypt(
        source,
        sink,
        keyChain,
        keyDescriptor,
        false,
        timeStamp,
        metadata,
        ct);
    }
    if(!File.Exists(sinkName))
    {
      File.Move(tmpName, sinkName);
    }
    else
    {
      var bakName = sinkName + ".bak";
      if(File.Exists(bakName))
      {
        File.Delete(bakName);
      }
      File.Replace(
        tmpName,
        sinkName,
        bakName);
    }
  }

  /// <summary>
  /// Compresses and encrypts the source file and writes it to an MVLT file.
  /// The destination file name is constructed from the source file name.
  /// </summary>
  /// <param name="sourceFile">
  /// The name of the file to compress and encrypt.
  /// </param>
  /// <param name="keyChain">
  /// The key chain providing the encryption key.
  /// </param>
  /// <param name="keyDescriptor">
  /// The key descriptor. This is used to select the key from <paramref name="keyChain"/>
  /// and is also embedded in the MVLT file.
  /// </param>
  /// <param name="timeStamp">
  /// Optional. The time stamp to record for the encrypted stream, so
  /// it can be set as modified time on the file when it is decrypted again.
  /// This sets the 'modified' field in the preamble block.
  /// </param>
  /// <param name="metadata">
  /// Optional. The metadata to write in the preamble block. If both
  /// <paramref name="metadata"/> and <paramref name="timeStamp"/> are set, the
  /// 'modified' field is set to the value of <paramref name="timeStamp"/>.
  /// </param>
  /// <param name="ct">
  /// Cancellation token to cancel this asynchronous operation.
  /// </param>
  /// <returns></returns>
  public static async Task CompressAndEncrypt(
    string sourceFile,
    KeyChain keyChain,
    PassphraseKeyInfoFile keyDescriptor,
    DateTimeOffset? timeStamp = null,
    JObject? metadata = null,
    CancellationToken ct = default)
  {
    var sinkName = DeriveMvltFileName(sourceFile, keyDescriptor.KeyId);
    using var source = File.OpenRead(sourceFile);
    await CompressAndEncrypt(
      source,
      sinkName,
      keyChain,
      keyDescriptor,
      timeStamp,
      metadata,
      ct);
  }

  public static string DeriveMvltFileName(
    string sourceFile,
    Guid keyId)
  {
    var keyTag = keyId.ToString("D")[..8];
    var suffix = $".{keyTag}.mvlt";
    return sourceFile + suffix;
  }

  /// <summary>
  /// Describes the key used to encrypt the vault.
  /// </summary>
  public PassphraseKeyInfoFile KeyDescriptor { get; }

  /// <summary>
  /// The current phase of the MVLT file.
  /// </summary>
  public MvltPhase Phase { get; private set; }

  /// <summary>
  /// Creation timestamp in Epoch Ticks
  /// </summary>
  public long TimeStamp { get; }

  /// <summary>
  /// Write the header of the MVLT file. Also initializes the
  /// associated data buffer for the next block.
  /// </summary>
  public async Task WriteHeader(CancellationToken ct = default)
  {
    ExpectPhase(MvltPhase.BeforeHeader);
    var header = new byte[112]; // cannot use Span<byte> here (async)  
    BinaryPrimitives.WriteUInt32LittleEndian(
      header.AsSpan(0, 4), MvltFormat.MvltSignature);
    BinaryPrimitives.WriteUInt16LittleEndian(
      header.AsSpan(4, 2), MvltFormat.MvltMinorVersion);
    BinaryPrimitives.WriteUInt16LittleEndian(
      header.AsSpan(6, 2), MvltFormat.MvltMajorVersion);
    BinaryPrimitives.WriteInt64LittleEndian(
      header.AsSpan(8, 8), TimeStamp);
    KeyDescriptor.SerializeToSpan(header.AsSpan(16, 96));
    await _output.WriteAsync(header, ct);
    Phase = MvltPhase.AfterHeader;
    header.AsSpan(0, 16).CopyTo(_lastTag);
    _totalLength = 0L;
  }

  /// <summary>
  /// Write the preamble block.
  /// </summary>
  /// <param name="preambleObject">
  /// The part of the metadata that is known before the data is written.
  /// </param>
  /// <param name="ct"></param>
  /// <returns></returns>
  public async Task WritePreamble(
    JObject preambleObject,
    CancellationToken ct = default)
  {
    ExpectPhase(MvltPhase.AfterHeader);
    var json = JsonConvert.SerializeObject(
      preambleObject, Formatting.None);
    var jsonBytes = Encoding.UTF8.GetBytes(json);
    await WriteBlock(
      MvltFormat.Preamble4CC,
      jsonBytes.Length,
      jsonBytes,
      ct);
    Phase = MvltPhase.Data;
  }

  /// <summary>
  /// Write the terminator block. This method adds (or overwrites) the
  /// 'length' field to the terminator object.
  /// </summary>
  /// <param name="terminatorObject">
  /// The remaining fields of the metadata. The 'length' field
  /// is set by this method.
  /// </param>
  /// <param name="ct"></param>
  /// <returns></returns>
  public async Task WriteTerminator(
    JObject terminatorObject,
    CancellationToken ct = default)
  {
    ExpectPhase(MvltPhase.Data);
    terminatorObject["length"] = _totalLength;
    var json = JsonConvert.SerializeObject(
      terminatorObject, Formatting.None);
    var jsonBytes = Encoding.UTF8.GetBytes(json);
    await WriteBlock(
      MvltFormat.Terminator4CC,
      jsonBytes.Length,
      jsonBytes,
      ct);
    Phase = MvltPhase.End;
  }

  /// <summary>
  /// Write all content in the source stream as data blocks to the MVLT file.
  /// Does not write the header or terminator blocks; the header must have
  /// been written before this method is called.
  /// </summary>
  public async Task WriteStreamData(
    Stream source,
    CancellationToken ct = default)
  {
    ExpectPhase(MvltPhase.Data);
    int n;
    while((n = await source.ReadAsync(
      _uncompressedBuffer.Memory(0, _uncompressedBuffer.Length), ct))> 0)
    {
      await WriteBufferedData(n, ct);
    }
  }

  /// <summary>
  /// Write a block of data to the MVLT file. An attempt is made to
  /// compress the data. Depending on the outcome the data is written
  /// compressed or uncompressed.
  /// </summary>
  /// <param name="data">
  /// The data to write. The size of the data chunk should be less than
  /// 0x0D0000 bytes (<see cref="MvltFormat.MvltChunkSize"/>).
  /// </param>
  /// <param name="ct"></param>
  /// <returns></returns>
  public async Task WriteData(
    Memory<byte> data,
    CancellationToken ct = default)
  {
    ExpectPhase(MvltPhase.Data);
    if(data.Length > _uncompressedBuffer.Length)
    {
      throw new ArgumentOutOfRangeException(
        nameof(data),
        $"Data block too large. Maximum size is {_uncompressedBuffer.Length}");
    }
    data.Span.CopyTo(_uncompressedBuffer.Span(0, data.Length));
    var length = data.Length;
    await WriteBufferedData(length, ct);
  }

  /// <summary>
  /// Writes the <paramref name="length"/> bytes of data that are already
  /// buffered in <see cref="_uncompressedBuffer"/>. This tries to compress
  /// the data and write it compressed if reasonable.
  /// </summary>
  private async Task WriteBufferedData(int length, CancellationToken ct)
  {
    var compressedSize = VaultCompressor.Compress(
      _uncompressedBuffer, length, _compressedBuffer);
    if(compressedSize < 0)
    {
      await WriteUncompressedData(
        _uncompressedBuffer.Memory(0, length), ct);
    }
    else
    {
      await WriteCompressedData(
        _compressedBuffer.Memory(0, compressedSize), length, ct);
    }
  }

  /// <summary>
  /// Write a compressed block of data to the MVLT file. Normally called
  /// through <see cref="WriteData(Memory{byte}, CancellationToken)"/>.
  /// </summary>
  /// <param name="compressedData">
  /// The pre-compressed unencrypted data.
  /// </param>
  /// <param name="unpackedSize">
  /// The size of the data before compression.
  /// </param>
  /// <param name="ct"></param>
  /// <returns></returns>
  public async Task WriteCompressedData(
    Memory<byte> compressedData,
    int unpackedSize,
    CancellationToken ct = default)
  {
    ExpectPhase(MvltPhase.Data);
    if(unpackedSize < 0)
    {
      throw new ArgumentException(
        "Unpacked size must be non-negative");
    }
    if(compressedData.Length > unpackedSize)
    {
      throw new ArgumentException(
        "Compressed data must be no larger than the unpacked size");
    }
    await WriteBlock(
      MvltFormat.CompressedBlock4CC,
      unpackedSize,
      compressedData,
      ct);
    _totalLength += unpackedSize;
  }

  /// <summary>
  /// Write a block of uncompressed data to the MVLT file.
  /// Normally called through <see cref="WriteData(Memory{byte}, CancellationToken)"/>.
  /// </summary>
  /// <param name="data">
  /// The (uncompressable) data to write.
  /// </param>
  /// <param name="ct"></param>
  /// <returns></returns>
  public async Task WriteUncompressedData(
    Memory<byte> data,
    CancellationToken ct = default)
  {
    ExpectPhase(MvltPhase.Data);
    await WriteBlock(
      MvltFormat.UncompressedBlock4CC,
      data.Length,
      data,
      ct);
    _totalLength += data.Length;
  }

  /// <summary>
  /// Writes a block of data to the MVLT file.
  /// This method takes care of the encryption and the
  /// chaining of the associated data.
  /// </summary>
  /// <param name="kind4cc">
  /// The block type.
  /// </param>
  /// <param name="unpackedSize">
  /// The uncompressed size of the data. For uncompressed blocks
  /// this is the same as the size of the data.
  /// </param>
  /// <param name="data">
  /// The data to write. In case of compressed data, this is the
  /// compressed unencrypted data.
  /// </param>
  /// <param name="ct"></param>
  /// <returns></returns>
  private async Task WriteBlock(
    uint kind4cc,
    int unpackedSize,
    Memory<byte> data,
    CancellationToken ct = default)
  {
    var totalLength = 0x28 + data.Length;
    if(totalLength > _blockBuffer.Length)
    {
      throw new ArgumentOutOfRangeException(
        nameof(data),
        $"Data block too large. Maximum size is {_blockBuffer.Length}");
    }
    var buffer = _blockBuffer.Memory(0, totalLength);
    var tagOut = new byte[16]; // needs to be separate from _lastTag
    _cryptor.Encrypt(
      _lastTag,
      data.Span,
      buffer.Span.Slice(0x28, data.Length),
      buffer.Span.Slice(12, 12),
      tagOut);
    tagOut.AsSpan().CopyTo(_lastTag);
    BinaryPrimitives.WriteUInt32LittleEndian(
      buffer.Span.Slice(0, 4), kind4cc);
    BinaryPrimitives.WriteUInt32LittleEndian(
      buffer.Span.Slice(4, 4), (uint)totalLength);
    BinaryPrimitives.WriteUInt32LittleEndian(
      buffer.Span.Slice(8, 4), (uint)unpackedSize);
    tagOut.AsSpan(0, 16).CopyTo(buffer.Span.Slice(24, 16));
    await _output.WriteAsync(buffer, ct);
  }

  private void ExpectPhase(MvltPhase phase)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    if(phase != Phase)
    {
      throw new InvalidOperationException(
        $"Expecting phase {phase} but current phase is {Phase}");
    }
  }

  /// <summary>
  /// Clean up
  /// </summary>
  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      Phase = MvltPhase.Disposed;
      _cryptor.Dispose();
      if(_ownsOutput)
      {
        _output.Dispose();
      }
      else
      {
        _output.Flush();
      }
      _blockBuffer.Dispose();
      _uncompressedBuffer.Dispose();
      _compressedBuffer.Dispose();
    }
  }
}
