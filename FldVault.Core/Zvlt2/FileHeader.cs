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

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Contains the content fields found in an encrypted file header
/// </summary>
public class FileHeader
{
  /// <summary>
  /// Create a new FileHeader
  /// </summary>
  public FileHeader(
    long encryptionStamp,
    Guid fileId)
  {
    EncryptionStamp = encryptionStamp;
    FileId = fileId;
  }

  /// <summary>
  /// The time stamp that the file was added to the vault
  /// </summary>
  public long EncryptionStamp { get; init; }

  /// <summary>
  /// The file identifier. This is assigned a random GUID
  /// at encryption time. This field provides a way to identify
  /// a file element even if it is anonymous or has a non-unique name.
  /// </summary>
  public Guid FileId { get; init; }
}
