/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using ZvaultKeyServer.WpfUtilities;

namespace ZvaultKeyServer.Main;

public class MainViewModel: ViewModelBase
{
  public MainViewModel()
  {
  }

  public ICommand ExitCommand { get; } = new DelegateCommand(p => {
    var w = Application.Current.MainWindow;
    w?.Close();
  });

  public string StatusMessage {
    get => _statusMessage;
    set {
      if(SetInstanceProperty(ref _statusMessage, value))
      {
      }
    }
  }
  private string _statusMessage = "Welcome";

}
