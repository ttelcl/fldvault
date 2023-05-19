using System;
using System.Text;

using Xunit;
using Xunit.Abstractions;

using FldVault.Core;
using FldVault.Core.Crypto;
using System.Security;
using System.Text.Unicode;
using System.Linq;
using System.Collections.Generic;
using FldVault.Core.Vaults;
using System.IO;

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
      for(var i=1; i < bytes.Length; i++)
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
      Assert.Null(keychain[id1]);
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
      Assert.NotNull(keychain[id1]);
      byte[] key4 = keychain[id1]!.Bytes.ToArray();
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
    for(var i=0; i<bytes.Length; i++)
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


}
