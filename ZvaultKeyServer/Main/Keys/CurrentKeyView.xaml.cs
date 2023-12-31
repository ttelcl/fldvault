using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ZvaultKeyServer.Main.Keys
{
  /// <summary>
  /// Interaction logic for CurrentKeyView.xaml
  /// </summary>
  public partial class CurrentKeyView: UserControl
  {
    public CurrentKeyView()
    {
      InitializeComponent();
    }

    private void PasswordBox_Bind(object sender, DependencyPropertyChangedEventArgs e)
    {
      if(sender is PasswordBox pwb && pwb.DataContext is KeysViewModel kvm)
      {
        Trace.TraceInformation("Password control bootstrap succeeded");
        kvm.BindPasswordBox(pwb);
      }
      else
      {
        Trace.TraceError("Password control bootstrap failed");
      }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
      if(sender is PasswordBox pwb && pwb.DataContext is KeysViewModel kvm)
      {
        kvm.SetPasswordBackground(false);
        if(e.Key == Key.Enter)
        {
          kvm.TryUnlockCommand.Execute(null);
        }
      }
    }
  }
}
