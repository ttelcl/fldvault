/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.BlockFiles;
using FldVault.Core.Crypto;
using FldVault.Core.Mvlt;
using FldVault.Core.Utilities;
using FldVault.Core.Zvlt2;

namespace FldVault.Core.Vaults;

/// <summary>
/// Content of *.pass.key-info files and PASS blocks in ZVLT files.
/// </summary>
public class PassphraseKeyInfoFile
{
  private readonly byte[] _salt;
  private string? _saltBase64Cache = null;

  /// <summary>
  /// Create a new PassphraseKeyInfoFile. Consider using one of the static
  /// ReadFrom(*) overloads or TryRead() instead. For initializing a new key
  /// use the other constructor instead.
  /// </summary>
  public PassphraseKeyInfoFile(
    Guid keyId,
    ReadOnlySpan<byte> salt,
    DateTime stamp)
  {
    KeyId = keyId;
    if(salt.Length != 64)
    {
      throw new ArgumentOutOfRangeException(nameof(salt), "Expecting salt to be 64 bytes in size");
    }
    _salt = salt.ToArray();
    if(stamp.Kind != DateTimeKind.Utc)
    {
      throw new ArgumentOutOfRangeException(nameof(stamp), "Expecting an UTC time stamp");
    }
    UtcKeyStamp = stamp;
  }

  /// <summary>
  /// Create a new PassphraseKeyInfoFile from an existing key (to initialize a new file)
  /// </summary>
  /// <param name="key">
  /// The key to represent
  /// </param>
  /// <param name="stamp">
  /// The UTC timestamp to associate with the key, or null (default) to use the current time.
  /// </param>
  public PassphraseKeyInfoFile(PassphraseKey key, DateTime? stamp = null)
    : this(key.GetId(), key.Salt, stamp ?? DateTime.UtcNow)
  {
  }

  /// <summary>
  /// Convert a Zkey to a PassphraseKeyInfoFile. These classes contain the
  /// same information; <see cref="Zkey"/> is JSON serializable and
  /// <see cref="PassphraseKeyInfoFile"/> serializes to a custom binary format.
  /// </summary>
  public static PassphraseKeyInfoFile FromZkey(Zkey zkey)
  {
    var keyId = Guid.Parse(zkey.KeyId);
    var salt = Base64Url.DecodeFromChars(zkey.Salt);
    return new PassphraseKeyInfoFile(keyId, salt, zkey.Created);
  }

  /// <summary>
  /// Convert this PassphraseKeyInfoFile to Zkey format.
  /// </summary>
  public Zkey ToZkey()
  {
    return Zkey.FromPassphraseKeyInfoFile(this);
  }

  /// <summary>
  /// Read a PassphraseKeyInfoFile from its 96 byte serialized format
  /// </summary>
  public static PassphraseKeyInfoFile ReadFrom(ReadOnlySpan<byte> blob96)
  {
    if(blob96.Length != 96)
    {
      throw new InvalidOperationException(
        "Incorrect *.pass.key-info content (expecting 96 bytes)");
    }
    var signature = BinaryPrimitives.ReadInt64LittleEndian(blob96.Slice(0, 8));
    if(signature != PassphraseKeyInfoSignature)
    {
      throw new InvalidOperationException(
        "Unrecognized file format for *.pass.key-info file");
    }
    var stamp = EpochTicks.ToUtc(BinaryPrimitives.ReadInt64LittleEndian(blob96.Slice(8, 8)));
    var guid = new Guid(blob96.Slice(16, 16));
    return new PassphraseKeyInfoFile(guid, blob96.Slice(32, 64), stamp);
  }

  /// <summary>
  /// Read a PassphraseKeyInfoFile from a *.pass.key-info stream
  /// </summary>
  public static PassphraseKeyInfoFile ReadFrom(Stream stream)
  {
    Span<byte> blob = stackalloc byte[96];
    var n = stream.Read(blob);
    if(n != 96)
    {
      throw new EndOfStreamException(
        "Unexpected EOF while loading *.pass.key-info stream");
    }
    return ReadFrom(blob);
  }

  /// <summary>
  /// Read a PassphraseKeyInfoFile from a *.pass.key-info file
  /// </summary>
  public static PassphraseKeyInfoFile ReadFrom(string filename)
  {
    var bytes = File.ReadAllBytes(filename);
    if(bytes.Length != 96)
    {
      throw new InvalidOperationException(
        $"Unexpected file size for {filename} (expecting 96 bytes)");
    }
    return ReadFrom(bytes);
  }

  /// <summary>
  /// Read a new instance from a "PASS" (<see cref="Zvlt2BlockType.PassphraseLink"/>)
  /// block embedded in a block file
  /// </summary>
  /// <param name="stream">
  /// The block file stream (usually *.zvlt)
  /// </param>
  /// <param name="blockInfo">
  /// The descriptor of the existing PASS block in the stream
  /// </param>
  /// <returns>
  /// A new <see cref="PassphraseKeyInfoFile"/> instance
  /// </returns>
  public static PassphraseKeyInfoFile ReadFromBlock(Stream stream, BlockInfo blockInfo)
  {
    if(blockInfo.Kind != Zvlt2BlockType.PassphraseLink)
    {
      throw new InvalidOperationException("Incorrect block kind");
    }
    if(blockInfo.Size != 96)
    {
      throw new InvalidOperationException("Unexpected block size");
    }
    Span<byte> content = stackalloc byte[blockInfo.ContentSize];
    blockInfo.ReadSync(stream, content);
    var stamp = EpochTicks.ToUtc(BinaryPrimitives.ReadInt64LittleEndian(content.Slice(0, 8)));
    var guid = new Guid(content.Slice(8, 16));
    return new PassphraseKeyInfoFile(guid, content.Slice(24, 64), stamp);
  }

  /// <summary>
  /// Try to read the *.pass.key-info file for the given key in the given folder.
  /// Returns null if not found. This overload assumes the key-info file has no tag part.
  /// DEPRECATED.
  /// </summary>
  /// <param name="keyId">
  /// The key ID to look for (derive the file name from)
  /// </param>
  /// <param name="folderName">
  /// The folder where to look for the file
  /// </param>
  /// <returns>
  /// Null if the file was not found, the key file content if found
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the Key ID in the file did not match the name
  /// </exception>
  public static PassphraseKeyInfoFile? TryRead(Guid keyId, string folderName)
  {
    if(!Directory.Exists(folderName))
    {
      return null;
    }
    var fileName = Path.Combine(folderName, $"{keyId}.pass.key-info");
    if(!File.Exists(fileName))
    {
      return null;
    }
    var pkif = ReadFrom(fileName);
    if(pkif.KeyId != keyId)
    {
      throw new InvalidOperationException(
        $"The content of ${fileName} does not match its name (key ID is ${pkif.KeyId})");
    }
    return pkif;
  }

  /// <summary>
  /// Try to read the *.pass.key-info file for the given key in the given folder.
  /// Returns null if not found.
  /// </summary>
  /// <param name="kin">
  /// The key info file name descriptor
  /// </param>
  /// <param name="folderName">
  /// The folder where to look for the file
  /// </param>
  /// <returns>
  /// Null if the file was not found, the key file content if found
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the Key ID in the file did not match the name, or if the
  /// key-info was not a passphrase based key-info.
  /// </exception>
  public static PassphraseKeyInfoFile? TryRead(KeyInfoName kin, string folderName)
  {
    if(!Directory.Exists(folderName))
    {
      return null;
    }
    if(!File.Exists(kin.FileName))
    {
      return null;
    }
    if(kin.Kind != KeyKind.Passphrase)
    {
      throw new ArgumentException(
        "Expecting a key-info descriptor for a passphrase based key.");
    }
    var pkif = ReadFrom(kin.FileName);
    if(pkif.KeyId != kin.KeyId)
    {
      throw new InvalidOperationException(
        $"The content of ${kin.FileName} does not match its name (key ID is ${pkif.KeyId})");
    }
    return pkif;
  }

  /// <summary>
  /// Try to read a PassphraseKeyInfoFile from a file in a supported format.
  /// This implementation supports both *.pass.key-info files as well as *.zvlt
  /// files.
  /// </summary>
  /// <param name="fileName">
  /// The name of the file, which should have a recognized file extension
  /// </param>
  /// <returns>
  /// The key info that was loaded from the file, if any was found
  /// </returns>
  public static PassphraseKeyInfoFile? TryFromFile(string fileName)
  {
    if(fileName.EndsWith(".zkey"))
    {
      var zkey = Zkey.FromJson(File.ReadAllText(fileName));
      return FromZkey(zkey);
    }
    if(fileName.EndsWith(".pass.key-info"))
    {
      return ReadFrom(fileName);
    }
    if(fileName.EndsWith(".zvlt"))
    {
      var vaultFile = new VaultFile(fileName);
      return vaultFile.GetPassphraseInfo();
    }
    if(fileName.EndsWith(".mvlt"))
    {
      return MvltFormat.ReadKeyInfo(fileName);
    }
    return null;
  }

  /// <summary>
  /// Write the content of this object to a 96 byte blob
  /// </summary>
  /// <param name="span">
  /// A 96 byte buffer
  /// </param>
  public void SerializeToSpan(Span<byte> span)
  {
    if(span.Length != 96)
    {
      throw new ArgumentOutOfRangeException(nameof(span), "Expecting a 96 byte span");
    }
    BinaryPrimitives.WriteInt64LittleEndian(span.Slice(0, 8), PassphraseKeyInfoSignature);
    BinaryPrimitives.WriteInt64LittleEndian(span.Slice(8, 8), EpochTicks.FromUtc(UtcKeyStamp));
    KeyId.TryWriteBytes(span.Slice(16, 16));
    Salt.CopyTo(span.Slice(32, 64));
  }

  /// <summary>
  /// Write the content of this info object to a *.pass.key-info stream
  /// </summary>
  public void Write(Stream stream)
  {
    Span<byte> buffer = stackalloc byte[96];
    SerializeToSpan(buffer);
    stream.Write(buffer);
  }

  /// <summary>
  /// Write the content to a file in the specified raw folder.
  /// The file name is created based on the Key ID.
  /// If necessary, this method creates the folder.
  /// </summary>
  /// <param name="folder">
  /// The folder to write this key-info to
  /// </param>
  /// <returns>
  /// The full name of the written file
  /// </returns>
  public string WriteToFolder(string folder)
  {
    if(!Directory.Exists(folder))
    {
      Directory.CreateDirectory(folder);
    }
    var fileName = Path.Combine(folder, DefaultFileName);
    WriteToFile(fileName);
    return fileName;
  }

  /// <summary>
  /// Append the content as a new ZVLT v2 block to the end of the ZVLT stream
  /// </summary>
  /// <param name="blockStream">
  /// The open ZVLT v2 block stream.
  /// </param>
  public BlockInfo WriteBlock(Stream blockStream)
  {
    blockStream.Position = blockStream.Length;
    Span<byte> block = stackalloc byte[96-8];
    BinaryPrimitives.WriteInt64LittleEndian(block.Slice(0, 8), EpochTicks.FromUtc(UtcKeyStamp));
    KeyId.TryWriteBytes(block.Slice(8, 16));
    Salt.CopyTo(block.Slice(24, 64));
    var bi = BlockInfo.WriteSync(blockStream, Zvlt2BlockType.PassphraseLink, block);
    return bi;
  }

  /// <summary>
  /// The default file name for this key-info
  /// </summary>
  public string DefaultFileName { get => $"{KeyId}.pass.key-info"; }

  /// <summary>
  /// Write the content to a file. Consider using <see cref="WriteToFolder(string)"/>
  /// instead to ensure consistent file naming
  /// </summary>
  /// <param name="fileName">
  /// The name of the file. The folder is assumed to exist.
  /// </param>
  public void WriteToFile(string fileName)
  {
    var buffer = new byte[96];
    SerializeToSpan(buffer);
    File.WriteAllBytes(fileName, buffer);
  }

  /// <summary>
  /// The key id (derived from the raw key, suitable for validating
  /// the raw key)
  /// </summary>
  public Guid KeyId { get; init; }

  /// <summary>
  /// The PBKDF2 salt
  /// </summary>
  public ReadOnlySpan<byte> Salt { get => _salt; }

  /// <summary>
  /// The UTC time the key was created
  /// </summary>
  public DateTime UtcKeyStamp { get; init; }

  /// <summary>
  /// Get the salt encoded as standard base64url string
  /// </summary>
  public string SaltBase64 {
    get {
      if(_saltBase64Cache == null)
      {
        _saltBase64Cache = Base64Url.EncodeToString(_salt);
      }
      return _saltBase64Cache;
    }
  }

  /// <summary>
  /// *.pass.key-inf file signature
  /// </summary>
  public const long PassphraseKeyInfoSignature = 0x00464E4953534150L; // "PASSINF\0"

}