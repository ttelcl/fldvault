/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Vaults;

/// <summary>
/// Constants used for vault serialization (and related files)
/// </summary>
public static class VaultFormat
{

  /// <summary>
  /// *.pass.key-inf file signature
  /// </summary>
  public const long PassphraseKeyInfoSignature = 0x00464E4953534150L; // "PASSINF\0"

  /// <summary>
  /// *.link.key-inf file signature
  /// </summary>
  public const long LinkKeyInfoSignature = 0x00464E494B4E494CL; // "LINKINF\0"

  /// <summary>
  /// *.unlock file signature
  /// </summary>
  public const long Unlock0Signature = 0x000059454B574152L; // "RAWKEY\0\0"

  /// <summary>
  /// The number of .NET ticks at 1970-01-01 00:00:00 UTC
  /// </summary>
  public const long EpochTicks = 0x089F7FF5F7B58000L; 

  /// <summary>
  /// Vault signature for vaults containing a file
  /// </summary>
  public const long VaultSignatureFile = 0x00454C46544C565AL; // "ZVLTFLE\0"

  /// <summary>
  /// Vault signature for vaults containing data for in-memory use only
  /// </summary>
  public const long VaultSignatureSecret = 0x00434553544C565AL; // "ZVLTSEC\0"

  /// <summary>
  /// Maximum chunk size (256 kb). Also maximum size for non-file segments, since
  /// they need to fit in one chunk.
  /// </summary>
  public const int MaxChunkSize = 0x040000;

  /// <summary>
  /// Test if a stream signature is known
  /// </summary>
  public static bool IsKnownSignature(long signature)
  {
    return signature switch {
      VaultSignatureFile => true,
      VaultSignatureSecret => true,
      _ => false,
    };
  }
}
