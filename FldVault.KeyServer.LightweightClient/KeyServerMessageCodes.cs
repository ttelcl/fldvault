using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UdSocketLib.Framing.Layer1;

namespace FldVault.KeyServer.LightweightClient;

/// <summary>
/// Key Server message codes used by this library.
/// A subset of <c>FldVault.KeyServer.KeyServerMessages</c>.
/// </summary>
public static class KeyServerMessageCodes
{

  /// <summary>
  /// Key upload "request". The content is the 32 bytes of the key. Expected response
  /// <see cref="KeyUploadedCode"/> (== <see cref="MessageCodes.OkNoContent"/>)
  /// </summary>
  public const int KeyUploadCode = 0x10010002;

  /// <summary>
  /// The code indicating that the key upload succeeded (no content)
  /// </summary>
  public const int KeyUploadedCode = MessageCodes.OkNoContent;
}
