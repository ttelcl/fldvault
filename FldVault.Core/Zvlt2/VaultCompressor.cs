/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Crypto;

using ICSharpCode.SharpZipLib.BZip2;

namespace FldVault.Core.Zvlt2;

/// <summary>
/// Compression library adapter
/// </summary>
public static class VaultCompressor
{
  /*
   * Compression in ZVLT works on each block individually:
   * the content of the block is compressed as an independent unit
   * using BZ2.
   * 
   * For now we use the ICSharpCode.SharpZipLib.BZip2 library even
   * though its API is misoptimized for our use case (leading to
   * a lot more LOH allocations than necessary)
   */

  /// <summary>
  /// Try to compress the first <paramref name="count"/> bytes of <paramref name="bcbIn"/>
  /// to <paramref name="bcbOut"/>, and return the number of bytes used in the output buffer.
  /// Returns -1 to indicate ineffective compression.
  /// </summary>
  /// <param name="bcbIn">
  /// The input buffer
  /// </param>
  /// <param name="count">
  /// The number of bytes used in the input buffer
  /// </param>
  /// <param name="bcbOut">
  /// The output buffer
  /// </param>
  /// <returns>
  /// The number of bytes stored in the output buffer, or -1 if the compression wasn't
  /// effective.
  /// </returns>
  /// <remarks>
  /// <para>
  /// The "compression is ineffective" if any of the following is true.
  /// </para>
  /// <list type="bullet">
  /// <item>
  /// The input was less than 256 bytes.
  /// </item>
  /// <item>
  /// The resulting "compressed" block has a size outSize that does not meet the
  /// requirement (outSize+256) &lt; inSize.
  /// </item>
  /// </list>
  /// </remarks>
  public static int Compress(ByteCryptoBuffer bcbIn, int count, ByteCryptoBuffer bcbOut)
  {
    if(count < 256)
    {
      return -1;
    }
    try
    {
      using(var sIn = bcbIn.ReadableStream(count))
      using(var sOut = bcbOut.WriteableStream())
      {
        BZip2.Compress(sIn, sOut, false, 9);
        if(sIn.Position != sIn.Length)
        {
          throw new InvalidOperationException("Internal error");
        }
        if(sOut.OverflowDetected)
        {
          sOut.OverflowDetected = false;
          return -1;
        }
        var length = (int)sOut.Length;
        return (length + 256) < count ? length : -1;
      }
    }
    catch(NotSupportedException) // thrown when writing more content than space in the output buffer
    {
      return -1;
    }
  }

  /// <summary>
  /// Decompress the first <paramref name="count"/> bytes of <paramref name="bcbIn"/>
  /// into the output buffer <paramref name="bcbOut"/> and return the number of resulting bytes.
  /// </summary>
  public static int Decompress(ByteCryptoBuffer bcbIn, int count, ByteCryptoBuffer bcbOut)
  {
    using(var sIn = bcbIn.ReadableStream(count))
    using(var sOut = bcbOut.WriteableStream())
    {
      BZip2.Decompress(sIn, sOut, false);
      var length = (int)sOut.Length;
      return length;
    }
  }

}
