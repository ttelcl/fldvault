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

using UdSocketLib.Framing;
using UdSocketLib.Framing.Layer1;

namespace FldVault.KeyServer;

/// <summary>
/// Defines key server message codes and extension methods
/// for reading and writting the associated messages.
/// </summary>
public static class KeyServerMessages
{
  /// <summary>
  /// Simple single key request. The payload is just the 16 bytes of the requested Guid.
  /// Expected responses: <see cref="KeyResponseCode"/> or <see cref="KeyNotFoundCode"/>.
  /// </summary>
  public const int KeyRequestCode = 0x10010000;

  /// <summary>
  /// Response message code for a successful key request. The payload is the 32 byte raw key.
  /// </summary>
  public const int KeyResponseCode = 0x10010001;

  /// <summary>
  /// Response message indicating that the requested key was not found (no content)
  /// </summary>
  public const int KeyNotFoundCode = MessageCodes.NotFound;

  /// <summary>
  /// Key upload "request". The content is the 32 bytes of the key. Expected response
  /// <see cref="KeyUploadedCode"/> (== <see cref="MessageCodes.OkNoContent"/>)
  /// </summary>
  public const int KeyUploadCode = 0x10001002;

  /// <summary>
  /// The code indicating that the key upload succeeded (no content)
  /// </summary>
  public const int KeyUploadedCode = MessageCodes.OkNoContent;

  /// <summary>
  /// Request to remove a key. The content is the key Guid. Expected responses
  /// <see cref="KeyRemovedCode"/> (== <see cref="MessageCodes.OkNoContent"/>) or
  /// <see cref="KeyNotFoundCode"/> (== <see cref="MessageCodes.NotFound"/>).
  /// </summary>
  public const int KeyRemoveCode = 0x10010003;

  /// <summary>
  /// The code to indicate that the key was removed. No content. (numerically same as KeyUploadedCode)
  /// </summary>
  public const int KeyRemovedCode = MessageCodes.OkNoContent;

  /// <summary>
  /// Request and response for checking which keys in a list of key guids
  /// are present in the server. The response contains the subset of keys
  /// that is present. This is a variable length message.
  /// </summary>
  public const int KeyPresenceListCode = 0x10010004;

  /// <summary>
  /// Request for the server to log diagnostics. The caller receives a plain
  /// <see cref="MessageCodes.OkNoContent"/> as response.
  /// </summary>
  public const int ServerDiagnosticsCode = 0x00010005;

  /// <summary>
  /// Read the key to look up from the key request message in the frame
  /// </summary>
  /// <param name="frame">
  /// The frame holding the received message
  /// </param>
  /// <returns>
  /// The extracted GUID
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Something went wrong: The frame's message code was wrong or the frame content was too short.
  /// </exception>
  public static Guid ReadKeyRequest(this MessageFrameIn frame)
  {
    frame
      .Rewind()
      .ValidateI32(KeyRequestCode, "Internal error: Incorrect message code")
      .TakeGuid(out var guid)
      .EnsureFullyRead();
    return guid;
  }

  /// <summary>
  /// Write a key request message into the output frame
  /// </summary>
  public static void WriteKeyRequest(this MessageFrameOut frame, Guid keyId)
  {
    frame
      .Clear()
      .AppendI32(KeyRequestCode)
      .AppendGuid(keyId);
  }

  /// <summary>
  /// Write the response to the key lookup request into the frame (whether the lookup was 
  /// successful or failed)
  /// </summary>
  /// <param name="frame">
  /// The frame to write to
  /// </param>
  /// <param name="key">
  /// If the key lookup succeeded: the key buffer holding the key.
  /// If the key lookup failed: null
  /// </param>
  public static void WriteKeyResponse(this MessageFrameOut frame, KeyBuffer? key)
  {
    if(key == null)
    {
      frame.WriteNoContentMessage(KeyNotFoundCode);
    }
    else
    {
      frame
        .Clear()
        .AppendI32(KeyResponseCode)
        .AppendBytes(key.Bytes);
    }
  }

  /// <summary>
  /// Write a key upload message into the output frame
  /// </summary>
  /// <param name="frame">
  /// The frame to write to
  /// </param>
  /// <param name="key">
  /// The key to write
  /// </param>
  public static void WriteKeyUpload(this MessageFrameOut frame, KeyBuffer key)
  {
    frame
      .Clear()
      .AppendI32(KeyUploadCode)
      .AppendBytes(key.Bytes);
  }

  /// <summary>
  /// Read an uploaded key from the input frame.
  /// Then store it into the key chain and return the key ID
  /// </summary>
  /// <param name="frame">
  /// The frame containing the key upload message
  /// </param>
  /// <param name="keyChain">
  /// The key chain to store the key in
  /// </param>
  /// <returns>
  /// The GUID of the key.
  /// </returns>
  public static Guid ReadKeyUpload(this MessageFrameIn frame, KeyChain keyChain)
  {
    frame
      .Rewind()
      .ValidateI32(KeyUploadCode, "Unsupported message code for key transfer")
      .TakeSlice(32, out var span)
      .EnsureFullyRead();
    using(var kb = new KeyBuffer(span))
    {
      keyChain.PutCopy(kb);
      return kb.GetId();
    }
  }

  /// <summary>
  /// Read the found key from a key response.
  /// Then store it into the key chain and return the key ID
  /// </summary>
  /// <param name="frame">
  /// The frame containing the key response message
  /// </param>
  /// <param name="keyChain">
  /// The key chain to store the key in
  /// </param>
  /// <returns>
  /// The GUID of the key.
  /// </returns>
  public static Guid ReadKeyResponse(this MessageFrameIn frame, KeyChain keyChain)
  {
    frame
      .Rewind()
      .ValidateI32(KeyResponseCode, "Unsupported message code for key transfer")
      .TakeSlice(32, out var span)
      .EnsureFullyRead();
    using(var kb = new KeyBuffer(span))
    {
      keyChain.PutCopy(kb);
      return kb.GetId();
    }
  }

  /// <summary>
  /// Write one of the no-content messages into the output frame
  /// </summary>
  public static void WriteNoContent(this MessageFrameOut frame, int messageCode = MessageCodes.OkNoContent)
  {
    frame.Clear().AppendI32(messageCode);
  }

  /// <summary>
  /// Write an error message to the frame
  /// </summary>
  public static void WriteErrorResponse(this MessageFrameOut frame, string error)
  {
    frame
      .Clear()
      .AppendI32(MessageCodes.ErrorText)
      .AppendString(error);
  }

  /// <summary>
  /// Write an error message to the frame, derived from the exception type and message
  /// </summary>
  public static void WriteErrorResponse(this MessageFrameOut frame, Exception error)
  {
    frame.WriteErrorResponse(error.GetType().FullName + ": " + error.Message);
  }

  /// <summary>
  /// Read the key to remove from the key remove message in the frame
  /// </summary>
  /// <param name="frame">
  /// The frame holding the received message
  /// </param>
  /// <returns>
  /// The extracted GUID
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Something went wrong: The frame's message code was wrong or the frame content was too short.
  /// </exception>
  public static Guid ReadKeyRemove(this MessageFrameIn frame)
  {
    frame
      .Rewind()
      .ValidateI32(KeyRemoveCode, "Internal error: Incorrect message code")
      .TakeGuid(out var guid)
      .EnsureFullyRead();
    return guid;
  }

  /// <summary>
  /// Read a key presence request or response
  /// </summary>
  /// <param name="frame">
  /// The frame to read from
  /// </param>
  /// <returns>
  /// A list of the key ids in the messaage
  /// </returns>
  public static List<Guid> ReadKeyPresence(this MessageFrameIn frame)
  {
    frame
      .Rewind()
      .ValidateI32(KeyPresenceListCode, "Internal error: Incorrect message code");
    var list = new List<Guid>();
    while(frame.Space > 0)
    {
      var guid = frame.ReadGuid();
      list.Add(guid);
    }
    frame.EnsureFullyRead();
    return list;
  }

  /// <summary>
  /// Write a key presence request or reponse
  /// </summary>
  /// <param name="frame">
  /// The frame to write to
  /// </param>
  /// <param name="keys">
  /// The key ids to write (possibly none)
  /// </param>
  /// <returns>
  /// The number of keys written into the message
  /// </returns>
  public static int WriteKeyPresence(this MessageFrameOut frame, IEnumerable<Guid> keys)
  {
    frame
      .Clear()
      .AppendI32(KeyPresenceListCode);
    var n = 0;
    foreach(var guid in keys)
    {
      frame.AppendGuid(guid);
      n++;
    }
    return n;
  }

  /// <summary>
  /// Write a key remove message into the output frame
  /// </summary>
  public static void WriteKeyRemove(this MessageFrameOut frame, Guid keyId)
  {
    frame
      .Clear()
      .AppendI32(KeyRemovedCode)
      .AppendGuid(keyId);
  }
}
