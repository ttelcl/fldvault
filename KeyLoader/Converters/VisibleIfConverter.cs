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

namespace KeyLoader.Converters;

/// <summary>
/// Converter that returns Visibility.Visible if the argument is true.
/// The return values can be set with the MatchValue and MismatchValue
/// properties.
/// </summary>
public class VisibleIfConverter: IValueConverter
{
  /// <summary>
  /// The visibility to return when the value is true
  /// </summary>
  public Visibility MatchValue { get; set; } = Visibility.Visible;

  /// <summary>
  /// The visibility to return when the value is false
  /// </summary>
  public Visibility MismatchValue { get; set; } = Visibility.Collapsed;

  /// <inheritdoc/>
  public object Convert(
    object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(targetType == typeof(Visibility) && value != null)
    {
      return (bool)value ? MatchValue : MismatchValue;
    }
    return MismatchValue;
  }

  /// <inheritdoc/>
  public object ConvertBack(
    object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
