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

namespace FldVault.Core.Vaults;

/// <summary>
/// Utility to write a vault to a stream
/// </summary>
public class VaultWriter
{
  private KeyBuffer _keyBuffer;
  private DateTime _writeTime;
  private DateTime _originalTime;
  private long _signature;
  private bool _expectName;
  private bool _expectFileBlob;
  private bool _expectSecretBlob;
  private readonly NonceGenerator _nonceGenerator;
  private CryptoBuffer<byte>? _chunkBuffer; 

  /// <summary>
  /// Create a new VaultWriter
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
    w.Write(_keyBuffer.GetId().ToByteArray());
    w.Write(_writeTime.Ticks);
    w.Write(_originalTime.Ticks);
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
  public void WriteFileNameSegment(BinaryWriter w, string fileName)
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
      WriteTerminatorChunk(w);
    }
  }

  /// <summary>
  /// Write a file content segment
  /// </summary>
  /// <param name="w"></param>
  /// <param name="source"></param>
  /// <exception cref="InvalidOperationException"></exception>
  public void WriteContentSegment(BinaryWriter w, Stream source)
  {
    if(!_expectFileBlob)
    {
      throw new InvalidOperationException(
        "Not expecting a file content segment for this vault subtype");
    }
    var sourceLength = source.Length;
    var chunkCount = (sourceLength + VaultFormat.MaxChunkSize - 1) / VaultFormat.MaxChunkSize;
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
      while(bytesLeft > 0)
      {
        var n = source.Read(span);
        bytesLeft -= n;
        WriteChunk(w, encryptor, span.Slice(0, n));
      }
      WriteTerminatorChunk(w);
    }
  }

  /// <summary>
  /// Write a non-file content segment
  /// </summary>
  /// <param name="w"></param>
  /// <param name="source"></param>
  /// <exception cref="InvalidOperationException"></exception>
  public void WriteSecretSegment(BinaryWriter w, CryptoBuffer<byte> source)
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
      WriteTerminatorChunk(w);
    }

    throw new NotImplementedException();
  }

  /// <summary>
  /// Write a pseudo-chunk as terminator of the chunk list of a segment
  /// </summary>
  /// <param name="w"></param>
  private void WriteTerminatorChunk(BinaryWriter w)
  {
    w.Write(0L);
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
    w.Write(_chunkBuffer.Span());
  }

}
