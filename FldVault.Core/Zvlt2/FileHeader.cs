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
    long encryptionStamp)
  {
    EncryptionStamp = encryptionStamp;
  }

  /// <summary>
  /// The time stamp that the file was added to the vault
  /// </summary>
  public long EncryptionStamp { get; init; }
}
