/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Vaults.Model;

/// <summary>
/// Models the header of a supported vault file.
/// Used in vault reading scenarios.
/// </summary>
public class VaultHeader
{
  private VaultHeader(
    long signature,
    int version,
    Guid keyId,
    long writeTimeCode,
    long sourceTimeCode)
  {
    Signature = signature;
    Version = version;
    KeyId = keyId;
    WriteTimeCode = writeTimeCode;
    SourceTimeCode = sourceTimeCode;
  }

  /// <summary>
  /// The signature of the file, which may affect the format.
  /// The LSB 4 bytes must be the same as <see cref="VaultFormat.VaultSignatureFile"/>
  /// and <see cref="VaultFormat.VaultSignatureSecret"/>; the MSB byte must be 0.
  /// </summary>
  public long Signature { get; init; }

  /// <summary>
  /// The format version of the file, expected to be 
  /// <see cref="VaultFormat.VaultFileVersion"/>
  /// </summary>
  public int Version { get; init; }

  /// <summary>
  /// The ID of the key used in this file
  /// </summary>
  public Guid KeyId { get; init; }

  /// <summary>
  /// Time code (epoch ticks) of the time the file was written
  /// </summary>
  public long WriteTimeCode { get; init; }

  /// <summary>
  /// Time code (epoch ticks) of the original file
  /// </summary>
  public long SourceTimeCode { get; init; }

}