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

public class StringBrushConverter: IValueConverter
{
  public StringBrushConverter(
    BrushCache? cache)
  {
    Cache = cache ?? BrushCache.Default;
  }

  public StringBrushConverter()
    : this(BrushCache.Default)
  {
  }

  public BrushCache Cache { get; set; }

  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(targetType.IsAssignableFrom(typeof(SolidColorBrush)) && value is string s)
    {
      return Cache.BrushOrDefault(s);
    }
    else
    {
      return Cache["#FF44CC"];
    }
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
