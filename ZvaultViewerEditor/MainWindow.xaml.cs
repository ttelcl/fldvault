﻿using System.Diagnostics;
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

using MahApps.Metro.Controls;

using ZvaultViewerEditor.Main;

namespace ZvaultViewerEditor;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow: MetroWindow
{
  public MainWindow()
  {
    InitializeComponent();
  }

  private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
  {
    if(DataContext is MainViewModel mainViewModel)
    {
      Trace.TraceInformation(
        "MainWindow.MetroWindow_Loaded: Not yet handled");
    }
  }

}
