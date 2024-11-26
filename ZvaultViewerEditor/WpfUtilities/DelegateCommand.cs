/*
 * (c) 2019   / TTELCL
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Input;

namespace ZvaultViewerEditor.WpfUtilities;

/// <summary>
/// Standard DelegateCommand implementation
/// </summary>
public class DelegateCommand: ICommand
{
  private readonly Predicate<object?>? _canExecute;
  private readonly Action<object?> _execute;

  /// <summary>
  /// Create a DelegateCommand whose executability
  /// is variable
  /// </summary>
  public DelegateCommand(
    Action<object?> execute,
    Predicate<object?>? canExecute = null)
  {
    _execute = execute;
    _canExecute = canExecute;
  }

  /// <summary>
  /// Test if the commend can be executed
  /// </summary>
  public bool CanExecute(object? parameter)
  {
    return _canExecute == null || _canExecute(parameter);
  }

  /// <summary>
  /// Execute the commend
  /// </summary>
  public void Execute(object? parameter)
  {
    _execute(parameter);
  }

  /// <summary>
  /// Attaches the event to CommandManager.RequerySuggested
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
