using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLoader.Main.TaskTab;

/// <summary>
/// A message informing recipients of a change in current tab
/// </summary>
public class CurrentTabChangedMessage
{
  /// <summary>
  /// Create a new <see cref="CurrentTabChangedMessage"/> instance
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="newTab"></param>
  public CurrentTabChangedMessage(
    TabHostViewModel sender,
    TaskTabBaseViewModel? newTab)
  {
    Sender = sender;
    NewTab = newTab;
  }

  /// <summary>
  /// The tab host whose current tab changed
  /// </summary>
  public TabHostViewModel Sender { get; }

  /// <summary>
  /// The new current tab
  /// </summary>
  public TaskTabBaseViewModel? NewTab { get; }
}
