/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Crypto
{
  /// <summary>
  /// Helper class for generating 12 byte nonces for use with AesGcm
  /// </summary>
  public class NonceGenerator
  {
    private long _lastTicks;
    private readonly byte[] _randomBytes;

    /// <summary>
    /// Create a new NonceGenerator
    /// </summary>
    public NonceGenerator()
    {
      _lastTicks = DateTime.UtcNow.Ticks;
      _randomBytes = new byte[4];
      RandomNumberGenerator.Fill(_randomBytes);
    }

    /// <summary>
    /// Generate the next nonce in a way that guarantees it to be different
    /// from the ones before. It includes a part that depends on the current
    /// time, so it is not fully unpredictable and has a value that can be
    /// traced back to the current time.
    /// </summary>
    /// <param name="twelvebytes">
    /// The buffer to store the (12 byte) nonce
    /// </param>
    public void Next(Span<byte> twelvebytes)
    {
      if(twelvebytes.Length != 12)
      {
        throw new ArgumentOutOfRangeException(
          nameof(twelvebytes),
          $"Expecting the nonce buffer to be 12 bytes long, but it was {twelvebytes.Length} bytes");
      }
      var ticks = DateTime.UtcNow.Ticks;
      if(ticks <= _lastTicks)
      {
        // guarantee an increase, and therefore guarantee a never before seen value
        ticks = _lastTicks+1L; 
      }
      _lastTicks = ticks;
      _randomBytes.CopyTo(twelvebytes.Slice(8, 4));
      BinaryPrimitives.WriteInt64LittleEndian(twelvebytes.Slice(0, 8), _lastTicks);
    }

  }

}
