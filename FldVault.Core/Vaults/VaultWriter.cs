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

using FldVault.Core.Crypto;
using FldVault.Core.Utilities;

namespace FldVault.Core.Vaults;

/// <summary>
/// Utility to write a vault to a stream
/// </summary>
public class VaultWriter
{
  private readonly KeyBuffer _keyBuffer;
  private readonly DateTime _writeTime;
  private readonly DateTime _originalTime;
  private readonly long _signature;
  private readonly bool _expectName;
  private readonly bool _expectFileBlob;
  private readonly bool _expectSecretBlob;
  private readonly NonceGenerator _nonceGenerator;
  private CryptoBuffer<byte>? _chunkBuffer;
  private const int __version = VaultFormat.VaultFileVersion;

  /// <summary>
  /// Create a new VaultWriter (format 0x00010001)
  /// </summary>
  public VaultWriter(
    long signature,
    KeyBuffer key,
    DateTime? originalTime = null,
    DateTime? writeTime = null)
  {
    _writeTime = writeTime.HasValue ? writeTime.Value.ToUniversalTime() : DateTime.UtcNow;
    _originalTime = originalTime.HasValue ? originalTime.Value.ToUniversalTime() : _writeTime;
    _keyBuffer = key;
    _signature = signature;
    _nonceGenerator = new NonceGenerator();
    switch(signature)
    {
      case VaultFormat.VaultSignatureFile:
        _expectName = true;
        _expectFileBlob = true;
        _expectSecretBlob = false;
        break;
      case VaultFormat.VaultSignatureSecret:
        _expectName = false;
        _expectFileBlob = false;
        _expectSecretBlob = true;
        break;
      default:
        throw new InvalidOperationException(
          "Unknown signature");
    }
  }

  /// <summary>
  /// Write the vault header
  /// </summary>
  public void WriteHeader(BinaryWriter w)
  {
    w.Write(_signature);
    w.Write((int)__version);
    w.Write((int)0); // unused
    w.Write(_keyBuffer.GetId().ToByteArray());
    w.Write(EpochTicks.FromUtc(_writeTime));
    w.Write(EpochTicks.FromUtc(_originalTime));
  }

  
  /// <summary>
  /// Write the segment for the file name
  /// </summary>
  /// <param name="w">
  /// A writer on the vault
  /// </param>
  /// <param name="fileName">
  /// The file name to store in the vault
  /// </param>
  public Guid WriteFileNameSegment(BinaryWriter w, string fileName)
  {
    if(!_expectName)
    {
      throw new InvalidOperationException(
        "Not expecting a file name for this vault subtype");
    }
    var bytes = Encoding.UTF8.GetBytes(fileName);
    if(bytes.Length > VaultFormat.MaxChunkSize)
    {
      throw new InvalidOperationException(
        "File name too long."); // a file name encoding to over 256K UTF bytes ???
    }
    using(var encryptor = new VaultSegmentEncryptor(
      _keyBuffer, _nonceGenerator, (short)SegmentKind.Name, bytes.Length, _writeTime))
    {
      w.Write(encryptor.LengthCode.PackedValue);
      w.Write(1);
      WriteChunk(w, encryptor, bytes);
      FinishSegment(w);
      return encryptor.LatestTagGuid;
    }
  }

  /// <summary>
  /// Write a file content segment
  /// </summary>
  /// <param name="w"></param>
  /// <param name="source"></param>
  /// <exception cref="InvalidOperationException"></exception>
  public Guid WriteContentSegment(BinaryWriter w, Stream source)
  {
    if(!_expectFileBlob)
    {
      throw new InvalidOperationException(
        "Not expecting a file content segment for this vault subtype");
    }
    var sourceLength = source.Length;
    int chunkCount = (int)((sourceLength + VaultFormat.MaxChunkSize - 1) / VaultFormat.MaxChunkSize);
    if(chunkCount == 0)
    {
      // Probably requires special-casing
      throw new NotSupportedException("Chunk count 0 (empty source file) is not yet supported");
    }
    var bytesLeft = sourceLength;
    using(var encryptor = new VaultSegmentEncryptor(
      _keyBuffer, _nonceGenerator, (short)SegmentKind.File, sourceLength, _writeTime))
    using(var inputBuffer = new CryptoBuffer<byte>(VaultFormat.MaxChunkSize))
    {
      w.Write(encryptor.LengthCode.PackedValue);
      w.Write(chunkCount);
      var span = inputBuffer.Span();
      var remainingChunks = chunkCount;
      while(bytesLeft > 0)
      {
        var n = source.Read(span);
        bytesLeft -= n;
        WriteChunk(w, encryptor, span.Slice(0, n));
        remainingChunks--;
      }
      FinishSegment(w);
      if(remainingChunks != 0)
      {
        throw new InvalidOperationException(
          $"Internal error in chunk count calculation ({remainingChunks} / {chunkCount})");
      }
      return encryptor.LatestTagGuid;
    }
  }

  /// <summary>
  /// Write a non-file content segment
  /// </summary>
  /// <param name="w"></param>
  /// <param name="source"></param>
  /// <exception cref="InvalidOperationException"></exception>
  public Guid WriteSecretSegment(BinaryWriter w, CryptoBuffer<byte> source)
  {
    if(!_expectSecretBlob)
    {
      throw new InvalidOperationException(
        "Not expecting a non-file content segment for this vault subtype");
    }
    if(source.Length > VaultFormat.MaxChunkSize)
    {
      throw new InvalidOperationException(
        "Non-File blob is too long (maximum is 256 kb).");
    }
    using(var encryptor = new VaultSegmentEncryptor(
      _keyBuffer, _nonceGenerator, (short)SegmentKind.SecretBlob, source.Length, _writeTime))
    {
      w.Write(encryptor.LengthCode.PackedValue);
      w.Write(1);
      WriteChunk(w, encryptor, source.Span());
      FinishSegment(w);
      return encryptor.LatestTagGuid;
    }
  }

  /// <summary>
  /// Cleanup buffer used during segment writing
  /// </summary>
  /// <param name="w"></param>
  private void FinishSegment(BinaryWriter w)
  {
    if(_chunkBuffer != null)
    {
      _chunkBuffer.Dispose();
      _chunkBuffer = null;
    }
  }

  private void WriteChunk(BinaryWriter w, VaultSegmentEncryptor cryptor, ReadOnlySpan<byte> chunk)
  {
    if(_chunkBuffer == null)
    {
      _chunkBuffer = new CryptoBuffer<byte>(VaultFormat.MaxChunkSize);
    }
    Span<byte> nonce = stackalloc byte[12];
    Span<byte> authenticationTag = stackalloc byte[16];
    cryptor.Encrypt(chunk, _chunkBuffer.Span(0, chunk.Length), nonce, authenticationTag);
    w.Write(chunk.Length + 32);
    w.Write(nonce);
    w.Write(authenticationTag);
    w.Write(_chunkBuffer.Span(0, chunk.Length));
  }

}
