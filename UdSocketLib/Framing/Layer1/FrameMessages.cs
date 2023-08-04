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
/// Common Message implementations and factories
/// </summary>
public static class FrameMessages
{
  /// <summary>
  /// A prebuilt message for <see cref="MessageCodes.OkNoContent"/>
  /// </summary>
  public static Layer1Message OkNoContent { get; } = new Layer1Message(MessageCodes.OkNoContent);

  /// <summary>
  /// A prebuilt message for <see cref="MessageCodes.Unrecognized"/>
  /// </summary>
  public static Layer1Message Unrecognized { get; } = new Layer1Message(MessageCodes.Unrecognized);

  /// <summary>
  /// A prebuilt message for <see cref="MessageCodes.KeepAlive"/>
  /// </summary>
  public static Layer1Message KeepAlive { get; } = new Layer1Message(MessageCodes.KeepAlive);

  /// <summary>
  /// Read the message code from an input frame
  /// </summary>
  public static int MessageCode(this MessageFrameIn frame) => Layer1Message.ReadMessageCode(frame);

  /// <summary>
  /// Read a <see cref="RawTextMessage"/> from the frame.
  /// Throws an exception if the frame does not contain such a message.
  /// </summary>
  public static RawTextMessage ReadTextMessage(this MessageFrameIn frame)
  {
    return RawTextMessage.FromFrame(frame);
  }

  /// <summary>
  /// Read a <see cref="RawJsonMessage"/> from the frame.
  /// Throws an exception if the frame does not contain such a message.
  /// </summary>
  public static RawJsonMessage ReadJsonMessage(this MessageFrameIn frame)
  {
    return RawJsonMessage.FromMessage(frame);
  }

  /// <summary>
  /// Read a <see cref="RawBlobMessage"/> from the frame.
  /// Throws an exception if the frame does not contain such a message.
  /// </summary>
  public static RawBlobMessage ReadBlobMessage(this MessageFrameIn frame)
  {
    return RawBlobMessage.FromMessage(frame);
  }

  /// <summary>
  /// Write a message into <paramref name="frame"/> that contains just a message code,
  /// without further content.
  /// </summary>
  /// <param name="frame">
  /// The frame to write to
  /// </param>
  /// <param name="messageCode">
  /// The message code to write
  /// </param>
  public static void WriteNoContentMessage(this MessageFrameOut frame, int messageCode)
  {
    frame.Clear();
    frame.AppendI32(messageCode);
  }

  /// <summary>
  /// Try to read the message in the frame as a text, error or json message
  /// </summary>
  /// <param name="frame">
  /// The frame to read from
  /// </param>
  /// <param name="message">
  /// On success: the text of the recognized text message
  /// </param>
  /// <param name="messageCode">
  /// On success: the recognized message code (<see cref="MessageCodes.RawText"/>,
  /// <see cref="MessageCodes.ErrorText"/> or <see cref="MessageCodes.RawJson"/>).
  /// On failure, for a frame of sufficient size: the message code.
  /// On failure, for an empty frame: 0
  /// </param>
  /// <returns>
  /// True on success, false on failure
  /// </returns>
  public static bool TryReadText(
    this MessageFrameIn frame,
    [MaybeNullWhen(false)] out string message,
    out int messageCode)
  {
    frame.Rewind();
    if(frame.Length >= 6)
    {
      messageCode = frame.ReadI32();
      switch(messageCode)
      {
        case MessageCodes.RawText:
        case MessageCodes.RawJson:
        case MessageCodes.ErrorText:
          message = frame.ReadString();
          return true;
        default:
          // fall through
          message = null;
          return false;
      }
    }
    else if(frame.Length >= 4)
    {
      messageCode = frame.ReadI32();
      message = null;
      return false;
    }
    else
    {
      message = null;
      messageCode = 0;
      return false;
    }
  }

}
