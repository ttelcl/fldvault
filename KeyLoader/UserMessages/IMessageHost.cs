using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLoader.UserMessages;

/// <summary>
/// Implemented by a UI element that can show message pseudo-dialogs
/// and can show a status message.
/// </summary>
public interface IMessageHost
{

  /// <summary>
  /// Show the given message, or hide the current one if <paramref name="message"/>
  /// is <see langword="null"/>. If another message is showing, it is replaced.
  /// This is not modal, but it can be assumed that passing a value other
  /// than <see langword="null"/> will block the usual UI.
  /// </summary>
  /// <param name="message">
  /// The message to show
  /// </param>
  void ShowMessage(UserMessage? message);

  /// <summary>
  /// Return the severity of the currently showing message, or return null if no message is currently showing
  /// </summary>
  /// <returns></returns>
  MessageSeverity? CurrentMessageSeverity();

  /// <summary>
  /// Change the current status message to <paramref name="status"/>.
  /// After <paramref name="duration"/> automatically clear the message
  /// (if not <see langword="null"/>)
  /// </summary>
  /// <param name="status">
  /// The new status message to set, or <see langword="null"/> or an empty
  /// string to clear the status.
  /// </param>
  /// <param name="duration">
  /// The maximum duration to show the status. If <see langword="null"/>,
  /// there is no automatic clearing of the message.
  /// </param>
  void SetStatus(string? status, TimeSpan? duration = null);

  /// <summary>
  /// If the current status exactly matches <paramref name="status"/>, then clear
  /// it immediately. Otherwise it is assumed the status already changed and should
  /// not be affected by the caller.
  /// </summary>
  /// <param name="status"></param>
  void UnsetStatus(string status);
}

/// <summary>
/// Extension methods on <see cref="IMessageHost"/>.
/// </summary>
public static class MessageHostExtensions
{
  /// <summary>
  /// Show an informational message (possibly replacing the current message)
  /// </summary>
  /// <param name="host">
  /// The <see cref="IMessageHost"/> to show the message on
  /// </param>
  /// <param name="message">
  /// The message to show.
  /// </param>
  /// <param name="title">
  /// The title for the message frame, or <see langword="null"/> to derive a default.
  /// </param>
  public static void ShowInfo(this IMessageHost host, string message, string? title = null)
  {
    host.ShowMessage(new UserMessage(MessageSeverity.Information, message, title));
  }

  /// <summary>
  /// Show a warning message (possibly replacing the current message)
  /// </summary>
  /// <param name="host">
  /// The <see cref="IMessageHost"/> to show the message on
  /// </param>
  /// <param name="message">
  /// The message to show.
  /// </param>
  /// <param name="title">
  /// The title for the message frame, or <see langword="null"/> to derive a default.
  /// </param>
  public static void ShowWarning(this IMessageHost host, string message, string? title = null)
  {
    host.ShowMessage(new UserMessage(MessageSeverity.Warning, message, title));
  }

  /// <summary>
  /// Show an error message (possibly replacing the current message)
  /// </summary>
  /// <param name="host">
  /// The <see cref="IMessageHost"/> to show the message on
  /// </param>
  /// <param name="message">
  /// The message to show.
  /// </param>
  /// <param name="title">
  /// The title for the message frame, or <see langword="null"/> to derive a default.
  /// </param>
  public static void ShowError(this IMessageHost host, string message, string? title = null)
  {
    host.ShowMessage(new UserMessage(MessageSeverity.Error, message, title));
  }

  /// <summary>
  /// Clear the message currently being shown
  /// </summary>
  /// <param name="host"></param>
  public static void ClearMessage(this IMessageHost host)
  {
    host.ShowMessage(null);
  }
}
