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

  /// <summary>
  /// Return true if the block type is recognized as a group start.
  /// That is: the last character of its 4CC representation is '('.
  /// </summary>
  public static bool IsGroupStart(int blockType)
  {
    return (blockType & 0x7F000000) == 0x28000000;
  }

  /// <summary>
  /// Return true if the block type is recognized as a group end.
  /// That is: the first or last character of its 4CC representation is ')'.
  /// In practice group end blocks are usually of type <see cref="ImpliedGroupEnd"/>
  /// </summary>
  public static bool IsGroupEnd(int blockType)
  {
    return ((blockType & 0x000000FF) == 0x00000029) || ((blockType & 0x7F000000) == 0x29000000);
  }

}
