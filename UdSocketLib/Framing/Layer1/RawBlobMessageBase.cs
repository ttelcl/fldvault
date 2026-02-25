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
/// A message carrying just a blob. Also used for messages carrying
/// just a string or a JSON string (depending on message code).
/// The blob is copied into a new byte array in the constructor; that
/// copy is never cleared, so this class is not fit for blobs
/// carrying secrets.
/// </summary>
public abstract class RawBlobMessageBase: Layer1Message
{
  private readonly byte[] _blob;

  /// <summary>
  /// Create a new <see cref="RawBlobMessageBase"/> instance with the given
  /// message type and the given blob as content. The blob is captured
  /// as-is (no copy is made).
  /// </summary>
  protected RawBlobMessageBase(int messageCode, byte[] blob)
    : base(messageCode)
  {
    _blob = blob;
  }

  /// <summary>
  /// A read-only view on the captured blob
  /// </summary>
  public ReadOnlySpan<byte> Blob { get => _blob.AsSpan(); }

  /// <summary>
  /// Serialize this message (RawBlob, RawText or RawJson)
  /// </summary>
  public override void SerializeToFrame(MessageFrameOut frame)
  {
    base.SerializeToFrame(frame);
    // Note that a blob containing utf8 encoded text are compatible serializes
    // the same as frame.AppendString() would.
    frame.AppendBlob(_blob); 
  }
}

/// <summary>
/// A raw blob message 
/// </summary>
public class RawBlobMessage: RawBlobMessageBase
{
  /// <summary>
  /// Create a new <see cref="RawBlobMessage"/> instance capturing a copy of the provided
  /// blob, using message code <see cref="MessageCodes.RawBlob"/>.
  /// </summary>
  public RawBlobMessage(ReadOnlySpan<byte> blob)
    : base(MessageCodes.RawBlob, blob.ToArray())
  {
  }

  /// <summary>
  /// Create a RawBlobMessage from a matching frame
  /// </summary>
  public static RawBlobMessage FromMessage(MessageFrameIn frame)
  {
    var mc = Layer1Message.ReadMessageCode(frame);
    if(mc != MessageCodes.RawBlob)
    {
      throw new InvalidOperationException(
        "Expecting a frame containing a RawBlob message");
    }
    return new RawBlobMessage(frame.ReadBlob());
  }
}

/// <summary>
/// A raw text message 
/// </summary>
public class RawTextMessage: RawBlobMessageBase
{
  /// <summary>
  /// Create a new <see cref="RawTextMessage"/> instance capturing the given text as
  /// an UTF8 encoded blob, using message code <see cref="MessageCodes.RawText"/>.
  /// </summary>
  public RawTextMessage(string text)
    : base(MessageCodes.RawText, Encoding.UTF8.GetBytes(text))
  {
  }

  internal RawTextMessage(ReadOnlySpan<byte> utf8Bytes)
    : base(MessageCodes.RawText, utf8Bytes.ToArray())
  {
  }

  /// <summary>
  /// Get the captured string value
  /// </summary>
  public string ExtractText()
  {
    return Encoding.UTF8.GetString(Blob);
  }

  /// <summary>
  /// Create a RawTextMessage from a matching frame
  /// </summary>
  public static RawTextMessage FromFrame(MessageFrameIn frame)
  {
    var mc = Layer1Message.ReadMessageCode(frame);
    if(mc != MessageCodes.RawText)
    {
      throw new InvalidOperationException(
        "Expecting a frame containing a RawText message");
    }
    return new RawTextMessage(frame.ReadBlob());
  }
}

/// <summary>
/// A raw JSON message 
/// </summary>
public class RawJsonMessage: RawBlobMessageBase
{
  /// <summary>
  /// Create a new <see cref="RawJsonMessage"/> instance capturing the given JSON text as
  /// an UTF8 encoded blob, using message code <see cref="MessageCodes.RawJson"/>.
  /// </summary>
  public RawJsonMessage(string json)
    : base(MessageCodes.RawJson, Encoding.UTF8.GetBytes(json))
  {
  }

  internal RawJsonMessage(ReadOnlySpan<byte> jsonBytes)
    : base(MessageCodes.RawJson, jsonBytes.ToArray())
  {
  }

  /// <summary>
  /// Get the captured string value (presumably valid JSON)
  /// </summary>
  public string ExtractJson()
  {
    return Encoding.UTF8.GetString(Blob);
  }

  /// <summary>
  /// Create a RawJsonMessage from a matching frame
  /// </summary>
  public static RawJsonMessage FromMessage(MessageFrameIn frame)
  {
    var mc = Layer1Message.ReadMessageCode(frame);
    if(mc != MessageCodes.RawJson)
    {
      throw new InvalidOperationException(
        "Expecting a frame containing a RawJson message");
    }
    return new RawJsonMessage(frame.ReadBlob());
  }

}
