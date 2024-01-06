using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using FldVault.Upi;

using MahApps.Metro.Controls;

namespace ZvaultKeyServer.Main;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow: MetroWindow
{
  public MainWindow()
  {
    InitializeComponent();
  }

  protected override void OnClosing(CancelEventArgs e)
  {
    if(DataContext is MainViewModel mainViewModel)
    {
      var cancel = mainViewModel.KeyStates.AnyKeys && (MessageBox.Show(
        "Are you sure you want to terminate the key server?\nThis will discard all keys from memory.",
        "Confirmation",
        MessageBoxButton.OKCancel,
        MessageBoxImage.Warning) != MessageBoxResult.OK);
      if(cancel)
      {
        e.Cancel = true;
      }
      else
      {
        mainViewModel.OnClosing(e);
      }
    }
    base.OnClosing(e);
  }

  private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
  {
    if(DataContext is MainViewModel mainViewModel)
    {
      // Auto-start if possible
      if(mainViewModel.ServerStatus == ServerStatus.CanStart)
      {
        mainViewModel.StartServer();
      }
    }
  }

  private void ZvaultServer_Drop(object sender, DragEventArgs e)
  {
    if(e.Data.GetDataPresent(DataFormats.FileDrop)
      && DataContext is MainViewModel mvm
      && mvm.KeysViewModel != null)
    {
      var kvm = mvm.KeysViewModel;
      var files = (string[])e.Data.GetData(DataFormats.FileDrop);
      foreach(var file in files)
      {
        kvm.LinkFile(file);
      }
      kvm.SyncModel();
    }
  }
}
