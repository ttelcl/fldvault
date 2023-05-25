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

using FldVault.Core.Crypto;

namespace FldVault.Core.Vaults
{
  /// <summary>
  /// File based key cache
  /// </summary>
  public class UnlockStore: IKeyCacheStore
  {
    /// <summary>
    /// Create a new UnlockStore
    /// </summary>
    public UnlockStore()
    {
      CacheFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ".zvlt",
        "cache");
      if(!Directory.Exists(CacheFolder))
      {
        Directory.CreateDirectory(CacheFolder);
      }
    }

    /// <summary>
    /// Default instance
    /// </summary>
    public static IKeyCacheStore Default = new UnlockStore();

    /// <summary>
    /// The size of an unlock file
    /// </summary>
    public const int UnlockSize = 48;

    /// <summary>
    /// The folder in which unlock files are stored
    /// </summary>
    public string CacheFolder { get; init; }

    /// <summary>
    /// Try to find a key by ID, creating a new instance
    /// of <see cref="KeyBuffer"/> if found.
    /// </summary>
    /// <param name="keyId">
    /// The ID of the key
    /// </param>
    /// <returns>
    /// A newly created KeyBuffer if found, null if not found
    /// </returns>
    public KeyBuffer? LoadKey(Guid keyId)
    {
      var fileName = Path.Combine(CacheFolder, $"{keyId}.unlock");
      if(File.Exists(fileName))
      {
        byte[]? buffer = null;
        try
        {
          buffer = File.ReadAllBytes(fileName);
          if(buffer.Length >= UnlockSize) // allow larger files too
          {
            ReadOnlySpan<byte> span = buffer;
            var signature = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(0, 8));
            var unused = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(8, 8));
            if(signature == VaultFormat.Unlock0Signature && unused == 0L)
            {
              var kb = new KeyBuffer(span.Slice(8, 32));
              if(kb.GetId() == keyId)
              {
                return kb;
              }
              else
              {
                // Bad key file - name does not match the key
                kb.Dispose();
                throw new InvalidOperationException(
                  $"Corrupted file detected: {keyId}.unlock");
              }
            }
          }
        }
        finally
        {
          if(buffer != null)
          {
            Array.Clear(buffer, 0, buffer.Length);
          }
        }
      }
      return null;
    }

    /// <summary>
    /// If the key file exists, first set all bytes in it to 0,
    /// then delete the file.
    /// </summary>
    /// <returns>
    /// True if the file existed and was deleted.
    /// </returns>
    public bool EraseKey(Guid keyId)
    {
      var fileName = Path.Combine(CacheFolder, $"{keyId}.unlock");
      if(File.Exists(fileName))
      {
        byte[]? buffer = null;
        try
        {
          buffer = File.ReadAllBytes(fileName);
          Array.Clear(buffer, 0, buffer.Length);
          File.WriteAllBytes(fileName, buffer);
          File.Delete(fileName);
          return true;
        }
        finally
        {
          if(buffer != null)
          {
            Array.Clear(buffer, 0, buffer.Length);
          }
        }
      }
      else
      {
        return false;
      }
    }

    /// <summary>
    /// Store a key in this UnlockStore, creating an *.unlock file.
    /// </summary>
    /// <param name="keyBuffer">
    /// The key to store
    /// </param>
    /// <param name="overwrite">
    /// Determines what happens if the target file already exists.
    /// If true, the existing file is erased and deleted, then newly written.
    /// If false (default), this StoreKey request is ignored (nothing is
    /// written, assuming that the existing file is equivalent)
    /// </param>
    /// <returns>
    /// True if the key was stored, false if it already existed
    /// </returns>
    public bool StoreKey(KeyBuffer keyBuffer, bool overwrite = false)
    {
      var keyId = keyBuffer.GetId();
      var fileName = Path.Combine(CacheFolder, $"{keyId}.unlock");
      if(File.Exists(fileName))
      {
        if(overwrite)
        {
          EraseKey(keyId);
        }
        else
        {
          return false;
        }
      }
      byte[] buffer = new byte[UnlockSize];
      try
      {
        Span<byte> span = buffer;
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(0, 8), VaultFormat.Unlock0Signature);
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(8, 8), 0L);
        keyBuffer.Bytes.CopyTo(span.Slice(16, 32));
        File.WriteAllBytes(fileName, buffer);
      }
      finally
      {
        Array.Clear(buffer, 0, buffer.Length);
      }
      return true;
    }

  }
}
