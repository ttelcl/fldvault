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
/// Converter that returns Visibility.Visible if the argument is true.
/// The return values can be set with the MatchValue and MismatchValue
/// properties.
/// </summary>
public class VisibleIfConverter: IValueConverter
{
  public Visibility MatchValue { get; set; } = Visibility.Visible;

  public Visibility MismatchValue { get; set; } = Visibility.Collapsed;

  public object Convert(
    object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(targetType == typeof(Visibility) && value != null)
    {
      return (bool)value ? MatchValue : MismatchValue;
    }
    return MismatchValue;
  }

  public object ConvertBack(
    object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
