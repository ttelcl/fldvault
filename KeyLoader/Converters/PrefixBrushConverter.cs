using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace KeyLoader.Converters;

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

  /// <summary>
  /// The default color. Defaults to the <see cref="BrushCache.DefaultColor"/> for
  /// <see cref="Cache"/>.
  /// </summary>
  public Brush DefaultColor { get; set; }

  /// <summary>
  /// The prefix that this converter uses. Defaults to "/".
  /// </summary>
  public string Prefix { get; set; }

  /// <summary>
  /// The <see cref="BrushCache"/> this converter uses. Defaults to <see cref="BrushCache.Default"/>.
  /// </summary>
  public BrushCache Cache { get; set; }

  /// <summary>
  /// Converts the value to a <see cref="Brush"/> by first converting it to a string,
  /// then prefixing it with <see cref="Prefix"/>, then looking it up in <see cref="Cache"/>.
  /// If the value already exists in the cache it is returned. Otherwise
  /// <see cref="DefaultColor"/> is returned.
  /// </summary>
  /// <param name="value"></param>
  /// <param name="targetType"></param>
  /// <param name="parameter"></param>
  /// <param name="culture"></param>
  /// <returns></returns>
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(targetType.IsAssignableFrom(typeof(Brush)) && value != null)
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
      return Binding.DoNothing;
    }
  }

  /// <summary>
  /// Not supported.
  /// </summary>
  /// <exception cref="NotSupportedException"></exception>
  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
