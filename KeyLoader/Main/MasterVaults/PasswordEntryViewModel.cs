using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

using CommunityToolkit.Mvvm.ComponentModel;

namespace KeyLoader.Main.MasterVaults;

/// <summary>
/// A ViewModel for a password / passphrase entry widget
/// </summary>
public class PasswordEntryViewModel: ObservableObject
{

  /// <summary>
  /// Create a new PasswordEntryViewModel
  /// </summary>
  public PasswordEntryViewModel()
  {
  }

  /// <summary>
  /// Create a new <see cref="PasswordEntryViewModel"/> and set up a
  /// <see cref="PasswordTask"/> using the given <paramref name="submitAction"/>.
  /// </summary>
  /// <param name="submitAction"></param>
  /// <param name="supportCancel"></param>
  public PasswordEntryViewModel(Action<SecureString?> submitAction, bool supportCancel)
    : this()
  {
    PasswordTask = new PasswordEntryTaskViewModel(submitAction, supportCancel);
  }

  /// <summary>
  /// The currently active password task
  /// </summary>
  public PasswordEntryTaskViewModel? PasswordTask {
    get => _passwordTask;
    set {
      if(_passwordTask != value)
      {
        _passwordTask?.Disconnect();
        _passwordTask = value;
        // The intention is that this next OnPropertyChanged causes binding to set this
        // as a PasswordBox' DataContext which in turn connects the PasswordBox to the
        // PasswordEntryTaskViewModel
        OnPropertyChanged();
        HasTask = _passwordTask != null;
      }
    }
  }
  private PasswordEntryTaskViewModel? _passwordTask = null;

  /// <summary>
  /// True if this model has a pending task
  /// </summary>
  public bool HasTask {
    get => _hasTask;
    private set {
      SetProperty(ref _hasTask, value);
    }
  }
  private bool _hasTask;
}
