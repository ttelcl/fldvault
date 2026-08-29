using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Vaults;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Combines the two parts of information for a child key described in
/// a master key file. Either half may be missing.
/// </summary>
public class ChildKeyInfo
{
  /// <summary>
  /// Create a new empty <see cref="ChildKeyInfo"/> object for the key with
  /// identifier <paramref name="keyId"/>.
  /// </summary>
  /// <param name="keyId"></param>
  public ChildKeyInfo(Guid keyId)
  {
    KeyId = keyId;
  }

  /// <summary>
  /// The key ID for the key described in this record
  /// </summary>
  public Guid KeyId { get; }

  /// <summary>
  /// Get or set the data from the <see cref="Zvlt2BlockType.KeyTransform"/> block,
  /// or null if not (yet) known. 
  /// </summary>
  public KeyTransformEntry? Transformation { get; internal set; }

  /// <summary>
  /// Get or set the passphrase information that can recreate the key given a
  /// passphrase, as read from a <see cref="Zvlt2BlockType.ExternalPassphraseLink"/> block.
  /// </summary>
  public PassphraseKeyInfoFile? PassphraseLink { get; internal set; }

}
