using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.BlockFiles;
using FldVault.Core.Vaults;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Maps child key ids to <see cref="ChildKeyInfo"/> instances and provides
/// methods to load this map.
/// </summary>
public class ChildKeyMap
{
  private readonly Dictionary<Guid, ChildKeyInfo> _keymap;

  /// <summary>
  /// Create a new empty <see cref="ChildKeyMap"/>
  /// </summary>
  public ChildKeyMap()
  {
    _keymap = new Dictionary<Guid, ChildKeyInfo>();
  }

  /// <summary>
  /// The mapping of keys to <see cref="ChildKeyInfo"/>.
  /// </summary>
  public IReadOnlyDictionary<Guid, ChildKeyInfo> KeyMap => _keymap;

  /// <summary>
  /// Read the information for the given <paramref name="block"/> from
  /// the <paramref name="reader"/>.
  /// </summary>
  /// <param name="reader"></param>
  /// <param name="block"></param>
  /// <returns>
  /// True if information was read, false if the block is not of a relevant
  /// block type.
  /// </returns>
  public bool AddBlock(VaultFileReader reader, IBlockInfo block)
  {
    switch (block.Kind) {
      case Zvlt2BlockType.ExternalPassphraseLink:
        var pkif = PassphraseKeyInfoFile.ReadFromBlock(reader, block);
        GetKey(pkif.KeyId).PassphraseLink = pkif;
        return true;
      case Zvlt2BlockType.KeyTransform:
        var trx = KeyTransformEntry.ReadFrom(reader, block);
        GetKey(trx.TargetKey).Transformation = trx;
        return true;
      default:
        return false;
    }
  }

  /// <summary>
  /// Load all relevant blocks from the vault file associated with the
  /// <paramref name="reader"/>
  /// </summary>
  /// <param name="reader"></param>
  public void Load(VaultFileReader reader)
  {
    foreach(var block in reader.Vault.Blocks.Blocks)
    {
      AddBlock(reader, block); // addblock handles picking only relevant blocks
    }
  }

  /// <summary>
  /// Load all relevant blocks from the vault
  /// </summary>
  /// <param name="vf"></param>
  public void Load(VaultFile vf)
  {
    using(var reader = new VaultFileReader(vf, null))
    {
      Load(reader);
    }
  }

  /// <summary>
  /// Get an existing <see cref="ChildKeyInfo"/> or create a new one if missing
  /// </summary>
  /// <param name="keyId"></param>
  /// <returns></returns>
  private ChildKeyInfo GetKey(Guid keyId)
  {
    if(!_keymap.TryGetValue(keyId, out var keyInfo))
    {
      keyInfo = new ChildKeyInfo(keyId);
      _keymap[keyId] = keyInfo;
    }
    return keyInfo;
  }
}
