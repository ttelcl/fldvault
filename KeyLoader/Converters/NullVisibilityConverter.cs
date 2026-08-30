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
/// Convert null or not-null to Visibility
/// </summary>
public class NullVisibilityConverter: IValueConverter
{

  /// <summary>
  /// The visibility to return when the value is null
  /// </summary>
  public Visibility NullValue { get; set; } = Visibility.Collapsed;

  /// <summary>
  /// The visibility to return when the value is not null
  /// </summary>
  public Visibility NotNullValue { get; set; } = Visibility.Visible;

  /// <inheritdoc/>
  public object Convert(
    object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(targetType == typeof(Visibility))
    {
      return value == null ? NullValue : NotNullValue;
    }
    return NullValue;
  }

  /// <inheritdoc/>
  public object ConvertBack(
    object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
