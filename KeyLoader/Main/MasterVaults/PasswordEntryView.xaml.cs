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

namespace KeyLoader.Main.MasterVaults;
/// <summary>
/// Interaction logic for PasswordEntryView.xaml
/// </summary>
public partial class PasswordEntryView: UserControl
{
  /// <summary>
  /// Create a new <see cref="PasswordEntryView"/>
  /// </summary>
  public PasswordEntryView()
  {
    InitializeComponent();
  }

  private void Pass_DataContextChanged(
    object sender, DependencyPropertyChangedEventArgs e)
  {
    if(sender is PasswordBox pwb)
    {
      if(e.OldValue is PasswordEntryTaskViewModel petvmOld)
      {
        petvmOld.Disconnect();
      }
      if(pwb.DataContext is PasswordEntryTaskViewModel petvm)
      {
        petvm.Connect(pwb);
      }
      else if(pwb.DataContext == null)
      {
        Trace.TraceInformation("PWB detached");
      }
      else
      {
        Trace.TraceError("Failed to bind PWB: type error");
      }
    }
    else
    {
      Trace.TraceError("Failed to bind PWB");
    }
  }

  private void Pass_PasswordChanged(
    object sender, RoutedEventArgs e)
  {
    if(sender is PasswordBox pwb &&
      pwb.DataContext is PasswordEntryTaskViewModel petvm)
    {
      petvm.OnPassphraseChanged(pwb);
    }
  }

  private void Pass_KeyDown(object sender, KeyEventArgs e)
  {
    if(sender is PasswordBox pwb && pwb.DataContext is PasswordEntryTaskViewModel petvm)
    {
      if(e.Key == Key.Enter)
      {
        petvm.Submit(false);
        e.Handled = true;
      }
      if(e.Key == Key.Escape)
      {
        petvm.Submit(true);
        e.Handled = true;
      }
    }
  }


}
