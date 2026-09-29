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

namespace KeyLoader.Main.TaskTab;

/// <summary>
/// Interaction logic for TabView.xaml
/// </summary>
public partial class TabView: UserControl
{
  /// <summary>
  /// Create a new <see cref="TabView"/>
  /// </summary>
  public TabView()
  {
    InitializeComponent();
  }

  private Point? _clickPosition = null;

  private void TabView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
  {
    if(DataContext is TaskTabBaseViewModel tab && !tab.IsActive)
    {
      tab.IsClicking = true;
    }
    if(CaptureMouse())
    {
      _clickPosition = Mouse.GetPosition(this);
    }
    e.Handled = true;
  }

  private void TabView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
  {
    ReleaseMouseCapture();
    var wasClicking = false;
    var clicked = false;
    if(DataContext is TaskTabBaseViewModel tab)
    {
      wasClicking = tab.IsClicking;
      tab.IsClicking = false;
      if(_clickPosition.HasValue && wasClicking)
      {
        var downPosition = _clickPosition.Value;
        var upPosition = Mouse.GetPosition(this);
        var delta = downPosition - upPosition;
        var distance = delta.Length;
        clicked = distance <= 5;
      }
      // reset state BEFORE passing on the click callback
      _clickPosition = null;
      e.Handled = true;
      if(clicked)
      {
        Trace.TraceInformation($"Clicked tab '{tab.Title}'");
        tab.TabClicked();
      }
      else if(wasClicking)
      {
        Trace.TraceWarning("Moved too far to accept click");
      }
    }
    else
    {
      _clickPosition = null;
      e.Handled = true;
    }
  }
}
