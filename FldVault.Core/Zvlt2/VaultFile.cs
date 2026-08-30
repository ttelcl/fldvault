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
  /// Create a new VaultFile object for an existing *.zvlt file (or compatible)
  /// </summary>
  private VaultFile(
    string fileName,
    bool anyPurpose,
    int purpose)
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
      Header = VaultHeader.ReadSync(stream, anyPurpose, purpose);
      if(!anyPurpose && purpose != Header.Purpose)
      {
        throw new InvalidOperationException(
          "This vault file is not of the requested kind");
      }
      stream.Position = 0;
      Blocks.Reload(stream);
      GetPassphraseInfo(stream); // caches it if found
    }
  }

  /// <summary>
  /// Create a new VaultFile object for an existing *.zvlt file known to have
  /// a specific purpose.
  /// </summary>
  /// <param name="fileName">
  /// The file name
  /// </param>
  /// <param name="purpose">
  /// The expected purpose of the vault. This must match the purpose in the file
  /// header. Normally this is <see cref="ZvltPurpose.Default"/>.
  /// </param>
  public VaultFile(string fileName, int purpose)
    : this(fileName, false, purpose)
  {
  }

  /// <summary>
  /// Create a new VaultFile object for an existing *.zvlt file known to have
  /// purpose <see cref="ZvltPurpose.Default"/>. This overload exists primarily
  /// for backward compatibility.
  /// </summary>
  /// <param name="fileName">
  /// The file name
  /// </param>
  public VaultFile(string fileName)
    : this(fileName, false, ZvltPurpose.Default)
  {
  }

  /// <summary>
  /// Create a new VaultFile object for an existing *.zvlt file without requiring
  /// knowledge of the file's purpose. This is intended for diagnostic tools only.
  /// </summary>
  /// <param name="fileName">
  /// The file name
  /// </param>
  /// <param name="anyPurpose">
  /// Set to true to allow loading files with any purpose. Passing false is equivalent
  /// to <see cref="VaultFile(string)"/>.
  /// </param>
  public VaultFile(string fileName, bool anyPurpose)
    : this(fileName, anyPurpose, ZvltPurpose.Default)
  {
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
  /// <param name="purpose">
  /// The purpose of the file (default <see cref="ZvltPurpose.Default"/>).
  /// For nonstandard cases consider using <see cref="PurposeForFileExtension(string)"/>.
  /// </param>
  /// <returns>
  /// The VaultFile instance
  /// </returns>
  public static VaultFile OpenOrCreate(
    string fileName, Guid keyId, DateTime? stamp = null, int purpose = ZvltPurpose.Default)
  {
    fileName = Path.GetFullPath(fileName);
    if(File.Exists(fileName))
    {
      var vf = new VaultFile(fileName, purpose);
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
        VaultHeader.WriteSync(stream, keyId, stamp, purpose: purpose);
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
  /// <param name="purpose">
  /// The purpose of the file (default <see cref="ZvltPurpose.Default"/>)
  /// For nonstandard cases consider using <see cref="PurposeForFileExtension(string)"/>.
  /// </param>
  /// <returns>
  /// The VaultFile instance
  /// </returns>
  public static VaultFile OpenOrCreate(
    string fileName, IKeySeed keyInfo, DateTime? stamp = null, int purpose = ZvltPurpose.Default)
  {
    fileName = Path.GetFullPath(fileName);
    if(File.Exists(fileName))
    {
      var vf = new VaultFile(fileName, purpose);
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
        VaultHeader.WriteSync(stream, keyInfo.KeyId, stamp, purpose: purpose);
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
      VaultHeader.WriteSync(stream, source.KeyId, source.Header.TimeStamp, purpose: source.Header.Purpose);
      var pkif = source.GetPassphraseInfo();
      pkif?.WriteBlock(stream);
    }
    return new VaultFile(fileName);
  }

  /// <summary>
  /// Create an empty passphrase based vault file.
  /// Optionally creates a file with a nonstandard purpose (e.g. a master key file)
  /// </summary>
  /// <param name="fileName">
  /// File name. By convention this should have the extension <c>*.zvlt</c>
  /// if <paramref name="purpose"/> is omitted or is <see cref="ZvltPurpose.Default"/>,
  /// or <c>*.mzvlt</c> if <paramref name="purpose"/> is
  /// <see cref="ZvltPurpose.Master"/>.
  /// </param>
  /// <param name="pkif">
  /// </param>
  /// <param name="purpose">
  /// The "purpose" of the file, by convention also affecting the file extension.
  /// Defaults to <see cref="ZvltPurpose.Default"/>.
  /// </param>
  /// <returns></returns>
  public static VaultFile CreateEmpty(
    string fileName,
    PassphraseKeyInfoFile pkif,
    int purpose = ZvltPurpose.Default)
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
      VaultHeader.WriteSync(stream, pkif.KeyId, purpose: ZvltPurpose.Default);
      pkif.WriteBlock(stream);
    }
    return new VaultFile(fileName);
  }

  /// <summary>
  /// Open an existing vault file. If not explicitly specified as <paramref name="purpose"/>,
  /// the file's purpose is derived from the file's extension.
  /// </summary>
  /// <param name="fileName">
  /// The file name
  /// </param>
  /// <param name="purpose">
  /// The explicitly requested purpose, or null to derive from the file extension
  /// </param>
  public static VaultFile Open(string fileName, int? purpose = null)
  {
    var purpose2 = purpose ?? PurposeForFileExtension(fileName) ?? ZvltPurpose.Default;
    return new VaultFile(fileName, false, purpose2);
  }

  /// <summary>
  /// Open an existing vault file without locking in its purpose. This overload
  /// allows reading the file's purpose from the vault header without prescribing it.
  /// </summary>
  /// <param name="fileName">
  /// The name of the file
  /// </param>
  /// <returns></returns>
  public static VaultFile OpenAnyVault(string fileName)
  {
    return new VaultFile(fileName, true);
  }

  /// <summary>
  /// Return the vault purpose as derived from the file extension (*.zvlt or *.mzvlt),
  /// or null if the extension is not recognized.
  /// </summary>
  /// <param name="fileName"></param>
  /// <returns></returns>
  public static int? PurposeForFileExtension(string fileName)
  {
    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    return extension switch {
      ".zvlt" => ZvltPurpose.Default,
      ".mzvlt" => ZvltPurpose.Master,
      _ => null,
    };
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
  /// Enumerate the top level <see cref="IBlockElement"/>s whose block has the
  /// given <paramref name="kind"/>.
  /// </summary>
  /// <param name="kind"></param>
  /// <returns></returns>
  public IEnumerable<IBlockElement> ElementsOfKind(int kind)
  {
    return Children.Where(ibe => ibe.Block.Kind == kind);
  }

  /// <summary>
  /// Enumerate the top level blocks that have the given <paramref name="kind"/>.
  /// </summary>
  /// <param name="kind"></param>
  /// <returns></returns>
  public IEnumerable<IBlockInfo> BlocksOfKind(int kind)
  {
    return Children.Where(ibe => ibe.Block.Kind == kind).Select(ibe => ibe.Block);
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

  /// <summary>
  /// Write a new master key file. If the file already exists, a backup file is created
  /// before creating the new file
  /// </summary>
  /// <param name="fileName">
  /// The master vault file name. Must end with ".mzvlt"
  /// </param>
  /// <param name="masterPkif">
  /// The <see cref="PassphraseKeyInfoFile"/> describing the master key
  /// </param>
  /// <param name="childKeyIds">
  /// The key ids to include in the file
  /// </param>
  /// <param name="childKeyChain">
  /// The key chain providing the keys specified by <paramref name="childKeyIds"/>.
  /// </param>
  /// <param name="masterKeyChain">
  /// If not null, the key chain providing the master key as identified by
  /// <paramref name="masterPkif"/>. If null, the master key must be in
  /// <paramref name="childKeyChain"/>.
  /// </param>
  /// <param name="passphraseLinks">
  /// If not null: passphrase info blocks to include in the master key file.
  /// In common scenarios it may be better to not include any, since they
  /// publicly declare the IDs of the keys that are present.
  /// </param>
  /// <exception cref="ArgumentException"></exception>
  /// <exception cref="InvalidOperationException"></exception>
  public static void WriteMasterKeyFile(
    string fileName,
    PassphraseKeyInfoFile masterPkif,
    IReadOnlyCollection<Guid> childKeyIds,
    KeyChain childKeyChain,
    KeyChain? masterKeyChain = null,
    IEnumerable<PassphraseKeyInfoFile>? passphraseLinks = null)
  {
    if(!fileName.EndsWith(".mzvlt", StringComparison.InvariantCultureIgnoreCase))
    {
      throw new ArgumentException(
        "Expecting file name to end with '.mzvlt'");
    }
    masterKeyChain ??= childKeyChain;
    if(!masterKeyChain.ContainsKey(masterPkif.KeyId))
    {
      throw new InvalidOperationException(
        $"Master key '{masterPkif.KeyId}' not found in the master key chain.");
    }
    var tmpName = fileName + ".tmp";
    var vault = VaultFile.CreateEmpty(tmpName, masterPkif, ZvltPurpose.Master);
    using(var cryptor = vault.CreateCryptor(masterKeyChain))
    using(var writer = new VaultFileWriter(vault, cryptor))
    {
      if(passphraseLinks != null)
      {
        foreach(var link in passphraseLinks)
        {
          writer.AppendExternalPassphraseLink(link);
        }
      }
      writer.AppendChildKeyList(childKeyIds, childKeyChain);
    }
    if(File.Exists(fileName))
    {
      var bakName = fileName + ".bak";
      if(File.Exists(bakName))
      {
        File.Delete(bakName);
      }
      File.Replace(tmpName, fileName, bakName);
    }
  }

  private PassphraseKeyInfoFile? GetPassphraseInfo(Stream? stream)
  {
    if(!_pkifSearched)
    {
      _pkifSearched = true;
      // Require the first PASS block to match the file key (checked later)
      // Silently accept but ignore other PASS blocks
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
