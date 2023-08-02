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
/// A base message serving engine. Subclasses should register message handlers.
/// </summary>
public class MessageServer
{
  private readonly Dictionary<int, Action<MessageFrameIn, MessageFrameOut>> _commandHandlers;

  /// <summary>
  /// Create a new MessageServer, with only a handler for <see cref="MessageCodes.KeepAlive"/>
  /// pre-installed.
  /// </summary>
  public MessageServer()
  {
    _commandHandlers = new Dictionary<int, Action<MessageFrameIn, MessageFrameOut>>();
    RegisterHandler(MessageCodes.KeepAlive, HandleKeepAlive);
  }

  /// <summary>
  /// Dispatch the message found in the input frame to the appropriate message handler, and
  /// write the response to the output frame. If no matching handler is found, an
  /// <see cref="MessageCodes.Unrecognized"/> response is written as response.
  /// If any exception is thrown, it is up to the caller to catch that
  /// and format an error message.
  /// </summary>
  /// <param name="frameIn">
  /// The input frame, filled with the message to process
  /// </param>
  /// <param name="frameOut">
  /// The output frame, which will receive the response.
  /// </param>
  public void ProcessMessage(MessageFrameIn frameIn, MessageFrameOut frameOut)
  {
    try
    {
      var messageCode = frameIn.MessageCode();
      if(_commandHandlers.TryGetValue(messageCode, out var handler))
      {
        handler(frameIn, frameOut);
      }
      else
      {
        frameOut.Clear().AppendI32(MessageCodes.Unrecognized);
      }
    }
    catch(Exception)
    {
      // make sure not to leave the output in a possibly half-complete state.
      frameOut.Clear();
      throw;
    }
  }

  /// <summary>
  /// Register (or unregister) a message handler for the specied message code
  /// </summary>
  public void RegisterHandler(int messageCode, Action<MessageFrameIn, MessageFrameOut>? handler)
  {
    if(handler == null)
    {
      _commandHandlers.Remove(messageCode);
    }
    else
    {
      _commandHandlers[messageCode] = handler;
    }
  }

  private void HandleKeepAlive(MessageFrameIn frameIn, MessageFrameOut frameOut)
  {
    frameOut.Clear().AppendI32(MessageCodes.KeepAlive);
  }
}
