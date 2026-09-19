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

using ControlzEx.Theming;

using KeyLoader.UserMessages;

namespace KeyLoader.Main;

/// <summary>
/// Interaction logic for MessageView.xaml
/// </summary>
public partial class MessageView: UserControl
{
  /// <summary>
  /// Create the <see cref="MessageView"/>
  /// </summary>
  public MessageView()
  {
    InitializeComponent();
  }

  private void MessageView_DataContextChanged(
    object sender, DependencyPropertyChangedEventArgs e)
  {
    UpdateTheme(DataContext);
  }

  private void UpdateTheme(object? vm)
  {
    if(vm is MainViewModel main && main.CurrentMessage != null)
    {
      var themeName = main.CurrentMessage.Severity switch {
        MessageSeverity.Information => "Dark.Emerald",
        MessageSeverity.Warning => "Dark.Amber",
        MessageSeverity.Error => "Dark.Crimson",
        _ => "Dark.Cyan"
      };
      Trace.TraceInformation($"Setting message theme to '{themeName}'");
      ThemeManager.Current.ChangeTheme(this, themeName);
    }
    else
    {
      Trace.TraceInformation($"Resetting message theme");
      ThemeManager.Current.ChangeTheme(this, "Dark.Steel");
    }
  }

  private void MessageView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
  {
    UpdateTheme(DataContext);
  }
}
