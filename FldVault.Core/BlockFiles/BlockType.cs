/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.BlockFiles;

/// <summary>
/// Constants used as block types in generic block files
/// (which include ZVLT V2 files; ZVLT specific blocks are specified
/// in <see cref="Zvlt2.Zvlt2BlockType"/>)
/// </summary>
public static class BlockType
{

  /// <summary>
  /// The generic terminator block type ("    ")
  /// </summary>
  public const int GenericTerminator = 0x20202020;

  /// <summary>
  /// Marker for the end of an implied group (')   ')
  /// </summary>
  public const int ImpliedGroupEnd = 0x20202029;

  /// <summary>
  /// Plain unauthenticated comment string ('UCMT')
  /// </summary>
  public const int UnauthenticatedComment = 0x544D4355;

  /// <summary>
  /// Return the block type code rendered as a 4CC string
  /// </summary>
  public static string ToText(int blockType)
  {
    Span<char> buffer = stackalloc char[4];

    for(var i = 0; i < 4; i++)
    {
      int code = blockType & 0x000000FF;
      blockType >>= 8;
      if(code >= 0x20 && code < 0x7F)
      {
        buffer[i] = (char)code;
      }
      else
      {
        buffer[i] = '?';
      }
    }
    return new String(buffer);
  }
}
