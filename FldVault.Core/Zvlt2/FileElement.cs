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

namespace FldVault.Core.Zvlt2
{
  /// <summary>
  /// Gathers the information about an encrypted file inside a side.
  /// </summary>
  public class FileElement
  {
    private FileHeader? _cachedHeader = null;
    private FileMetadata? _cachedMetadata = null;
    private byte[]? _cachedMetaAuthtag = null;

    /// <summary>
    /// Create a new FileElement
    /// </summary>
    public FileElement(
      IBlockElement rootElement)
    {
      if(rootElement.Block.Kind != Zvlt2BlockType.FileHeader)
      {
        throw new ArgumentOutOfRangeException(nameof(rootElement), 
          "Expecting a file header block");
      }
      if(rootElement.Children[^1].Block.Kind != BlockType.ImpliedGroupEnd)
      {
        throw new ArgumentOutOfRangeException(nameof(rootElement), 
          "Unrecognized file element structure: missing element group terminator");
      }
      RootElement = rootElement.ExpectContentLength(24);
      var metaElement = RootElement.Children.FirstOrDefault(e => e.Block.Kind == Zvlt2BlockType.FileMetadata);
      MetadataBlock = metaElement?.Block ?? throw new InvalidOperationException(
        "Missing file metadata block");
      ContentBlocks =
        RootElement.Children
          .Where(e => e.Block.Kind == Zvlt2BlockType.FileContentV3)
          .Select(e => e.Block)
          .ToList();
    }

    /// <summary>
    /// The root "FLX(" element
    /// </summary>
    public IBlockElement RootElement { get; }

    /// <summary>
    /// The file element header block
    /// </summary>
    public IBlockInfo HeaderBlock { get => RootElement.Block; }

    /// <summary>
    /// The file element metadata block
    /// </summary>
    public IBlockInfo MetadataBlock { get; init; }

    /// <summary>
    /// The content blocks
    /// </summary>
    public IReadOnlyList<IBlockInfo> ContentBlocks { get; init; }

    /// <summary>
    /// Get the cached <see cref="FileHeader"/> instance,
    /// reading and caching it first if not already done so
    /// </summary>
    /// <param name="reader">
    /// The reader to read from if not yet cached.
    /// </param>
    /// <param name="reload">
    /// If true, the cached copy is re-read even if already available
    /// </param>
    public FileHeader GetHeader(VaultFileReader reader, bool reload = false)
    {
      if(_cachedHeader == null || reload)
      {
        _cachedHeader = ReadHeader(reader);
      }
      return _cachedHeader;
    }

    /// <summary>
    /// Get the cached <see cref="FileMetadata"/> instance and
    /// its authentication tag, reading it from the file if not already
    /// done so.
    /// </summary>
    /// <param name="reader">
    /// The reader to read from if not yet cached.
    /// </param>
    /// <param name="metaAuthTagOut">
    /// The 16 byte buffer to receive the authentication tag
    /// </param>
    /// <param name="reload">
    /// If true, the cached copy is re-read even if already available
    /// </param>
    public FileMetadata GetMetadata(
      VaultFileReader reader,
      Span<byte> metaAuthTagOut,
      bool reload = false)
    {
      if(_cachedMetadata == null || reload)
      {
        _cachedMetaAuthtag = new byte[16];
        _cachedMetadata = ReadMetadata(reader, GetHeader(reader, reload), _cachedMetaAuthtag);
      }
      _cachedMetaAuthtag.CopyTo(metaAuthTagOut);
      return _cachedMetadata;
    }

    /// <summary>
    /// Get the cached <see cref="FileMetadata"/> instance, reading it
    /// from the file if not already done so.
    /// </summary>
    /// <param name="reader">
    /// The reader to read from if not yet cached.
    /// </param>
    /// <param name="reload">
    /// If true, the cached copy is re-read even if already available
    /// </param>
    public FileMetadata GetMetadata(
      VaultFileReader reader,
      bool reload = false)
    {
      Span<byte> authTagBuffer = stackalloc byte[16];
      return GetMetadata(reader, authTagBuffer, reload);
    }

    /// <summary>
    /// Decode the content of this file element and save it to the
    /// specified stream.
    /// </summary>
    /// <param name="reader">
    /// The vault reader for the vault this element is part of
    /// </param>
    /// <param name="destination">
    /// The destination stream
    /// </param>
    public void SaveContentToStream(
      VaultFileReader reader,
      Stream destination)
    {
      using(var buffer = new CryptoBuffer<byte>(VaultFormat.VaultChunkSize))
      {
        Span<byte> authTagOut = stackalloc byte[16];
        Span<byte> authTagIn = stackalloc byte[16];
        _ = GetMetadata(reader, authTagOut);
        foreach(var ibi in ContentBlocks)
        {
          authTagOut.CopyTo(authTagIn);
          var fch = FileContentHeader.Read(reader, ibi);
          var plaintext = buffer.Span(0, ibi.Size - 40);
          reader.DecryptFragment(authTagIn, authTagOut, plaintext, fch.BlockInfo);
          destination.Write(plaintext);
        }
      }
    }

    /// <summary>
    /// Decode the content of this file element and save it to a file.
    /// </summary>
    /// <param name="reader">
    /// The vault reader wrapping the vault file and the key
    /// </param>
    /// <param name="rootFolder">
    /// The folder used to resolve a relative filename (default ".", 
    /// i.e. the current directory)
    /// </param>
    /// <param name="fileName">
    /// The file name to save to. If null, the file name in the metadata
    /// is used instead. In either case, a relative file name is resolved
    /// relative to <paramref name="rootFolder"/>. It is an error if both
    /// this is null and the metadata does not specify a name.
    /// </param>
    /// <param name="setStamp">
    /// When true (default) and the metadata provides a last write stamp:
    /// restore that last modified timestamp.
    /// </param>
    /// <param name="checkFolder">
    /// When true (default) this method refuses to save files in the same
    /// folder as the vault file. Set to false to disable this feature.
    /// See remarks section.
    /// </param>
    /// <exception cref="InvalidOperationException"></exception>
    /// <remarks>
    /// <para>
    /// In case you wonder why decrypting content to the same folder as
    /// the vault file is discouraged: it is assumed that the vault folder
    /// is not necessarily as secure as the folder where you would want to
    /// put the decrypted file. Whether it is or not is ultimately something
    /// the user has to consciously decide.
    /// </para>
    /// </remarks>
    public void SaveContentToFile(
      VaultFileReader reader,
      string rootFolder = ".",
      string? fileName = null,
      bool setStamp = true,
      bool checkFolder = true)
    {
      rootFolder = Path.GetFullPath(rootFolder);
      Span<byte> authTagOut = stackalloc byte[16];
      var meta = GetMetadata(reader, authTagOut);
      fileName = fileName ?? meta.Name;
      if(String.IsNullOrEmpty(fileName))
      {
        throw new InvalidOperationException(
          "No filename provided and no default filename is available.");
      }
      fileName = Path.Combine(rootFolder, fileName);
      var folder = Path.GetDirectoryName(fileName)!;
      if(!Directory.Exists(folder))
      {
        Directory.CreateDirectory(folder);
      }
      else if(checkFolder) // we can skip this check if the folder did not exist
      {
        var vaultFolder = Path.GetDirectoryName(reader.Vault.FileName)!;
        var probeName = $"{Guid.NewGuid()}.probe";
        var vaultProbe = Path.Combine(vaultFolder, probeName);
        var destinationProbe = Path.Combine(folder, probeName);
        File.Create(destinationProbe).Dispose();
        var failed = File.Exists(vaultProbe);
        File.Delete(destinationProbe);
        if(failed)
        {
          throw new InvalidOperationException(
            "Denied for security reasons: attempt to extract vault content into the same folder as the *.zvlt vault itself.");
        }
      }
      using(var stream = File.Create(fileName))
      {
        SaveContentToStream(reader, stream);
      }
      if(setStamp && meta.Stamp.HasValue)
      {
        var utcStamp = EpochTicks.ToUtc(meta.Stamp.Value);
        File.SetLastWriteTimeUtc(fileName, utcStamp);
      }
    }

    /// <summary>
    /// Get the total length of the encrypted content (summing
    /// the lengths of the content in all content blocks)
    /// </summary>
    public long GetContentLength()
    {
      return ContentBlocks.Sum(ibi => (ibi.Size - 36L));
    }

    /// <summary>
    /// Read the content of the file header block.
    /// Normally invoked indirectly via <see cref="GetHeader(VaultFileReader, bool)"/>
    /// </summary>
    private FileHeader ReadHeader(VaultFileReader reader)
    {
      reader.SeekBlock(HeaderBlock);
      Span<byte> span = stackalloc byte[HeaderBlock.ContentLength(24)];
      reader.ReadSpan(span);
      new SpanReader()
        .ReadI64(span, out var encryptionTicks)
        .ReadGuid(span, out var fileId)
        .CheckEmpty(span);
      return new FileHeader(encryptionTicks, fileId);
    }

    /// <summary>
    /// Read the metadata record for this FileElement
    /// Normally invoked indirectly via <see cref="GetMetadata(VaultFileReader, Span{byte}, bool)"/>
    /// </summary>
    /// <param name="reader">
    /// The vault reader (including the vault key)
    /// </param>
    /// <param name="fileHeader">
    /// The vault header information (which is taken into account in the AES-GCM
    /// authentication tag)
    /// </param>
    /// <param name="authTagOut">
    /// The buffer to receive the authentication tag (for chaining as associated data
    /// into the content block decryptions)
    /// </param>
    /// <returns>
    /// The FileMetadata deserialized from the JSON decrypted from the block
    /// </returns>
    private FileMetadata ReadMetadata(
      VaultFileReader reader,
      FileHeader fileHeader,
      Span<byte> authTagOut)
    {
      reader.SeekBlock(MetadataBlock);
      Span<byte> aux = stackalloc byte[24];
      new SpanWriter()
        .WriteI32(aux, MetadataBlock.Kind)
        .WriteI32(aux, MetadataBlock.Size)
        .WriteI64(aux, fileHeader.EncryptionStamp)
        .WriteI64(aux, EpochTicks.FromUtc(reader.Vault.Header.TimeStamp))
        .CheckFull(aux);
      var size = MetadataBlock.Size - 8 - 12 - 16;
      using(var buffer = new CryptoBuffer<byte>(size))
      {
        var plaintext = buffer.Span();
        reader.DecryptFragment(aux, authTagOut, plaintext, MetadataBlock);
        var json = Encoding.UTF8.GetString(plaintext);
        var meta = JsonConvert.DeserializeObject<FileMetadata>(json);
        if(meta == null)
        {
          throw new InvalidOperationException(
            "Invalid file metadata entry");
        }
        return meta;
      }
    }
  }
}
