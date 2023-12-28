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
      mainViewModel.OnClosing(e);
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
}
