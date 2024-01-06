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
using System.Windows.Data;
using System.Windows.Media;

namespace ZvaultKeyServer.Converters;

/// <summary>
/// Converts input values to brushes via a prefixed lookup
/// in the default brush cache.
/// </summary>
public class PrefixBrushConverter: IValueConverter
{
  /// <summary>
  /// Create a new PrefixBrushConverter
  /// </summary>
  public PrefixBrushConverter()
  {
    Cache = BrushCache.Default;
    DefaultColor = Cache.DefaultColor;
    Prefix = "/";
  }

  public SolidColorBrush DefaultColor { get; set; }

  public string Prefix { get; set; }

  public BrushCache Cache { get; set; }

  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(targetType.IsAssignableFrom(typeof(SolidColorBrush)) && value != null)
    {
      var key = Prefix+value.ToString();
      var color = Cache.KnownColor(key);
      if(color == null)
      {
        Trace.TraceError($"Unregistered color: '{key}'");
      }
      return color ?? DefaultColor;
    }
    else
    {
      Trace.TraceError(
        $"Color conversion error. Target type is {targetType.Name}. " +
        $"Value = '{value?.ToString()??String.Empty}'");
      return DefaultColor;
    }
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
