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
using System.Windows.Shapes;

using MahApps.Metro.Controls;

namespace KeyLoader.Main;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow: MetroWindow
{
  /// <summary>
  /// Create the <see cref="MainWindow"/>.
  /// In this application this is called manually by the bootstrapping code in
  /// App.xaml.cs (not by the WPF framework as 'Startup URI').
  /// </summary>
  public MainWindow()
  {
    InitializeComponent();
  }
}
