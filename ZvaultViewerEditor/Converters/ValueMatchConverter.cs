/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ZvaultViewerEditor.Converters;

/// <summary>
/// Converter to check if a value matches a parameter,
/// returning a boolean. Particularly useful for radio buttons
/// (passing something like "ConverterParameter={x:Static local:MyEnum.FooBar}")
/// </summary>
public class ValueMatchConverter: IValueConverter
{
  public object Convert(
    object value, Type targetType, object parameter, CultureInfo culture)
  {
    return value == null ? parameter==null : value.Equals(parameter);
  }

  public object ConvertBack(
    object value, Type targetType, object parameter, CultureInfo culture)
  {
    // From https://www.codeproject.com/Tips/720497/Binding-Radio-Buttons-to-a-Single-Property
    return value.Equals(true) ? parameter : Binding.DoNothing;
  }
}
