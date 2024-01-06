/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

using FldVault.Upi;

namespace ZvaultKeyServer.Converters;

/// <summary>
/// Implements a converter returning a Visibility based
/// on whether the argument matches the converter parameter
/// after conversion to string
/// </summary>
public class StringMatchVisibilityConverter: IValueConverter
{
  public Visibility MatchValue {  get; set; } = Visibility.Visible;

  public Visibility MismatchValue { get; set; } = Visibility.Collapsed;

  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(targetType == typeof(Visibility) && value != null)
    {
      if(parameter is String p)
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

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
