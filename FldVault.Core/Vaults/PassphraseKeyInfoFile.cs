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

namespace FldVault.Core.Vaults;

/// <summary>
/// Content of *.pass.key-info files
/// </summary>
public class PassphraseKeyInfoFile
{
  private readonly byte[] _salt;

  /// <summary>
  /// Create a new PassphraseKeyInfoFile
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
    if(signature != VaultFormat.PassphraseKeyInfoSignature)
    {
      throw new InvalidOperationException(
        "Unrecognized file format for *.pass.key-info file");
    }
    var ticks = BinaryPrimitives.ReadInt64LittleEndian(blob96.Slice(8, 8));
    var stamp = new DateTime(ticks, DateTimeKind.Utc);
    var guid = new Guid(blob96.Slice(16, 16));
    return new PassphraseKeyInfoFile(guid, blob96.Slice(32, 64), stamp);
  }

  /// <summary>
  /// Read a PassphraseKeyInfoFile from a stream
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
  /// Read a PassphraseKeyInfoFile from a stream
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
  /// Try to read the *.pass.key-info file for the given key in the given folder.
  /// Returns null if not found.
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
}