/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Crypto;
using FldVault.Core.Vaults;

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
  /// (The Key ID can be calculated from that if not already known)
  /// </summary>
  public const int KeyResponseCode = 0x10010001;

  /// <summary>
  /// Response message indicating that the requested key was not found (no content)
  /// </summary>
  public const int KeyNotFoundCode = MessageCodes.NotFound;

  /// <summary>
  /// Response message code indicating the key is known, but requires decloaking
  /// by the user.
  /// </summary>
  public const int KeyNotAllowedCode = 0x00000403;

  /// <summary>
  /// Key upload "request". The content is the 32 bytes of the key. Expected response
  /// <see cref="KeyUploadedCode"/> (== <see cref="MessageCodes.OkNoContent"/>)
  /// </summary>
  public const int KeyUploadCode = 0x10010002;

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
  /// Request single file key by file name (the server figures out what key belongs
  /// to the file). The payload is a (length-prefixed) string containing the name
  /// of the file. Potential responses: <see cref="KeyResponseCode"/>,
  /// <see cref="KeyNotFoundCode"/> or <see cref="MessageCodes.ErrorText"/>.
  /// </summary>
  public const int KeyForFileCode = 0x10010006;

  /// <summary>
  /// Key descriptor info request. The payload is the key GUID (in binary form).
  /// Potential responses: <see cref="KeyInfoResponseCode"/> or <see cref="KeyNotFoundCode"/>
  /// </summary>
  public const int KeyInfoCode = 0x10010007;

  /// <summary>
  /// Response when the requested key descriptor was found. The payload is a
  /// binary <see cref="PassphraseKeyInfoFile"/> blob (96 bytes).
  /// </summary>
  public const int KeyInfoResponseCode = 0x10010008;

  /// <summary>
  /// Upload one or more keys to the server in one operation. The payload is the
  /// N*32 keys to upload.
  /// The response is <see cref="KeyUploadedCode"/>, which is an alias for
  /// <see cref="MessageCodes.OkNoContent"/>.
  /// </summary>
  public const int KeyUploadManyCode = 0x10010009;

  /// <summary>
  /// Upload one or more key information blocks to the server. The payload is the
  /// N*96 key info blocks (binary <see cref="PassphraseKeyInfoFile"/> blocks).
  /// The response is <see cref="KeyUploadedCode"/>, which is an alias for
  /// <see cref="MessageCodes.OkNoContent"/>.
  /// </summary>
  public const int KeyInfoUploadManyCode = 0x1001000A;

  /// <summary>
  /// A duplicate of <see cref="MessageCodes.NoServer"/>. Not an actual message code,
  /// but a library response code indicating the server was unreachable.
  /// </summary>
  public const int NoServer = MessageCodes.NoServer;

  /// <summary>
  /// A duplicate of <see cref="MessageCodes.Unrecognized"/>. Indicates that the
  /// server did not recognize or support the request.
  /// </summary>
  public const int Unrecognized = MessageCodes.Unrecognized;

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
  /// Read the key info request. The payload is the key GUID (in binary form).
  /// </summary>
  public static Guid ReadKeyInfoRequest(this MessageFrameIn frame)
  {
    frame
      .Rewind()
      .ValidateI32(KeyInfoCode, "Internal error: Incorrect message code")
      .TakeGuid(out var guid)
      .EnsureFullyRead();
    return guid;
  }

  /// <summary>
  /// Write a key info request
  /// </summary>
  public static void WriteKeyInfoRequest(this MessageFrameOut frame, Guid keyId)
  {
    frame
      .Clear()
      .AppendI32(KeyInfoCode)
      .AppendGuid(keyId);
  }

  /// <summary>
  /// Write a key info response into the output frame.
  /// </summary>
  public static void WriteKeyInfoResponse(this MessageFrameOut frame,
    PassphraseKeyInfoFile? keyInfo)
  {
    if(keyInfo == null)
    {
      frame.WriteNoContentMessage(KeyNotFoundCode);
      return;
    }
    Span<byte> keyInfoBytes = stackalloc byte[96];
    keyInfo.SerializeToSpan(keyInfoBytes);
    frame
      .Clear()
      .AppendI32(KeyInfoResponseCode)
      .AppendBytes(keyInfoBytes);
  }

  /// <summary>
  /// Read a key info response from the input frame.
  /// </summary>
  public static PassphraseKeyInfoFile ReadKeyInfoResponse(this MessageFrameIn frame)
  {
    frame
      .Rewind()
      .ValidateI32(KeyInfoResponseCode, "Internal error: Incorrect message code")
      .TakeSlice(96, out var keyInfoBytes)
      .EnsureFullyRead();
    return PassphraseKeyInfoFile.ReadFrom(keyInfoBytes);
  }

  /// <summary>
  /// Read the file name to find the key for. The file name is not validated by
  /// this method (the caller should do so).
  /// </summary>
  public static string ReadKeyForFileRequest(this MessageFrameIn frame)
  {
    frame
      .Rewind()
      .ValidateI32(KeyForFileCode, "Internal error: Incorrect message code")
      .TakeString(out var fileName);
    return fileName;
  }

  /// <summary>
  /// Write a key-for-file request.
  /// </summary>
  /// <param name="frame">
  /// The frame to write the request to
  /// </param>
  /// <param name="fileName">
  /// The name of the existing file. The full path will be sent in the request.
  /// </param>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the file name is not valid or the file does not exist.
  /// </exception>
  public static void WriteKeyForFileRequest(this MessageFrameOut frame, string fileName)
  {
    if(String.IsNullOrEmpty(fileName))
    {
      throw new InvalidOperationException("No file name given");
    }
    if(!File.Exists(fileName))
    {
      throw new InvalidOperationException("Expecting the name of an existing file");
    }
    fileName = Path.GetFullPath(fileName);
    frame
      .Clear()
      .AppendI32(KeyForFileCode)
      .AppendString(fileName);
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
  /// If the key lookup failed: null.
  /// If the key exists but is cloaked: an empty key buffer.
  /// </param>
  public static void WriteKeyResponse(this MessageFrameOut frame, IBytesWrapper? key)
  {
    if(key == null)
    {
      frame.WriteNoContentMessage(KeyNotFoundCode);
    }
    else if(key.Bytes.Length == 0)
    {
      frame
        .Clear()
        .AppendI32(KeyNotAllowedCode);
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
  public static void WriteKeyUpload(this MessageFrameOut frame, IBytesWrapper key)
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
  /// Write a multikey upload message into the output frame.
  /// Consider using <see cref="WriteKeysUpload(MessageFrameOut, KeyChain, IEnumerable{Guid})"/>
  /// instead.
  /// </summary>
  /// <param name="frame"></param>
  /// <param name="keys">
  /// One or more keys to upload (there must be at least one)
  /// </param>
  /// <exception cref="InvalidOperationException"></exception>
  public static void WriteKeysUpload(this MessageFrameOut frame, IEnumerable<IBytesWrapper> keys)
  {
    frame
      .Clear()
      .AppendI32(KeyUploadManyCode);
    var checkpoint = frame.Position;
    foreach(var key in keys)
    {
      frame.AppendBytes(key.Bytes);
    }
    if(checkpoint == frame.Position)
    {
      throw new InvalidOperationException(
        "There should be at least 1 key as argument");
    }
  }

  /// <summary>
  /// Write a multikey upload message into the output frame
  /// </summary>
  /// <param name="frame"></param>
  /// <param name="keyChain">
  /// The keychain containing the actual keys
  /// </param>
  /// <param name="keyIds">
  /// The IDs of one or more keys to upload (there must be at least one, and all keys
  /// must be present in <paramref name="keyChain"/>)
  /// </param>
  /// <exception cref="InvalidOperationException"></exception>
  public static void WriteKeysUpload(this MessageFrameOut frame, KeyChain keyChain, IEnumerable<Guid> keyIds)
  {
    frame
      .Clear()
      .AppendI32(KeyUploadManyCode);
    var checkpoint = frame.Position;
    foreach(var keyId in keyIds)
    {
      if(!keyChain.TryUseKey(keyId, (id,keyBytes) => frame.AppendBytes(keyBytes.Bytes)))
      {
        throw new InvalidOperationException(
          $"Key {keyId} is missing from the provided key chain");
      }
    }
    if(checkpoint == frame.Position)
    {
      throw new InvalidOperationException(
        "There should be at least 1 key as argument");
    }
  }

  /// <summary>
  /// Read the keys of a <see cref="KeyUploadManyCode"/> message and load them into
  /// <paramref name="keyChain"/>. If successful, a list of the ids of the keys that were
  /// read is returned (an empty message is silently accepted, returning an empty list).
  /// </summary>
  /// <param name="frame"></param>
  /// <param name="keyChain"></param>
  /// <returns></returns>
  public static IReadOnlyList<Guid> ReadKeysUpload(this MessageFrameIn frame, KeyChain keyChain)
  {
    var keylist = new List<Guid>();
    frame
      .Rewind()
      .ValidateI32(KeyUploadManyCode, "Unsupported message code for key transfer");
    if(frame.Space % 32 != 0)
    {
      throw new InvalidOperationException(
        "Invalid request size: expecting one or more 32-byte keys and nothing else");
    }
    // silently accept an empty upload list (0 keys) as well
    while(frame.Space > 0)
    {
      frame.TakeSlice(32, out var span);
      using(var kb = new KeyBuffer(span))
      {
        keyChain.PutCopy(kb);
        keylist.Add(kb.GetId());
      }
    }
    return keylist;
  }

  /// <summary>
  /// Write a <see cref="KeyInfoUploadManyCode"/> message, carying one or more key-info objects
  /// (binarized <see cref="PassphraseKeyInfoFile"/>s) into this frame.
  /// </summary>
  /// <param name="frame"></param>
  /// <param name="keyinfos">
  /// The key info objects to be serialized inside the message. There must be at least one.
  /// </param>
  /// <exception cref="InvalidOperationException">
  /// Thrown if <paramref name="keyinfos"/> appears to be empty.
  /// </exception>
  public static void WriteKeyInfosUpload(this MessageFrameOut frame, IEnumerable<PassphraseKeyInfoFile> keyinfos)
  {
    frame
      .Clear()
      .AppendI32(KeyInfoUploadManyCode);
    Span<byte> keyInfoBytes = stackalloc byte[96];
    var checkpoint = frame.Position;
    foreach(var pkif in keyinfos)
    {
      pkif.SerializeToSpan(keyInfoBytes);
      frame.AppendBytes(keyInfoBytes);
    }
    if(checkpoint == frame.Position)
    {
      throw new InvalidOperationException(
        "There should be at least 1 keyinfo as argument");
    }
  }

  /// <summary>
  /// Read the content of a <see cref="KeyInfoUploadManyCode"/> message, returning the
  /// <see cref="PassphraseKeyInfoFile"/>s read. While an "empty" message is not valid, that is
  /// silently accepted (returning an empty list). A message that does not contain an integer
  /// number of key-info blocks causes an exception.
  /// </summary>
  /// <param name="frame"></param>
  /// <returns>
  /// A list with the <see cref="PassphraseKeyInfoFile"/> objects read
  /// </returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown when the message payload size is invalid (not a multiple of 96 bytes)
  /// </exception>
  public static IReadOnlyList<PassphraseKeyInfoFile> ReadKeyInfosUpload(this MessageFrameIn frame)
  {
    frame
      .Rewind()
      .ValidateI32(KeyInfoUploadManyCode, "Unsupported message code for key info transfer");
    var keyinfos = new List<PassphraseKeyInfoFile>();
    if(frame.Space % 96 != 0)
    {
      throw new InvalidOperationException(
        "Invalid request size: expecting one or more 96-byte key-info blocks and nothing else");
    }
    // silently accept an empty upload list (0 keys) as well
    while(frame.Space > 0)
    {
      frame.TakeSlice(96, out var span);
      var pkif = PassphraseKeyInfoFile.ReadFrom(span);
      keyinfos.Add(pkif);
    }
    return keyinfos;
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
      .AppendI32(KeyRemoveCode)
      .AppendGuid(keyId);
  }
}
