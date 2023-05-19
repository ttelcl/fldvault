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

    var saltBuffer = new byte[PassphraseKey.Saltlength];
    for(var i = 0; i< saltBuffer.Length; i++)
    {
      saltBuffer[i] = (byte)(i+1);
    }
    ReadOnlySpan<byte> salt = saltBuffer;

    _outputHelper.WriteLine($"Salt = {DumpHex(salt.Slice(0, 20))} ...");

    const string passphrase = "Hellö, wörld!!";
    const int keyLength = 32;

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

  private string DumpHex(ReadOnlySpan<byte> bytes)
  {
    var sb = new StringBuilder();
    for(var i=0; i<bytes.Length; i++)
    {
      var b = bytes[i];
      if(i > 0 && (i%8)==0)
      {
        sb.Append(' ');
      }
      sb.Append(b.ToString("X2"));
    }
    return sb.ToString();
  }

}
