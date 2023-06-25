﻿/*
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
    if(fileStamp.Kind != DateTimeKind.Utc)
    {
      throw new ArgumentOutOfRangeException(
        nameof(fileStamp), "Expecting a time stamp in UTC");
    }
    var length = content.Length;
    if(length > Int32.MaxValue)
    {
      throw new ArgumentOutOfRangeException(
        nameof(content), "This file is too large for storing in a *.zvlt file");
    }
    var remaining = (int)length;
    CheckFileNameValidity(logicalName);
    var encryptionStamp = DateTime.UtcNow;
    using(var stream = File.OpenWrite(FileName))
    {
      throw new NotImplementedException();
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
    throw new NotImplementedException();
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

}