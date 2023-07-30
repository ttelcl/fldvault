/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdSocketLib.Framing.Layer1;

/// <summary>
/// Base class for layer 1 messages
/// </summary>
public class Layer1Message
{
  /// <summary>
  /// Create a new Layer1MessageBase
  /// </summary>
  /// <param name="messageCode">
  /// The message code. For standard message codes see <see cref="MessageCodes"/>.
  /// </param>
  public Layer1Message(
    int messageCode)
  {
    MessageCode = messageCode;
  }

  /// <summary>
  /// The code identifying the message
  /// </summary>
  public int MessageCode { get; init; }

  /// <summary>
  /// Write this message to the provided frame.
  /// This base implementation first clears the frame and then writes
  /// the message code. Subclasses should override this method, first
  /// calling this base implementation, then append their own content.
  /// </summary>
  /// <param name="frame">
  /// The frame to write this message to
  /// </param>
  public virtual void SerializeToFrame(MessageFrameOut frame)
  {
    frame.Clear();
    frame.AppendI32(MessageCode);
  }

  /// <summary>
  /// Rewind an input frame and read the message code
  /// </summary>
  /// <param name="frame">
  /// The layer1 compatible frame to read the message code from
  /// </param>
  /// <returns>
  /// The message code
  /// </returns>
  public static int ReadMessageCode(MessageFrameIn frame)
  {
    frame.Rewind();
    if(frame.Length < 4)
    {
      throw new ArgumentOutOfRangeException(
        nameof(frame), "Expecting a frame loaded with a non-empty message");
    }
    return frame.ReadI32();
  }

  /// <summary>
  /// If the frame contains an empty layer 1 message then read it into
  /// <paramref name="message"/> and return true. Otherwise return false.
  /// </summary>
  public static bool TryReadEmptyMessage(
    MessageFrameIn frame,
    [MaybeNullWhen(false)] out Layer1Message message)
  {
    if(frame.Length == 4)
    {
      var mc = ReadMessageCode(frame);
      message = new Layer1Message(mc);
      return true;
    }
    else
    {
      message = null;
      return false;
    }
  }

}
