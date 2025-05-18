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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FldVault.Core.Mvlt;

/// <summary>
/// Utility class for reading MVLT files.
/// </summary>
public class MvltReader: IDisposable
{
  private readonly Stream _input;
  private long _virtualOffset = 0;
  private bool _disposed;
  private readonly bool _ownsInput;
  private readonly VaultCryptor? _cryptor;
  private readonly byte[] _lastTag;
  private readonly ByteCryptoBuffer _blockBuffer;
  private readonly ByteCryptoBuffer _decryptedBuffer;

  private readonly ByteCryptoBuffer _sourceBuffer;
  private Memory<byte>? _sourceMemory = null;
  private Memory<byte>? _decryptedMemory = null;
  private uint _blockType;
  private uint _blockSize;
  private uint _blockOriginalSize;
  private byte[] _nonceBuffer = new byte[12];
  private byte[] _tagBuffer = new byte[16];

  /// <summary>
  /// Create a new MvltReader
  /// </summary>
  internal MvltReader(
    KeyChain? keyChain,
    MvltFileHeader header,
    Stream input,
    bool ownsInput,
    NonceGenerator? nonceGenerator = null)
  {
    _input = input;
    _ownsInput = ownsInput;
    KeyDescriptor = header.KeyInfoFile;
    _cryptor = keyChain == null ? null : new VaultCryptor(
      keyChain,
      KeyDescriptor.KeyId,
      EpochTicks.ToUtc(header.Stamp),
      nonceGenerator);
    _lastTag = new byte[16];
    _blockBuffer = new ByteCryptoBuffer(MvltFormat.MvltChunkSize + 0x010000);
    _decryptedBuffer = new ByteCryptoBuffer(MvltFormat.MvltChunkSize + 0x010000);
    _sourceBuffer = new ByteCryptoBuffer(MvltFormat.MvltChunkSize + 0x010000);
    Phase = MvltPhase.AfterHeader;
    header.PreHeader[..16].CopyTo(_lastTag);
    _virtualOffset = header.HeaderByteCount;
  }

  /// <summary>
  /// If true, this reader does not have access to the key, and can
  /// not decrypt the data. Functionality is limited to providing information
  /// on the file structure, not content.
  /// </summary>
  public bool Keyless => _cryptor == null;

  /// <summary>
  /// Current file position (tracked manually, since the input may be
  /// not seekable)
  /// </summary>
  public long VirtualOffset => _virtualOffset;

  /// <summary>
  /// The key descriptor for the MVLT file.
  /// </summary>
  public PassphraseKeyInfoFile KeyDescriptor { get; }

  /// <summary>
  /// The current reading phase of the MVLT file.
  /// </summary>
  public MvltPhase Phase { get; private set; }

  /// <summary>
  /// The block type that was just loaded
  /// </summary>
  public uint BlockType => _blockType;

  /// <summary>
  /// The total size of the block that was just loaded
  /// </summary>
  public uint TotalBlockSize => _blockSize;

  /// <summary>
  /// The content size of the block that was just loaded (excluding the header)
  /// </summary>
  public uint BlockContentSize => _blockSize - 40;

  /// <summary>
  /// The orignal size of the block before compression
  /// </summary>
  public uint BlockOriginalSize => _blockOriginalSize;

  /// <summary>
  /// Return the decrypted memtadata. This is only valid for the
  /// Preamble and terminator blocks.
  /// </summary>
  public string GetMetadataText()
  {
    if(_cryptor == null)
    {
      throw new InvalidOperationException(
        $"This MVLT reader is info-only. It cannot decrypt content or metadata.");
    }
    if(_blockType != MvltFormat.Preamble4CC && _blockType != MvltFormat.Terminator4CC)
    {
      throw new InvalidOperationException(
        $"Expecting a metadata block (preamble or terminator), but it was {_blockType}");
    }
    if(_decryptedMemory == null)
    {
      throw new InvalidOperationException(
        $"Expecting a decrypted memory buffer, but it was null");
    }
    var json = Encoding.UTF8.GetString(_decryptedMemory.Value.Span);
    //var metadata =
    //  JsonConvert.DeserializeObject<JObject>(json)
    //  ?? throw new InvalidDataException("Invalid JSON bytes");
    //return metadata;
    return json;
  }

  /// <summary>
  /// Instead of decrypting and using the block, this just moves
  /// the tag to the last tag buffer and updates the phase. This allows
  /// scanning the file without decrypting it.
  /// </summary>
  /// <returns>
  /// True if more blocks are expected, false if the terminator block
  /// was found or the reader was disposed.
  /// </returns>
  public bool IgnoreBlock()
  {
    var phase = ValidatePhase(); // includes disposed check
    _tagBuffer.AsSpan().CopyTo(_lastTag);
    Phase = phase;
    return Phase < MvltPhase.End;
  }

  /// <summary>
  /// Decrypts the currently loaded block and returns the next phase.
  /// Does not change the phase, but returns the new phase.
  /// </summary>
  public MvltPhase DecryptBlock()
  {
    var nextPhase = ValidatePhase();  // includes disposed check
    if(_sourceMemory == null)
    {
      throw new InvalidOperationException(
        $"Expecting a source memory buffer, but it was null");
    }
    if(_cryptor == null)
    {
      throw new InvalidOperationException(
        $"This MVLT reader is info-only. It cannot decrypt content or metadata.");
    }
    var decryptedMemory = _decryptedBuffer.Memory(0, _sourceMemory.Value.Length);
    _decryptedMemory = decryptedMemory;
    _cryptor.Decrypt(
      _lastTag,
      _nonceBuffer,
      _tagBuffer,
      _sourceMemory.Value.Span,
      decryptedMemory.Span);
    _tagBuffer.AsSpan().CopyTo(_lastTag);
    return nextPhase;
  }

  /// <summary>
  /// Cycles the phase to the next phase (after <see cref="DecryptBlock"/>)
  /// </summary>
  public MvltPhase CyclePhase(MvltPhase nextPhase)
  {
    var oldPhase = Phase;
    Phase = nextPhase;
    return oldPhase;
  }

  /// <summary>
  /// Validates the block type considering the current phase.
  /// Returns the new phase.
  /// </summary>
  public MvltPhase ValidatePhase()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    switch(Phase)
    {
      case MvltPhase.BeforeHeader:
        // We are already in AfterHeader after the constructor completes
        throw new InvalidOperationException(
          $"Invalid phase {Phase} for MvltReader");
      case MvltPhase.AfterHeader:
        if(_blockType != MvltFormat.Preamble4CC)
        {
          throw new InvalidOperationException(
            $"Invalid block type {_blockType}. Expecting Preamble");
        }
        return MvltPhase.Data;
      case MvltPhase.Data:
        if(_blockType == MvltFormat.UncompressedBlock4CC
          || _blockType == MvltFormat.CompressedBlock4CC)
        {
          // stay in data phase
          return MvltPhase.Data;
        }
        if(_blockType != MvltFormat.Terminator4CC)
        {
          throw new InvalidOperationException(
            $"Invalid block type {_blockType}. Expecting Terminator or more data");
        }
        return MvltPhase.End;
      case MvltPhase.End:
        throw new InvalidOperationException(
          $"Invalid phase {Phase}. Not expecting more data");
      case MvltPhase.Disposed:
        throw new InvalidOperationException(
          $"Invalid phase {Phase}. Reader was already disposed");
      default:
        throw new InvalidOperationException(
          $"Unrecognized phase {Phase}");
    }
  }

  /// <summary>
  /// Loads the next block from the stream into this object's raw buffer
  /// and state variables. Does not decrypt the block nor validate the
  /// phase.
  /// </summary>
  /// <returns>
  /// The file offset before the read started.
  /// </returns>
  public async Task<long> LoadNextBlock(CancellationToken ct = default)
  {
    var offset = _virtualOffset;
    _sourceMemory = null;
    _decryptedMemory = null;
    ObjectDisposedException.ThrowIf(_disposed, this);
    if(Phase>=MvltPhase.End )
    {
      throw new InvalidOperationException(
        $"Invalid phase {Phase}. Not expecting more data");
    }
    var blockHeader = new byte[40];
    var bytesRead = await _input.ReadAsync(blockHeader, ct);
    if(bytesRead != blockHeader.Length)
    {
      throw new EndOfStreamException(
        $"Expecting {blockHeader.Length} bytes, but only {bytesRead} bytes were read");
    }
    _blockType = BinaryPrimitives.ReadUInt32LittleEndian(blockHeader.AsSpan(0, 4));
    _blockSize = BinaryPrimitives.ReadUInt32LittleEndian(blockHeader.AsSpan(4, 4));
    _blockOriginalSize = BinaryPrimitives.ReadUInt32LittleEndian(blockHeader.AsSpan(8, 4));
    blockHeader.AsSpan(12, 12).CopyTo(_nonceBuffer);
    blockHeader.AsSpan(24, 16).CopyTo(_tagBuffer);
    var sourceMemory = _sourceBuffer.Memory(0, (int)_blockSize - 40);
    _sourceMemory = sourceMemory;
    bytesRead = await _input.ReadAsync(sourceMemory, ct);
    if(bytesRead != _blockSize - 40)
    {
      throw new EndOfStreamException(
        $"Expecting {_blockSize - 40} bytes, but only {bytesRead} bytes were read");
    }
    _virtualOffset += _blockSize;
    return offset;
  }

  /// <summary>
  /// Clean up resources
  /// </summary>
  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      if(_ownsInput)
      {
        _input.Dispose();
      }
      _sourceMemory = null;
      _decryptedMemory = null;
      _cryptor?.Dispose();
      _blockBuffer.Dispose();
      _decryptedBuffer.Dispose();
      _sourceBuffer.Dispose();
      Phase = MvltPhase.Disposed;
    }
  }
}
