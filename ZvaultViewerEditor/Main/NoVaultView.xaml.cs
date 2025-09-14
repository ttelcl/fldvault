using System;
using System.Collections.Generic;
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

namespace ZvaultViewerEditor.Main;

/// <summary>
/// Interaction logic for NoVaultView.xaml
/// </summary>
public partial class NoVaultView: UserControl
{
  public NoVaultView()
  {
    InitializeComponent();
  }

  protected override void OnDrop(DragEventArgs e)
  {
    if(DataContext is NoVaultViewModel nvvm)
    {
      nvvm.DropEvent(e);
    }
  }

  protected override void OnDragOver(DragEventArgs e)
  {
    if(DataContext is NoVaultViewModel nvvm)
    {
      nvvm.DragOverEvent(e);
    }
  }
}
