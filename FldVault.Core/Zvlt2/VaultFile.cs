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
using FldVault.Core.KeyResolution;
using FldVault.Core.Utilities;
using FldVault.Core.Vaults;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Caches information about a ZVLT v2+ file. Does not itself wrap
/// an open file handle. Requires the file to exist and have at least
/// the file header block.
/// </summary>
public class VaultFile: IBlockElementContainer
{
  private PassphraseKeyInfoFile? _pkifCache;
  private bool _pkifSearched;
  private int _elementCheckCount = -1;
  private BlockElementContainer? _elementContainerCache;

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
  /// This overload also creates a PASS block if it creates a new vault file
  /// (or other similar block if the key is not passphrase based)
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
  public static VaultFile OpenOrCreate(string fileName, IKeySeed keyInfo, DateTime? stamp = null)
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
        keyInfo.WriteAsBlock(stream);
      }
      return new VaultFile(fileName);
    }
  }

  /// <summary>
  /// Create a new empty vault file, using the key ID and time stamp from the
  /// source vault. If the source file has a PASS block, it is copied to the new
  /// vault file.
  /// </summary>
  /// <param name="fileName">
  /// The name of the new file to create. If this file already exists, it is
  /// moved to a backup file with the same name plus the extension ".bak".
  /// </param>
  /// <param name="source">
  /// The source vault file to clone
  /// </param>
  /// <returns></returns>
  public static VaultFile CreateEmptyClone(
    string fileName,
    VaultFile source)
  {
    fileName = Path.GetFullPath(fileName);
    if(File.Exists(fileName))
    {
      var bak = fileName + ".bak";
      if(File.Exists(bak))
      {
        File.Delete(bak);
      }
      File.Move(fileName, bak);
    }
    using(var stream = File.Create(fileName))
    {
      VaultHeader.WriteSync(stream, source.KeyId, source.Header.TimeStamp);
      var pkif = source.GetPassphraseInfo();
      pkif?.WriteBlock(stream);
    }
    return new VaultFile(fileName);
  }

  /// <summary>
  /// Open an existing vault file. This method exists for symmetry with the 
  /// OpenOrCreate() factory methods but is just an alias for the constructor.
  /// </summary>
  public static VaultFile Open(string fileName)
  {
    return new VaultFile(fileName);
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
  /// Return true if there are any blocks other than overhead blocks in this file.
  /// Overhead blocks are <see cref="Zvlt2BlockType.ZvltFile"/> and
  /// <see cref="Zvlt2BlockType.PassphraseLink"/>.
  /// </summary>
  /// <returns></returns>
  public bool HasContent()
  {
    var overheadBlockKinds = new HashSet<int> {
      Zvlt2BlockType.ZvltFile,
      Zvlt2BlockType.PassphraseLink,
    };
    return Blocks.Blocks.Any(
      bi => !overheadBlockKinds.Contains(bi.Kind));
  }

  /// <summary>
  /// Create a matching <see cref="VaultCryptor"/>
  /// </summary>
  /// <param name="keyChain">
  /// The key chain that holds the key for this vault
  /// </param>
  /// <param name="nonceGenerator">
  /// The nonce generator to use for encryption, or null to create
  /// a new nonce generator instance.
  /// </param>
  /// <returns></returns>
  public VaultCryptor CreateCryptor(KeyChain keyChain, NonceGenerator? nonceGenerator = null)
  {
    return new VaultCryptor(keyChain, KeyId, Header.TimeStamp, nonceGenerator);
  }

  /// <summary>
  /// Implements <see cref="IBlockElementContainer"/>, returning a cached copy,
  /// or a newly generated instance if the block list has changed.
  /// </summary>
  public IReadOnlyList<IBlockElement> Children {
    get {
      if(_elementContainerCache == null || _elementCheckCount != Blocks.ChangeCounter)
      {
        _elementContainerCache = Blocks.BuildElementTree();
        _elementCheckCount = Blocks.ChangeCounter;
      }
      return _elementContainerCache.Children;
    }
  }

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
  /// Enumerate block elements in the vault that represent a contained file
  /// </summary>
  /// <param name="kind">
  /// The top level element type to look for (normally <see cref="Zvlt2BlockType.FileHeader"/>;
  /// that is also the default)
  /// </param>
  /// <returns></returns>
  public IEnumerable<IBlockElement> FileElements(int kind = Zvlt2BlockType.FileHeader)
  {
    return Children.Where(ibe => ibe.Block.Kind == kind);
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
    if(logicalName.IndexOfAny(new[] { ':', '\\' }) >= 0)
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

  /// <summary>
  /// Test if the source vault is compatible with this vault, enabling
  /// cloning blocks and elements directly, without reencoding and without
  /// access to the key.
  /// To enable this scenario, the source vault must have the same key and creation
  /// stamp as this vault.
  /// </summary>
  /// <param name="sourceVault">
  /// The source vault to test
  /// </param>
  /// <returns>
  /// True if the source vault is compatible with this vault
  /// </returns>
  public bool IsCompatibleSource(VaultFile sourceVault)
  {
    return sourceVault.KeyId == KeyId
      && sourceVault.Header.TimeStamp == Header.TimeStamp;
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
        passBlock.ExpectBlockLength(96);
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
