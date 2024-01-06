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
  /// Interaction logic for NewKeyPane.xaml
  /// </summary>
  public partial class NewKeyView: UserControl
  {
    public NewKeyView()
    {
      InitializeComponent();
    }

    private void PasswordBoxPrimary_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
      if(sender is PasswordBox pwb)
      {
        if(pwb.DataContext is NewKeyViewModel nkvm)
        {
          nkvm.BindPrimary(pwb);
        }
        else if(pwb.DataContext == null)
        {
          Trace.TraceInformation("Primary PWB detached");
        }
        else
        {
          Trace.TraceError("Failed to bind primary PWB: type error");
        }
      }
      else
      {
        Trace.TraceError("Failed to bind primary PWB");
      }
    }

    private void PasswordBoxVerify_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
      if(sender is PasswordBox pwb)
      {
        if(pwb.DataContext is NewKeyViewModel nkvm)
        {
          nkvm.BindVerify(pwb);
        }
        else if(pwb.DataContext == null)
        {
          Trace.TraceInformation("Verification PWB detached");
        }
        else
        {
          Trace.TraceError("Failed to bind verification PWB: type error");
        }
      }
      else
      {
        Trace.TraceError("Failed to bind verification PWB");
      }
    }
  }
}
