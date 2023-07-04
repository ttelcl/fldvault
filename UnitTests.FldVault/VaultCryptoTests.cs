using System;
using System.Text;
using System.Security;
using System.Linq;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;
using Xunit.Abstractions;

using FldVault.Core;
using FldVault.Core.Crypto;
using FldVault.Core.Vaults;
using FldVault.Core.Zvlt2;
using FldVault.Core.BlockFiles;
using FldVault.Core.Utilities;

namespace UnitTests.FldVault;

public class VaultCryptoTests
{
  private readonly ITestOutputHelper _outputHelper;

  public VaultCryptoTests(
    ITestOutputHelper testOutputHelper)
  {
    _outputHelper = testOutputHelper;
  }

  [Fact]
  public void DisposingCryptoBufferClearsBuffer()
  {
    Span<byte> bytes;
    using(var buffer = new CryptoBuffer<byte>(8))
    {
      bytes = buffer.Span();
      for(byte i = 0; i < bytes.Length; i++)
      {
        bytes[i] = i;
      }
      for(var i = 1; i < bytes.Length; i++)
      {
        Assert.NotEqual(0, bytes[i]);
      }
    }
    for(var i = 0; i < bytes.Length; i++)
    {
      Assert.Equal(0, bytes[i]);
    }
  }

  [Fact]
  public void KeyDerivationMethodsAreEquivalent()
  {
    // Converts a passphrase to a key for three different representations
    // of that passphrase and test that the resulting key is the same
    // - an array of characters
    // - a SecureString
    // - a byte array containing the UTF-8 encoded string

    ReadOnlySpan<byte> salt = CreateFixedBadSalt();

    _outputHelper.WriteLine($"Salt = {DumpHex(salt.Slice(0, 20))} ...");

    const string passphrase = "Hellö, wörld!!";
    const int keyLength = PassphraseKey.DefaultKeyLength;

    byte[] key1;
    Guid id1;
    using(var characters = CryptoBuffer<char>.FromSpanClear(passphrase.ToCharArray()))
    {
      using(var pk = PassphraseKey.FromCharacters(characters, salt, keyLength))
      {
        key1 = pk.Bytes.ToArray();
        id1 = pk.GetId();
      }
    }
    _outputHelper.WriteLine($"Key via character array = {DumpHex(key1)} {id1}");

    byte[] key2;
    Guid id2;
    using(var securepw = new SecureString())
    {
      foreach(var ch in passphrase)
      {
        securepw.AppendChar(ch);
      }
      using(var pk = PassphraseKey.FromSecureString(securepw, salt, keyLength))
      {
        key2 = pk.Bytes.ToArray();
        id2 = pk.GetId();
      }
    }
    _outputHelper.WriteLine($"Key via secure string   = {DumpHex(key2)} {id2}");

    byte[] key3;
    Guid id3;
    var byteCount = Encoding.UTF8.GetByteCount(passphrase);
    using(var bytes = new CryptoBuffer<byte>(byteCount))
    {
      Encoding.UTF8.GetBytes(passphrase.ToCharArray(), bytes.Span());
      using(var pk = PassphraseKey.FromBytes(bytes, salt, keyLength))
      {
        key3 = pk.Bytes.ToArray();
        id3 = pk.GetId();
      }
    }
    _outputHelper.WriteLine($"Key via byte array      = {DumpHex(key3)} {id3}");

    _outputHelper.WriteLine($"Key ID {id3}");
    _outputHelper.WriteLine($"     = {DumpHex(id3.ToByteArray())}");

    Assert.Equal(key1, key2);
    Assert.Equal(key1, key3);
  }

  [Fact]
  public void CanCreateNonces()
  {
    const int repetitions = 15;
    var buffer = new byte[repetitions*12];
    var span = new Span<byte>(buffer);

    var nonceGenerator = new NonceGenerator();
    for(var i = 0; i < repetitions; i++)
    {
      // keep this loop tight
      nonceGenerator.Next(span.Slice(i*12, 12));
      // var s = DumpHex(span.Slice(i*12, 12));
    }

    var hexes =
      Enumerable.Range(0, repetitions)
      .Select(i => DumpHex(buffer.AsSpan(i*12, 12)))
      .ToList();

    foreach(var hex in hexes)
    {
      _outputHelper.WriteLine($"- {hex}");
    }

    var hexset = new HashSet<string>(hexes);
    Assert.Equal(hexes.Count, hexset.Count); // in other words: they are all unique
  }

  [Fact]
  public void CanCreatePasphraseBasedKey()
  {
    ReadOnlySpan<byte> salt = CreateFixedBadSalt(); // "fixed" == "don't do this in real applications"
    const string passphraseText = "Hellö, wörld!!";
    const string passphraseTextWrong = "Hello, world!!";
    var stamp = new DateTime(2023, 5, 19, 0, 0, 0, DateTimeKind.Utc);
    byte[] key1;
    Guid id1;
    PassphraseKeyInfoFile pkif1;
    using(var passphraseBuffer = new CryptoBuffer<char>(passphraseText.ToCharArray()))
    {
      using(var pk = PassphraseKey.FromCharacters(passphraseBuffer, salt))
      {
        id1 = pk.GetId();
        key1 = pk.Bytes.ToArray();
        pkif1 = new PassphraseKeyInfoFile(pk, stamp);
      }
    }

    var pkifBytes = new byte[96];
    pkif1.SerializeToSpan(pkifBytes);
    var hexDump = DumpHex(pkifBytes);
    _outputHelper.WriteLine("PKIF bytes:");
    _outputHelper.WriteLine(hexDump);

    const string expectedByteDump = @"50415353494E4600 0000860506D83B00 42367E930A111F4C AAED87031D048A1E
0102030405060708 090A0B0C0D0E0F10 1112131415161718 191A1B1C1D1E1F20
2122232425262728 292A2B2C2D2E2F30 3132333435363738 393A3B3C3D3E3F40";
    Assert.Equal(expectedByteDump, hexDump);

    var pkif2 = PassphraseKeyInfoFile.ReadFrom(pkifBytes);
    Assert.Equal(pkif1.UtcKeyStamp, pkif2.UtcKeyStamp);
    Assert.Equal(pkif1.KeyId, pkif2.KeyId);
    Assert.Equal(pkif1.Salt.ToArray(), pkif2.Salt.ToArray());

    using(var passphraseBuffer = new CryptoBuffer<char>(passphraseText.ToCharArray()))
    {
      using(var pk = PassphraseKey.TryPassphrase(passphraseBuffer, pkif2))
      {
        Assert.NotNull(pk);
        Assert.Equal(pkif2.KeyId, pk!.GetId());
        byte[] key3 = pk.Bytes.ToArray();
        Assert.Equal(key1, key3);
      }
    }

    using(var passphraseBuffer = new CryptoBuffer<char>(passphraseTextWrong.ToCharArray()))
    {
      using(var pk = PassphraseKey.TryPassphrase(passphraseBuffer, pkif2))
      {
        Assert.Null(pk);
      }
    }

    // Verify we can get the key out despite clearing the passphrase buffer
    using(var keychain = new KeyChain())
    {
      Assert.Null(keychain.FindDirect(id1));
      using(var passphraseBuffer = new CryptoBuffer<char>(passphraseText.ToCharArray()))
      {
        using(var pk = PassphraseKey.TryPassphrase(passphraseBuffer, pkif2))
        {
          Assert.NotNull(pk);
          Assert.Equal(pkif2.KeyId, pk!.GetId());
          byte[] key3 = pk.Bytes.ToArray();
          Assert.Equal(key1, key3);
          keychain.PutCopy(pk);
        }
      }
      Assert.NotNull(keychain.FindDirect(id1));
      byte[] key4 = keychain.FindDirect(id1)!.Bytes.ToArray();
      Assert.Equal(key1, key4);
    }
  }

  [Fact]
  public void CanWriteAndReadKeyInfo()
  {
    ReadOnlySpan<byte> salt = CreateFixedBadSalt(); // "fixed" == "don't do this in real applications"
    const string passphraseText = "Hellö, wörld!!";
    var stamp = new DateTime(2023, 5, 19, 0, 0, 0, DateTimeKind.Utc);
    PassphraseKeyInfoFile pkif;
    using(var passphraseBuffer = new CryptoBuffer<char>(passphraseText.ToCharArray()))
    {
      using(var pk = PassphraseKey.FromCharacters(passphraseBuffer, salt))
      {
        pkif = new PassphraseKeyInfoFile(pk, stamp);
      }
    }

    var filename = Path.GetFullPath(pkif.DefaultFileName);

    if(File.Exists(filename))
    {
      _outputHelper.WriteLine($"Deleting existing {filename}");
      File.Delete(filename);
    }
    Assert.False(File.Exists(filename));

    _outputHelper.WriteLine($"Writing {filename}");
    pkif.WriteToFolder(".");
    Assert.True(File.Exists(filename));
    var fi = new FileInfo(filename);
    Assert.Equal(96, fi.Length);

    _outputHelper.WriteLine($"Trying to read key-info for key {pkif.KeyId}");
    var pkif2 = PassphraseKeyInfoFile.TryRead(pkif.KeyId, ".");
    Assert.NotNull(pkif2);
    Assert.Equal(pkif.KeyId, pkif2!.KeyId);
    _outputHelper.WriteLine("Validating passphrase correctness");
    using(var passphraseBuffer = new CryptoBuffer<char>(passphraseText.ToCharArray()))
    {
      using(var pk = PassphraseKey.TryPassphrase(passphraseBuffer, pkif2))
      {
        Assert.NotNull(pk);
      }
    }

  }

  [Fact]
  public void CanSplitKeyInfoName()
  {
    const string name1 = "937e3642-110a-4c1f-aaed-87031d048a1e.pass.key-info";
    const string name2 = "937e3642-110a-4c1f-aaed-87031d048a1e.hello-world.pass.key-info";
    var guid = Guid.Parse("937e3642-110a-4c1f-aaed-87031d048a1e");

    var kin1 = KeyInfoName.FromFile(name1);
    Assert.Equal(guid, kin1.KeyId);
    Assert.Equal(KeyKind.Passphrase, kin1.Kind);
    Assert.Null(kin1.Tag);
    Assert.Equal(name1, kin1.FileName);

    var kin2 = KeyInfoName.FromFile(name2);
    Assert.Equal(guid, kin2.KeyId);
    Assert.Equal(KeyKind.Passphrase, kin2.Kind);
    Assert.Equal("hello-world", kin2.Tag);
    Assert.Equal(name2, kin2.FileName);

    var kin3 = KeyInfoName.TryFromFile("937e3642-110a-4c1f-aaed-87031d048a1e.75BC30DE-DA48-4800-889D-BA86570EF97C.link.key-info");
    Assert.NotNull(kin3);
    Assert.Equal(guid, kin3!.KeyId);
    Assert.Equal(KeyKind.Link, kin3!.Kind);
    Assert.Equal("75BC30DE-DA48-4800-889D-BA86570EF97C", kin3!.Tag);

    kin3 = KeyInfoName.TryFromFile("937e3642-110a-4c1f-aaed-87031d048a1e..pass.key-info");
    Assert.Null(kin3);
    kin3 = KeyInfoName.TryFromFile("937e3642-110a-4c1f-aaed-87031d048a1e.hellö.pass.key-info");
    Assert.Null(kin3);
    kin3 = KeyInfoName.TryFromFile("937e3642-110a-4c1f-aaed-87031d048a1e.hello.world.pass.key-info");
    Assert.Null(kin3);
    kin3 = KeyInfoName.TryFromFile("937e3642-110a-4c1f-aaed-87031d048a1e.hello world.pass.key-info");
    Assert.Null(kin3);
    kin3 = KeyInfoName.TryFromFile("937e3642-110a-4c1f-aaed-87031d048a1e.pass!.key-info");
    Assert.Null(kin3);
    kin3 = KeyInfoName.TryFromFile("937e3642-110a-4c1f-aaed-87031d048a1e.pass.keyinfo");
    Assert.Null(kin3);
    kin3 = KeyInfoName.TryFromFile("937e3642-110a-4c1f-aaed-87031d048a1e.pass.key-info");
    Assert.NotNull(kin3);
    kin3 = KeyInfoName.TryFromFile("937e3642110a4c1faaed87031d048a1e.pass.key-info");
    Assert.Null(kin3);
    kin3 = KeyInfoName.TryFromFile("fluffy-bunny.pass.key-info");
    Assert.Null(kin3);
  }

  [Fact]
  public void CanCreateNewZvlt2File()
  {
    var stamp = new DateTime(2023, 5, 19, 1, 2, 3, 4, DateTimeKind.Utc);
    const string passphraseText = "HelloWorld";
    var pkif = CreateTestKeyInfo(passphraseText, stamp, null);

    const string fileName = "HelloWorld.zvlt";
    if(File.Exists(fileName))
    {
      _outputHelper.WriteLine($"Deleting existing {fileName}");
      File.Delete(fileName);
    }

    Assert.False(File.Exists(fileName));

    var vaultFile = VaultFile.OpenOrCreate(fileName, pkif, stamp);
    Assert.NotNull(vaultFile);
    Assert.True(File.Exists(fileName));
    var length1 = new FileInfo(fileName).Length;

    vaultFile.AppendComment("This is a U comment");
    var length2 = new FileInfo(fileName).Length;
    Assert.True(length2 > length1);

    vaultFile.AppendComment("This is another U comment");

    var vf2 = new VaultFile(fileName);
    Assert.NotNull(vf2);
    Assert.Equal(4, vf2.Blocks.Blocks.Count);

    foreach(var block in vf2.Blocks.Blocks)
    {
      _outputHelper.WriteLine($"'{BlockType.ToText(block.Kind)}' @{block.Offset:X6} ({block.Size,6} bytes)");
    }
  }

  [Fact]
  public void CanEncryptFileSmall()
  {
    var stamp = new DateTime(2023, 5, 19, 1, 2, 3, 4, DateTimeKind.Utc);
    const string passphraseText = "HelloWorld";
    const string testname1 = "testfile-small.xxx";
    const string testfileOriginalName = "xunit.abstractions.dll";
    const string vaultName = "HelloWorld-encrypt-small.zvlt";
    using(var keyChain = new KeyChain())
    {
      var pkif = CreateTestKeyInfo(passphraseText, stamp, keyChain);
      CloneSource(testfileOriginalName, testname1, stamp);
      var vaultFile = ResetVault(pkif, vaultName, stamp);

      BlockElement be;
      using(var cryptor = vaultFile.CreateCryptor(keyChain))
      using(var vaultWriter = new VaultFileWriter(vaultFile, cryptor))
      {
        be = vaultWriter.AppendFile(testname1, utcStampOverride: stamp);
      }

      Assert.NotNull(be);
      Assert.Equal(3, be.Children.Count);
      Assert.Equal(Zvlt2BlockType.FileHeader, be.Block.Kind);
      Assert.Equal(Zvlt2BlockType.FileMetadata, be.Children[0].Block.Kind);
      Assert.Equal(Zvlt2BlockType.FileContent, be.Children[1].Block.Kind);
      Assert.Equal(BlockType.ImpliedGroupEnd, be.Children[2].Block.Kind);

      var readVault = VaultFile.Open(vaultName);
      Assert.NotNull(readVault);
      foreach(var block in readVault.Blocks.Blocks)
      {
        _outputHelper.WriteLine($"'{BlockType.ToText(block.Kind)}' @{block.Offset:X6} ({block.Size,6} bytes)");
      }
      Assert.Equal(6, readVault.Blocks.Blocks.Count);

      var elemTree2 = readVault.Blocks.BuildElementTree();
      Assert.NotNull(elemTree2);
      Assert.Equal(3, elemTree2.Children.Count);
      Assert.Equal(0, elemTree2.Children[0].Children.Count);
      Assert.Equal(0, elemTree2.Children[1].Children.Count);
      Assert.Equal(3, elemTree2.Children[2].Children.Count);
    }
  }

  [Fact]
  public void CanEncryptFileLarge()
  {
    var stamp = new DateTime(2023, 5, 19, 1, 2, 3, 4, DateTimeKind.Utc);
    const string passphraseText = "HelloWorld";
    const string testname1 = "testfile-large.xxx";
    const string testfileOriginalName = "Newtonsoft.Json.dll";
    const string vaultName = "HelloWorld-encrypt-large.zvlt";
    using(var keyChain = new KeyChain())
    {
      var pkif = CreateTestKeyInfo(passphraseText, stamp, keyChain);
      CloneSource(testfileOriginalName, testname1, stamp);
      var vaultFile = ResetVault(pkif, vaultName, stamp);

      BlockElement be;
      using(var cryptor = vaultFile.CreateCryptor(keyChain))
      using(var vaultWriter = new VaultFileWriter(vaultFile, cryptor))
      {
        be = vaultWriter.AppendFile(testname1, utcStampOverride: stamp);
      }

      Assert.NotNull(be);
      Assert.Equal(5, be.Children.Count);
      Assert.Equal(Zvlt2BlockType.FileHeader, be.Block.Kind);
      Assert.Equal(Zvlt2BlockType.FileMetadata, be.Children[0].Block.Kind);
      Assert.Equal(Zvlt2BlockType.FileContent, be.Children[1].Block.Kind);
      Assert.Equal(Zvlt2BlockType.FileContent, be.Children[2].Block.Kind);
      Assert.Equal(Zvlt2BlockType.FileContent, be.Children[3].Block.Kind);
      Assert.Equal(BlockType.ImpliedGroupEnd, be.Children[4].Block.Kind);

      var readVault = VaultFile.Open(vaultName);
      Assert.NotNull(readVault);
      foreach(var block in readVault.Blocks.Blocks)
      {
        _outputHelper.WriteLine($"'{BlockType.ToText(block.Kind)}' @{block.Offset:X6} ({block.Size,6} bytes)");
      }
      Assert.Equal(8, readVault.Blocks.Blocks.Count);

      var elemTree2 = readVault.Blocks.BuildElementTree();
      Assert.NotNull(elemTree2);
      Assert.Equal(3, elemTree2.Children.Count);
      Assert.Equal(0, elemTree2.Children[0].Children.Count);
      Assert.Equal(0, elemTree2.Children[1].Children.Count);
      Assert.Equal(5, elemTree2.Children[2].Children.Count);
    }
  }

  [Fact(/* Skip = "Functionality NYI" */)]
  public void CanDecryptFile()
  {
    var stamp = new DateTime(2023, 5, 19, 1, 2, 3, 4, DateTimeKind.Utc);
    const string passphraseText = "HelloWorld";
    const string testnameIn = "testfile-decryption.xxx";
    const string testnameOut1 = "out.testfile-decryption.xxx";
    const string testnameOut2 = "out-2.testfile-decryption.xxx";
    const string testnameOut3 = "dump/out-3.testfile-decryption.xxx";
    const string testfileOriginalName = "xunit.abstractions.dll";
    const string vaultName = "HelloWorld-decrypt.zvlt";
    var nonceGenerator = new NonceGenerator();
    var fileId = Guid.NewGuid();
    using(var keyChain = new KeyChain())
    {
      var pkif = CreateTestKeyInfo(passphraseText, stamp, keyChain);
      CloneSource(testfileOriginalName, testnameIn, stamp);
      var vaultFile0 = ResetVault(pkif, vaultName, stamp);
      BlockElement be;
      using(var cryptor = vaultFile0.CreateCryptor(keyChain, nonceGenerator))
      using(var vaultWriter = new VaultFileWriter(vaultFile0, cryptor))
      {
        _outputHelper.WriteLine($"Appending {Path.GetFileName(testnameIn)} to {Path.GetFileName(vaultName)}");
        var metaAdditions = new JObject {
          ["null"] = null,
          ["text"] = "text",
          ["number"] = 42L,
          //["boolean"] = true,
          //["object"] = new JObject { ["hello"] = "world" },
          //["list"] = new JArray { 1, 2, "many"},
          //["guid"] = Guid.NewGuid(),
        };
        be = vaultWriter.AppendFile(testnameIn, utcStampOverride: stamp, additionalMetadata: metaAdditions, fileIdOverride: fileId);
      }
      Assert.NotNull(be);
      Assert.Equal(3, be.Children.Count);

      var vf = VaultFile.Open(vaultName);
      Assert.NotNull(vf);
      var elements = vf.Children;
      Assert.NotNull(elements);
      Assert.Equal(3, elements.Count);

      using(var cryptor = vf.CreateCryptor(keyChain, nonceGenerator))
      using(var vaultReader = new VaultFileReader(vf, cryptor))
      {
        var fileElements = elements.Where(e => e.Block.Kind == Zvlt2BlockType.FileHeader).ToList();
        Assert.Single(fileElements);
        var fe = new FileElement(fileElements[0]);
        var tagBytes = new byte[16];
        var metadata = fe.GetMetadata(vaultReader, tagBytes);
        Assert.NotNull(metadata);
        Assert.Equal(testnameIn, metadata.Name);
        var json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
        _outputHelper.WriteLine($"Metadata: {json}");
        var header = fe.GetHeader(vaultReader); // this is already cached
        Assert.NotNull(header);
        var encryptionStamp = EpochTicks.ToUtc(header.EncryptionStamp);
        Assert.Equal(stamp, encryptionStamp);
        Assert.Equal(fileId, header.FileId);
        _outputHelper.WriteLine($"File ID: {header.FileId}");
        _outputHelper.WriteLine($"Total length: {fe.GetContentLength()}");

        // extract as stream
        if(File.Exists(testnameOut1))
        {
          File.Delete(testnameOut1);
        }
        Assert.False(File.Exists(testnameOut1));
        using(var outstream = File.Create(testnameOut1))
        {
          fe.SaveContentToStream(vaultReader, outstream);
        }
        Assert.True(File.Exists(testnameOut1));
        var fiIn = new FileInfo(testnameIn);
        var fiOut = new FileInfo(testnameOut1);
        Assert.Equal(fiIn.Length, fiOut.Length);

        // extract to named file
        if(File.Exists(testnameOut2))
        {
          File.Delete(testnameOut2);
        }
        Assert.False(File.Exists(testnameOut2));
        Assert.Throws<InvalidOperationException>(() => {
          fe.SaveContentToFile(vaultReader, rootFolder: ".", fileName: testnameOut2, checkFolder: true);
        });
        Assert.False(File.Exists(testnameOut2));
        fe.SaveContentToFile(vaultReader, rootFolder: ".", fileName: testnameOut2, checkFolder: false);
        Assert.True(File.Exists(testnameOut2));

        // extract to named in other folder
        if(File.Exists(testnameOut3))
        {
          File.Delete(testnameOut3);
        }
        Assert.False(File.Exists(testnameOut3));
        fe.SaveContentToFile(vaultReader, rootFolder: ".", fileName: testnameOut3, checkFolder: true);
        Assert.True(File.Exists(testnameOut3));
      }
    }
  }

  private PassphraseKeyInfoFile CreateTestKeyInfo(string passphraseText, DateTime stamp, KeyChain? keyChain)
  {
    ReadOnlySpan<byte> salt = CreateFixedBadSalt(); // "fixed" == "don't do this in real applications"
    PassphraseKeyInfoFile pkif;
    using(var passphraseBuffer = new CryptoBuffer<char>(passphraseText.ToCharArray()))
    {
      using(var pk = PassphraseKey.FromCharacters(passphraseBuffer, salt))
      {
        pkif = new PassphraseKeyInfoFile(pk, stamp);
        if(keyChain != null)
        {
          keyChain.PutCopy(pk);
        }
      }
    }
    _outputHelper.WriteLine($"Key ID is {pkif.KeyId}");
    return pkif;
  }

  /// <summary>
  /// Create a new "salt" with a fixed content.
  /// For testing purposes only of course! "fixed content" and "proper salt"
  /// are mutually exclusive.
  /// </summary>
  /// <param name="byteCount">
  /// The number of bytes (default 64)
  /// </param>
  /// <returns>
  /// A new byte array
  /// </returns>
  private byte[] CreateFixedBadSalt(int byteCount = PassphraseKey.Saltlength)
  {
    var saltBuffer = new byte[byteCount];
    for(var i = 0; i< saltBuffer.Length; i++)
    {
      saltBuffer[i] = (byte)(i+1);
    }
    return saltBuffer;
  }

  private string DumpHex(ReadOnlySpan<byte> bytes)
  {
    var sb = new StringBuilder();
    for(var i = 0; i<bytes.Length; i++)
    {
      var b = bytes[i];
      if(i > 0 && (i%8)==0)
      {
        if(i%32 == 0)
        {
          sb.Append("\r\n");
        }
        else
        {
          sb.Append(' ');
        }
      }
      sb.Append(b.ToString("X2"));
    }
    return sb.ToString();
  }

  private VaultFile ResetVault(PassphraseKeyInfoFile pkif, string vaultName, DateTime stamp)
  {
    if(File.Exists(vaultName))
    {
      _outputHelper.WriteLine($"Deleting existing {Path.GetFileName(vaultName)}");
      File.Delete(vaultName);
    }
    Assert.False(File.Exists(vaultName));

    _outputHelper.WriteLine($"Creating new {Path.GetFileName(vaultName)}");
    var vaultFile = VaultFile.OpenOrCreate(vaultName, pkif, stamp);
    Assert.NotNull(vaultFile);
    Assert.True(File.Exists(vaultName));
    return vaultFile;
  }

  private void CloneSource(string original, string copy, DateTime stamp)
  {
    if(File.Exists(copy))
    {
      _outputHelper.WriteLine($"Deleting existing {Path.GetFileName(copy)}");
      File.Delete(copy);
    }
    _outputHelper.WriteLine($"Copying {Path.GetFileName(original)} to {Path.GetFileName(copy)}");
    File.Copy(original, copy);
    Assert.True(File.Exists(copy));
    File.SetLastWriteTimeUtc(copy, stamp);
  }


}
