using System;
using System.Text;

using Xunit;
using Xunit.Abstractions;

using FldVault.Core;
using FldVault.Core.Crypto;
using System.Security;
using System.Text.Unicode;

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

    const string passphrase = "Hellö, wörld!";
    const int keyLength = 16;

    byte[] key1;
    using(var characters = CryptoBuffer<char>.FromSpanClear(passphrase.ToCharArray()))
    {
      using(var pk = PassphraseKey.FromCharacters(keyLength, characters, salt))
      {
        key1 = pk.Bytes.ToArray();
      }
    }
    _outputHelper.WriteLine($"Key via character array = {DumpHex(key1)}");

    byte[] key2;
    using(var securepw = new SecureString())
    {
      foreach(var ch in passphrase)
      {
        securepw.AppendChar(ch);
      }
      using(var pk = PassphraseKey.FromSecureString(keyLength, securepw, salt))
      {
        key2 = pk.Bytes.ToArray();
      }
    }
    _outputHelper.WriteLine($"Key via secure string   = {DumpHex(key2)}");

    byte[] key3;
    var byteCount = Encoding.UTF8.GetByteCount(passphrase);
    using(var bytes = new CryptoBuffer<byte>(byteCount))
    {
      Encoding.UTF8.GetBytes(passphrase.ToCharArray(), bytes.Span());
      using(var pk = PassphraseKey.FromBytes(keyLength, bytes, salt))
      {
        key3 = pk.Bytes.ToArray();
      }
    }
    _outputHelper.WriteLine($"Key via byte array      = {DumpHex(key3)}");

    Assert.Equal(key1, key2);
    Assert.Equal(key1, key3);

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
