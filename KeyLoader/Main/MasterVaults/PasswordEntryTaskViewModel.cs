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
  /// with argument <see langword="null"/>, and can be externally triggered by
  /// calling <see cref="Cancel"/>. Upon accepting the submission,
  /// the action should consider removing this <see cref="PasswordEntryTaskViewModel"/>
  /// as <see cref="FrameworkElement.DataContext"/> of the <see cref="PasswordBox"/>.
  /// </param>
  public PasswordEntryTaskViewModel(
    Action<SecureString?> submitAction)
  {
    _submitAction = submitAction;
    SubmitCommand = new RelayCommand(() => Submit(false), () => _passwordBox != null);
  }

  /// <summary>
  /// Submits the current value of the connected <see cref="PasswordBox"/> to
  /// the callback passed to the constructor
  /// </summary>
  public ICommand SubmitCommand { get; }

  /// <summary>
  /// The watermark to show in the <see cref="PasswordBox"/> while it is still empty
  /// </summary>
  public string WaterMark {
    get => _watermark;
    set {
      SetProperty(ref _watermark, value);
    }
  }
  private string _watermark = "";

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
  /// Cancel passphrase entry: invoke the callback with a <see langword="null"/>
  /// argument, and if that doesn't <see cref="Disconnect"/>, do so.
  /// </summary>
  public void Cancel()
  {
    var oldPwb = _passwordBox;
    Submit(true);
    if(_passwordBox == oldPwb)
    {
      // Otherwise assume that Submit() already caused disconnecting
      Disconnect();
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
