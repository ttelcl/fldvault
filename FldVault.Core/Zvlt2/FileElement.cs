/*
 * (c) 2023  ttelcl / ttelcl
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

using Newtonsoft.Json;

namespace FldVault.Core.Zvlt2
{
  /// <summary>
  /// Gathers the information about an encrypted file inside a side.
  /// </summary>
  public class FileElement
  {
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
      RootElement = rootElement;
      var metaElement = RootElement.Children.FirstOrDefault(e => e.Block.Kind == Zvlt2BlockType.FileMetadata);
      MetadataBlock = metaElement?.Block ?? throw new InvalidOperationException(
        "Missing file metadata block");
      ContentBlocks =
        RootElement.Children
          .Where(e => e.Block.Kind == Zvlt2BlockType.FileContent)
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
    /// Read the content of the file header block
    /// </summary>
    public FileHeader ReadHeader(VaultFileReader reader)
    {
      reader.SeekBlock(HeaderBlock);
      Span<byte> span = stackalloc byte[8];
      reader.ReadSpan(span);
      new SpanReader()
        .ReadI64(span, out var encryptionTicks)
        .CheckEmpty(span);
      return new FileHeader(encryptionTicks);
    }

    /// <summary>
    /// Read the metadata record for this FileElement
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
    public FileMetadata ReadMetadata(
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
        reader.DecryptFragment(aux, authTagOut, plaintext);
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
