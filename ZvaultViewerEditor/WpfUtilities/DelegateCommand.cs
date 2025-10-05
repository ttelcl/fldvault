/*
 * (c) 2019   / TTELCL
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ZvaultViewerEditor.WpfUtilities;

/// <summary>
/// Standard 'DelegateCommand' implementation
/// </summary>
public class DelegateCommand: ICommand
{
  private readonly Predicate<object?>? _canExecute;
  private readonly Action<object?> _execute;

  /// <summary>
  /// Create a <see cref="DelegateCommand"/> whose executability
  /// is variable
  /// </summary>
  /// <param name="execute">
  /// The delegate to execute when the command is executed. The argument
  /// is the command parameter,
  /// </param>
  /// <param name="canExecute">
  /// Optional: the delegate executed to determine if the command can
  /// be executed (putting the command into a disabled state if returning
  /// false). If not given, the command is always enabled.
  /// </param>
  public DelegateCommand(
    Action<object?> execute,
    Predicate<object?>? canExecute = null)
  {
    _execute = execute;
    _canExecute = canExecute;
  }

  /// <summary>
  /// Test if the command can be executed
  /// </summary>
  public bool CanExecute(object? parameter)
  {
    return _canExecute == null || _canExecute(parameter);
  }

  /// <summary>
  /// Execute the command
  /// </summary>
  public void Execute(object? parameter)
  {
    _execute(parameter);
  }

  /// <summary>
  /// Attaches the event to <see cref="CommandManager.RequerySuggested"/>
  /// </summary>
  public event EventHandler? CanExecuteChanged {
    add {
      CommandManager.RequerySuggested += value;
    }
    remove {
      CommandManager.RequerySuggested -= value;
    }
  }

}
