using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace KeyLoader.Converters;

/// <summary>
/// Implements a converter returning a Visibility based
/// on whether the argument matches the converter parameter
/// after conversion to string
/// </summary>
public class StringMatchVisibilityConverter: IValueConverter
{
  /// <summary>
  /// The visibility to return when the value matches the parameter
  /// </summary>
  public Visibility MatchValue { get; set; } = Visibility.Visible;

  /// <summary>
  /// The visibility to return when the value does not match the parameter
  /// </summary>
  public Visibility MismatchValue { get; set; } = Visibility.Collapsed;

  /// <inheritdoc/>
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(targetType == typeof(Visibility) && value != null)
    {
      if(parameter is string p)
      {
        return p == value.ToString() ? MatchValue : MismatchValue;
      }
      else
      {
        return parameter == value ? MatchValue : MismatchValue;
      }
    }
    return MismatchValue;
  }

  /// <inheritdoc/>
  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
