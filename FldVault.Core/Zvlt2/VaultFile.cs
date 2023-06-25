/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.BlockFiles;
using FldVault.Core.Crypto;
using FldVault.Core.Utilities;
using FldVault.Core.Vaults;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Caches information about a ZVLT v2 file. Does not itself wrap
/// an open file handle. Requires the file to exist and have at least
/// the file header block.
/// </summary>
public class VaultFile
{
  private PassphraseKeyInfoFile? _pkifCache;
  private bool _pkifSearched;

  /// <summary>
  /// Create a new VaultFile object for an existing *.zvlt file
  /// </summary>
  public VaultFile(
    string fileName)
  {
    FileName = Path.GetFullPath(fileName);
    Blocks = new BlockInfoList();
    if(!File.Exists(FileName))
    {
      throw new FileNotFoundException(
        "File not found", FileName);
    }
    using(var stream = File.OpenRead(FileName))
    {
      Header = VaultHeader.ReadSync(stream);
      stream.Position = 0;
      Blocks.Reload(stream);
      GetPassphraseInfo(stream); // caches it if found
    }
  }

  /// <summary>
  /// Open an existing vault file or create a new one. If an existing
  /// file is opened, the key must match the given ID. This overload
  /// does not create a PASS block.
  /// </summary>
  /// <param name="fileName">
  /// The name of the file to open or create
  /// </param>
  /// <param name="keyId">
  /// The key ID for the new file, or the expected key ID for an existing file
  /// </param>
  /// <param name="stamp">
  /// The creation time stamp in UTC, or null to use the current time.
  /// This argument primarily exists to support Unit Tests.
  /// </param>
  /// <returns>
  /// The VaultFile instance
  /// </returns>
  public static VaultFile OpenOrCreate(string fileName, Guid keyId, DateTime? stamp = null)
  {
    fileName = Path.GetFullPath(fileName);
    if(File.Exists(fileName))
    {
      var vf = new VaultFile(fileName);
      if(vf.KeyId != keyId)
      {
        throw new InvalidOperationException(
          "The key ID does not match the existing *.zvlt file");
      }
      return vf;
    }
    else
    {
      using(var stream = File.Create(fileName))
      {
        VaultHeader.WriteSync(stream, keyId, stamp);
      }
      return new VaultFile(fileName);
    }
  }

  /// <summary>
  /// Open an existing vault file or create a new one. If an existing
  /// file is opened, the key must match the ID in the key-info.
  /// This overload also creates a PASS block if it creates a new vault file.
  /// </summary>
  /// <param name="fileName">
  /// The name of the file to open or create
  /// </param>
  /// <param name="keyInfo">
  /// The key descriptor for the new file, or the expected key descriptor for an existing file
  /// </param>
  /// <param name="stamp">
  /// The creation time stamp in UTC, or null to use the current time.
  /// This argument primarily exists to support Unit Tests.
  /// </param>
  /// <returns>
  /// The VaultFile instance
  /// </returns>
  public static VaultFile OpenOrCreate(string fileName, PassphraseKeyInfoFile keyInfo, DateTime? stamp = null)
  {
    fileName = Path.GetFullPath(fileName);
    if(File.Exists(fileName))
    {
      var vf = new VaultFile(fileName);
      if(vf.KeyId != keyInfo.KeyId)
      {
        throw new InvalidOperationException(
          "The key ID does not match the existing *.zvlt file");
      }
      return vf;
    }
    else
    {
      using(var stream = File.Create(fileName))
      {
        VaultHeader.WriteSync(stream, keyInfo.KeyId, stamp);
        keyInfo.WriteBlock(stream);
      }
      return new VaultFile(fileName);
    }
  }

  /// <summary>
  /// The full path to the file
  /// </summary>
  public string FileName { get; init; }

  /// <summary>
  /// The list of blocks
  /// </summary>
  public BlockInfoList Blocks { get; init; }

  /// <summary>
  /// Contains the content of the vault's header
  /// </summary>
  public VaultHeader Header { get; init; }

  /// <summary>
  /// The key id for the encryption key used in this vault
  /// </summary>
  public Guid KeyId { get => Header.KeyId; }

  /// <summary>
  /// Retrieve the <see cref="PassphraseKeyInfoFile"/> from the
  /// PASS block if available, or null if not available. The returned
  /// value is cached during the first call.
  /// </summary>
  public PassphraseKeyInfoFile? GetPassphraseInfo()
  {
    return GetPassphraseInfo(null);
  }

  /// <summary>
  /// Append an unauthenticated comment block
  /// </summary>
  /// <param name="comment">
  /// The comment to add
  /// </param>
  public BlockInfo AppendComment(string comment)
  {
    var bytes = Encoding.UTF8.GetBytes(comment);
    var bi = new BlockInfo(BlockType.UnauthenticatedComment);
    // Writing the block will take care of setting the size and offset fields
    using(var stream = File.OpenWrite(FileName))
    {
      stream.Position = stream.Length;
      bi.WriteSync(stream, bytes);
    }
    Blocks.Add(bi);
    return bi;
  }

  /// <summary>
  /// Append a new file to the vault (encrypted). This overload takes the input
  /// as a stream instead of a file.
  /// </summary>
  /// <param name="cryptor">
  /// The encryption engine that wraps the key and the nonce generator.
  /// </param>
  /// <param name="content">
  /// The stream that provides the content to be encrypted. The stream must have
  /// a well defined Length.
  /// </param>
  /// <param name="fileStamp">
  /// The UTC last write timestamp of the input file
  /// </param>
  /// <param name="logicalName">
  /// The name of the file. This must be a realative path (and usually is just
  /// the file name). If there are path separators they must be '/', not '\'.
  /// None of the segments of the path can be '..' or '.'.
  /// </param>
  /// <returns>
  /// A BlockElement wrapping the file block and its content blocks.
  /// </returns>
  public BlockElement AppendFile(
    VaultCryptor cryptor,
    Stream content,
    DateTime fileStamp,
    string logicalName)
  {
    if(cryptor.KeyId != KeyId)
    {
      throw new ArgumentException("Incorrect key", nameof(cryptor));
    }
    if(fileStamp.Kind != DateTimeKind.Utc)
    {
      throw new ArgumentOutOfRangeException(
        nameof(fileStamp), "Expecting a time stamp in UTC");
    }
    var length = content.Length;
    var remaining = length;
    CheckFileNameValidity(logicalName);
    var encryptionStamp = DateTime.UtcNow;
    using(var stream = File.OpenWrite(FileName))
    {
      stream.Position = stream.Length;
      Span<byte> headerContent = stackalloc byte[24];
      var headerBlock = AppendFileHeaderBlock(
        stream, encryptionStamp, fileStamp, length, headerContent);
      var nameBlock = AppendFileNameBlock(
        stream, cryptor, logicalName, headerContent);
      headerBlock.AddChild(nameBlock);
      using(var chunkBuffer = new CryptoBuffer<byte>(VaultFormat2.VaultChunkSize))
      using(var cipherBuffer = new CryptoBuffer<byte>(VaultFormat2.VaultChunkSize))
      {
        bool first = true;
        Span<byte> tagOld = stackalloc byte[16];
        Span<byte> tagNew = stackalloc byte[16];
        Span<byte> aux = stackalloc byte[32];
        while(remaining > 0L)
        {
          var n = content.Read(chunkBuffer.Span());
          remaining -= n;
          if(n == 0)
          {
            throw new EndOfStreamException("Unexpected end of file");
          }
          var plaintext = chunkBuffer.Span(0, n);
          var cipher = cipherBuffer.Span(0, n);
          BlockElement be;
          if(first)
          {
            be = AppendFirstFileBlock(
              stream, cryptor, plaintext, cipher, headerContent, tagNew);
          }
          else
          {
            be = AppendNextFileBlock(
              stream, cryptor, plaintext, cipher, tagOld, tagNew);
          }
          tagNew.CopyTo(tagOld);
          headerBlock.AddChild(be);
          first = false;
        }
      }
      var terminatorBlock = AppendTerminator(stream);
      headerBlock.AddChild(terminatorBlock);
      return headerBlock;
    }
  }

  /// <summary>
  /// Append a new file to the vault (encrypted)
  /// </summary>
  /// <param name="cryptor">
  /// The encryption engine that wraps the key and the nonce generator.
  /// </param>
  /// <param name="fileName">
  /// The name of the existing file to add
  /// </param>
  /// <param name="logicalName">
  /// The name of the file. This must be a realative path (and usually is just
  /// the file name). If there are path separators they must be '/', not '\'.
  /// None of the segments of the path can be '..' or '.'.
  /// If null, the file name part of <paramref name="fileName"/> is used
  /// (without path)
  /// </param>
  /// <returns>
  /// A BlockElement wrapping the file block and its content blocks.
  /// </returns>
  public BlockElement AppendFile(
    VaultCryptor cryptor,
    string fileName,
    string? logicalName = null)
  {
    logicalName ??= Path.GetFileName(fileName);
    using(var stream = File.OpenRead(fileName))
    {
      return AppendFile(cryptor, stream, File.GetLastWriteTimeUtc(fileName), logicalName);
    }
  }

  /// <summary>
  /// If there is no passphrase key link element in this Vaultfile yet, append one.
  /// </summary>
  /// <param name="pkif">
  /// The passphrase key link information
  /// </param>
  public void AppendPassphraseLinkIfMissing(PassphraseKeyInfoFile pkif)
  {
    if(pkif.KeyId != KeyId)
    {
      throw new InvalidOperationException(
        "Expecting key ID of the password info to match this file's key ID");
    }
    var passBlock = Blocks.Blocks.FirstOrDefault(bi => bi.Kind == Zvlt2BlockType.PassphraseLink);
    if(passBlock == null)
    {
      using(var stream = File.OpenWrite(FileName))
      {
        var bi = pkif.WriteBlock(stream);
        Blocks.Add(bi);
      }
      _pkifSearched = true;
      _pkifCache = pkif;
    }
  }

  /// <summary>
  /// Check if the name is valid for use as the logical name
  /// of a file in a z-vault, throwing an exception if it isn't.
  /// </summary>
  /// <param name="logicalName">
  /// The name to check
  /// </param>
  public static void CheckFileNameValidity(string logicalName)
  {
    if(String.IsNullOrEmpty(logicalName))
    {
      throw new ArgumentException("The logical file name cannot be empty");
    }
    if(logicalName.IndexOfAny(new[] { ':', '\\'} ) >= 0)
    {
      throw new ArgumentException("The logical file name cannot contain the characters ':' or '\\'");
    }
    if(logicalName.StartsWith("/"))
    {
      throw new ArgumentException("The logical name must be relative");
    }
    var segments = logicalName.Split('/');
    if(segments.Any(s => s == "." || s == ".."))
    {
      throw new ArgumentException("The logical name path cannot contain any '.' or '..' segments");
    }
    if(segments.Any(s => s.EndsWith('.')))
    {
      throw new ArgumentException("The logical name path cannot contain any segments ending in '.'");
    }
  }

  private PassphraseKeyInfoFile? GetPassphraseInfo(Stream? stream)
  {
    if(!_pkifSearched)
    {
      _pkifSearched = true;
      var passBlock = Blocks.Blocks.FirstOrDefault(bi => bi.Kind == Zvlt2BlockType.PassphraseLink);
      PassphraseKeyInfoFile pkif;
      if(passBlock != null)
      {
        //_pkifCache = PassphraseKeyInfoFile.ReadFromBlock
        if(stream != null)
        {
          pkif = PassphraseKeyInfoFile.ReadFromBlock(stream, passBlock);
        }
        else
        {
          using(var s = File.OpenRead(FileName))
          {
            pkif = PassphraseKeyInfoFile.ReadFromBlock(s, passBlock);
          }
        }
        if(pkif.KeyId != KeyId)
        {
          throw new InvalidOperationException(
            "Expecting key ID of the password info to match this file's key ID");
        }
        _pkifCache = pkif;
      }
      else
      {
        _pkifCache = null;
      }
    }
    return _pkifCache;
  }

  private BlockElement AppendFileHeaderBlock(
    Stream destination,
    DateTime utcEncryptionStamp,
    DateTime utcFileStamp,
    long fileSize,
    Span<byte> content)
  {
    var sw = new SpanWriter();
    sw
      .WriteEpochTicks(content, utcEncryptionStamp)
      .WriteEpochTicks(content, utcFileStamp)
      .WriteI64(content, fileSize)
      .CheckFull(content);
    var bi = BlockInfo.WriteSync(destination, Zvlt2BlockType.FileHeader, content);
    Blocks.Add(bi);
    return new BlockElement(bi);
  }

  private BlockElement AppendFileNameBlock(
    Stream destination,
    VaultCryptor cryptor,
    string logicalName,
    ReadOnlySpan<byte> headerContent)
  {
    // at this point we assume the name has already been validated
    var plainText = Encoding.UTF8.GetBytes(logicalName);
    Span<byte> nonce = stackalloc byte[12];
    Span<byte> authtag = stackalloc byte[16];
    Span<byte> aux = stackalloc byte[32];
    var size = 36 + plainText.Length;
    destination.Position = destination.Length;
    var bi = new BlockInfo(Zvlt2BlockType.FileName, size, destination.Position);
    new SpanWriter()
      .WriteI32(aux, bi.Kind)
      .WriteI32(aux, bi.Size)
      .WriteSpan(aux, headerContent)
      .CheckFull(aux);
    var cipherText = new byte[plainText.Length];
    cryptor.Encrypt(aux, plainText, cipherText, nonce, authtag);
    Span<byte> header = aux.Slice(0, 8);
    destination.Write(header);
    destination.Write(nonce);
    destination.Write(authtag);
    destination.Write(cipherText);
    Blocks.Add(bi);
    return new BlockElement(bi);
  }

  private BlockElement AppendFirstFileBlock(
    Stream destination,
    VaultCryptor cryptor,
    ReadOnlySpan<byte> plainText,
    Span<byte> cipherText,
    ReadOnlySpan<byte> headerContent,
    Span<byte> tagOut)
  {
    Span<byte> nonce = stackalloc byte[12];
    Span<byte> aux = stackalloc byte[32];
    var size = 36 + plainText.Length;
    destination.Position = destination.Length;
    var bi = new BlockInfo(Zvlt2BlockType.FileContent1, size, destination.Position);
    new SpanWriter()
      .WriteI32(aux, bi.Kind)
      .WriteI32(aux, bi.Size)
      .WriteSpan(aux, headerContent)
      .CheckFull(aux);
    cryptor.Encrypt(aux, plainText, cipherText, nonce, tagOut);
    Span<byte> header = aux.Slice(0, 8);
    destination.Write(header);
    destination.Write(nonce);
    destination.Write(tagOut);
    destination.Write(cipherText);
    Blocks.Add(bi);
    return new BlockElement(bi);
  }

  private BlockElement AppendNextFileBlock(
    Stream destination,
    VaultCryptor cryptor,
    ReadOnlySpan<byte> plainText,
    Span<byte> cipherText,
    ReadOnlySpan<byte> tagIn,
    Span<byte> tagOut)
  {
    Span<byte> nonce = stackalloc byte[12];
    var size = 36 + plainText.Length;
    destination.Position = destination.Length;
    var bi = new BlockInfo(Zvlt2BlockType.FileContent1, size, destination.Position);
    cryptor.Encrypt(tagIn, plainText, cipherText, nonce, tagOut);
    Span<byte> header = stackalloc byte[8];
    bi.FormatBlockHeader(header);
    destination.Write(header);
    destination.Write(nonce);
    destination.Write(tagOut);
    destination.Write(cipherText);
    Blocks.Add(bi);
    return new BlockElement(bi);
  }

  private BlockElement AppendTerminator(
    Stream destination)
  {
    var bi = new BlockInfo(BlockType.GenericTerminator);
    bi.WriteSync(destination, Span<byte>.Empty);
    Blocks.Add(bi);
    return new BlockElement(bi);
  }

}
