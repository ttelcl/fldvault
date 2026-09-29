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
/// Choices for the severity of a <see cref="UserMessage"/>
/// </summary>
public enum MessageSeverity
{
  /// <summary>
  /// The message is purely informational
  /// </summary>
  Information,

  /// <summary>
  /// The message is a warning
  /// </summary>
  Warning,

  /// <summary>
  /// The message is an error message
  /// </summary>
  Error,
}
