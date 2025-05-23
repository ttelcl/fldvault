﻿/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Microsoft.Win32;

using FldVault.Core.Crypto;
using FldVault.Core.KeyResolution;
using FldVault.Core.Vaults;

using ZvaultKeyServer.WpfUtilities;
using System.IO;

namespace ZvaultKeyServer.Main.Keys;

public class NewKeyViewModel: ViewModelBase
{
  private PasswordBox? _passwordBoxPrimary;
  private PasswordBox? _passwordBoxVerify;

  public NewKeyViewModel(
    KeysViewModel owner)
  {
    Owner = owner;
    CancelCommand = new DelegateCommand(p => Cancel());
    SubmitCommand = new DelegateCommand(p => Submit());
  }

  public ICommand CancelCommand { get; }

  public ICommand SubmitCommand { get; }

  public void Cancel()
  {
    if(Owner.NewKeyPane == this)
    {
      Trace.TraceInformation("New key creation CANCELED");
      ClearPasswords();
      Owner.NewKeyPane = null;
    }
  }

  public void Submit()
  {
    if(Owner.NewKeyPane == this && _passwordBoxPrimary!=null && _passwordBoxVerify!=null)
    {
      using(var ss1 = _passwordBoxPrimary.SecurePassword)
      using(var ss2 = _passwordBoxVerify.SecurePassword)
      {
        if(ss1.Length != ss2.Length)
        {
          MessageBox.Show(
            "The passphrases do not match (they have different lengths)",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
          return;
        }
        if(ss1.Length < 10)
        {
          // This limit is too low, really. It is just there to catch the
          // most extreme cases
          MessageBox.Show(
            "The passphrase is too short to be acceptable",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
          return;
        }
        using(var ppk = PassphraseKey.TryNewFromSecureStringPair(ss1, ss2))
        {
          if(ppk == null)
          {
            MessageBox.Show(
              "The passphrases do no match",
              "Error",
              MessageBoxButton.OK,
              MessageBoxImage.Error);
            return;
          }
          var pkif = new PassphraseKeyInfoFile(ppk);
          Owner.Model.KeyChain.PutCopy(ppk);
          var keystate = Owner.Model.GetKey(pkif.KeyId);
          var m = Owner.FindKey(pkif.KeyId);
          var seed = new PassphraseKeySeed2(pkif);
          keystate.SetSeed(seed);
          ClearPasswords();
          Owner.NewKeyPane = null;
          Owner.SyncModel();
          Owner.TrySelectKey(pkif.KeyId);
          Owner.StatusHost.StatusMessage = $"Created new key {pkif.KeyId}";
          var zkey = pkif.ToZkey();
          var zkeyfilename = $"{pkif.KeyId}.zkey";
          var response = MessageBox.Show(
            $"Do you want to save the key to {zkeyfilename}?",
            "Save key",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
          if(response == MessageBoxResult.Yes)
          {
            var sfd = new SaveFileDialog {
              FileName = zkeyfilename,
              Filter = "ZVault key info files (*.zkey)|*.zkey",
              DefaultExt = ".zkey",
              AddExtension = false,
              OverwritePrompt = true,
            };
            if(sfd.ShowDialog() == true)
            {
              var json = zkey.ToString(true);
              File.WriteAllText(sfd.FileName, json);
              Owner.StatusHost.StatusMessage =
                $"Key {pkif.KeyId} saved to {sfd.FileName}";
            }
          }
        }
      }
    }
  }

  public KeysViewModel Owner { get; }

  public void BindPrimary(PasswordBox pwb)
  {
    _passwordBoxPrimary = pwb;
  }

  public void BindVerify(PasswordBox pwb)
  {
    _passwordBoxVerify = pwb;
  }

  public void ClearPasswords()
  {
    _passwordBoxPrimary?.Clear();
    _passwordBoxVerify?.Clear();
  }

  public void Unbind()
  {
    ClearPasswords();
    _passwordBoxPrimary = null;
    _passwordBoxVerify = null;
  }

}
