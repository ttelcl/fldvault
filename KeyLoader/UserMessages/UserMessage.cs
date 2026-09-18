/*
 * (c) 2026  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLoader.UserMessages;

/// <summary>
/// Carries information describing a message to the user, similar to
/// what a message dialog can show. The message is immutable, so no need
/// to make this a ViewModel.
/// </summary>
public class UserMessage
{
  /// <summary>
  /// Create a new <see cref="UserMessage"/>.
  /// </summary>
  /// <param name="severity">
  /// The severity of the message, which may affect styling and color scheme,
  /// as wel as the default for <paramref name="title"/>
  /// </param>
  /// <param name="message">
  /// The message to show
  /// </param>
  /// <param name="title">
  /// The title for the message box, or <see langword="null"/> to derive one from
  /// <paramref name="severity"/>.
  /// Note that an empty string is a valid title (no title) and is different from
  /// <see langword="null"/>.
  /// </param>
  public UserMessage(MessageSeverity severity, string message, string? title = null)
  {
    Message = message;
    Severity = severity;
    Title =
      title ?? severity switch {
        MessageSeverity.Information => "Message",
        MessageSeverity.Warning => "Warning",
        MessageSeverity.Error => "Error",
        _ => "?"
      };
  }

  /// <summary>
  /// The message to display
  /// </summary>
  public string Message { get; }

  /// <summary>
  /// The title of the message "box"
  /// </summary>
  public string Title { get; }

  /// <summary>
  /// The severity of the message (affecting coloration and the default title)
  /// </summary>
  public MessageSeverity Severity { get; }

}
