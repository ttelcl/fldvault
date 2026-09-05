using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KeyLoader.Main.MasterVaults;

/// <summary>
/// A viewmodel representing a one-time passphrase entry request.
/// This will be set as data context for a <see cref="PasswordBox"/> and
/// thrown away after the entry is complete and the password has been
/// consumed.
/// </summary>
public class PasswordEntryTaskViewModel: ObservableObject
{
  /// <summary>
  /// The action that will be executed after submitting the password / passphrase
  /// (or cancelling it)
  /// </summary>
  private readonly Action<SecureString?> _submitAction;
  private PasswordBox? _passwordBox = null;

  /// <summary>
  /// Create a new <see cref="PasswordEntryTaskViewModel"/>.
  /// </summary>
  /// <param name="submitAction">
  /// The <see cref="Action"/> that is called when submitting the password
  /// or cancelling it. Cancellation is indicated by calling this action
  /// with argument <see langword="null"/>. Upon accepting the submission,
  /// the action should ensure that this <see cref="PasswordEntryTaskViewModel"/>
  /// is removed as <see cref="FrameworkElement.DataContext"/> of
  /// the <see cref="PasswordBox"/>.
  /// </param>
  /// <param name="supportCancel"></param>
  public PasswordEntryTaskViewModel(
    Action<SecureString?> submitAction, bool supportCancel)
  {
    _submitAction = submitAction;
    SubmitCommand = new RelayCommand(() => Submit(false), () => _passwordBox != null);
    CancelCommand = new RelayCommand(() => Submit(true));
    SupportCancel = supportCancel;
  }

  /// <summary>
  /// Submits the current value of the connected <see cref="PasswordBox"/> to
  /// the callback passed to the constructor
  /// </summary>
  public ICommand SubmitCommand { get; }

  /// <summary>
  /// Submits <see langword="null"/> to the callback passed to the constructor
  /// </summary>
  public ICommand CancelCommand { get; }

  /// <summary>
  /// Whether or not to show a "Cancel" button
  /// </summary>
  public bool SupportCancel {
    get => _supportCancel;
    set {
      SetProperty(ref _supportCancel, value);
    }
  }
  private bool _supportCancel;

  internal void Disconnect()
  {
    _passwordBox?.Clear();
    _passwordBox = null;
  }

  /// <summary>
  /// Callback from the <see cref="PasswordBox"/> control when this task is set as
  /// its DataContext.
  /// </summary>
  internal void Connect(PasswordBox pwb)
  {
    _passwordBox = pwb;
    _passwordBox?.Clear();
    if(_pendingFocus)
    {
      TryFocus();
    }
  }

  /// <summary>
  /// Try to focus on this entry's <see cref="PasswordBox"/>
  /// </summary>
  public void TryFocus()
  {
    if(_passwordBox == null)
    {
      _pendingFocus = true;
    }
    else
    {
      _passwordBox.Focus(); 
      _pendingFocus = false;
    }
  }

  private bool _pendingFocus;

  internal void OnPassphraseChanged(PasswordBox pwb)
  {
  }

  internal void Submit(bool cancel)
  {
    if(_passwordBox == null)
    {
      Trace.TraceError(
        "Getting a password submission while already disconnected. Ignoring!");
    }
    else
    {
      if(cancel)
      {
        Trace.TraceInformation("Password submission cancelled");
        _submitAction(null);
      }
      else
      {
        // Yes, a "using" block. PasswordBox.SecurePassword is returning a
        // *copy* of the password, so if we don't dispose it, it would be hanging around
        // until garbage collected.
        using(var passphrase = _passwordBox.SecurePassword)
        {
          // TODO: remove this log
          Trace.TraceInformation($"Submitting a {passphrase.Length} character passphrase");
          _submitAction(passphrase);
        }
      }
    }
  }

}
