/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Data;
using System.Globalization;
using System.Windows;

namespace ZvaultViewerEditor.Converters;

/// <summary>
/// Convert null or not-null to Visibility
/// </summary>
public class NullVisibilityConverter: IValueConverter
{

  public Visibility NullValue { get; set; } = Visibility.Collapsed;

  public Visibility NotNullValue { get; set; } = Visibility.Visible;

  public object Convert(
    object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(targetType == typeof(Visibility))
    {
      return value == null ? NullValue : NotNullValue;
    }
    return NullValue;
  }

  public object ConvertBack(
    object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
