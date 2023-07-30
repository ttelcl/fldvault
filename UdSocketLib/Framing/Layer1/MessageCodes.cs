/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdSocketLib.Framing.Layer1;

/// <summary>
/// Static class defining standard message codes
/// </summary>
public static class MessageCodes
{
  /// <summary>
  /// Generic "OK" response, without any content
  /// </summary>
  public const int OkNoContent = 0x00000204;

  /// <summary>
  /// Generic "Bad or unrecognized message" response (without any explanatory content)
  /// </summary>
  public const int Unrecognized = 0x00000400;

  /// <summary>
  /// Generic "not found" response (without any explanatory content)
  /// </summary>
  public const int NotFound = 0x00000404;

  /// <summary>
  /// Generic "keep connection alive" (NOP) request. Also used as response.
  /// </summary>
  public const int KeepAlive = 0x00000000;

  /// <summary>
  /// Generic request carrying just a string as content. Also used as response
  /// to that request (possibly carrying an empty string)
  /// </summary>
  public const int RawText = 0x10000001;

  /// <summary>
  /// Generic request (or response) carrying JSON content. Interpretation
  /// is application dependent. 
  /// </summary>
  public const int RawJson = 0x10000002;

  /// <summary>
  /// Generic request carrying just a blob as content. Also used as response
  /// to that request (possibly carrying an empty blob)
  /// </summary>
  public const int RawBlob = 0x10000003;

}