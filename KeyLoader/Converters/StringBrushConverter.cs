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
/// An <see cref="IValueConverter"/> that converts strings to <see cref="Brush"/> using
/// a <see cref="BrushCache"/>.
/// </summary>
public class StringBrushConverter: IValueConverter
{
  /// <summary>
  /// Create a <see cref="StringBrushConverter"/> using <paramref name="cache"/>
  /// as the brush cache, or <see cref="BrushCache.Default"/> if it is
  /// <see langword="null"/>.
  /// </summary>
  /// <param name="cache"></param>
  public StringBrushConverter(
    BrushCache? cache)
  {
    Cache = cache ?? BrushCache.Default;
  }

  /// <summary>
  /// Create a <see cref="StringBrushConverter"/> using the default
  /// brush cache <see cref="BrushCache.Default"/>.
  /// </summary>
  public StringBrushConverter()
    : this(BrushCache.Default)
  {
  }

  /// <summary>
  /// The <see cref="BrushCache"/> this converter uses.
  /// By default this is <see cref="BrushCache.Default"/>,
  /// </summary>
  public BrushCache Cache { get; set; }

  /// <inheritdoc/>
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    if(targetType.IsAssignableFrom(typeof(Brush)) && value is string s)
    {
      return Cache.BrushOrDefault(s);
    }
    else
    {
      return Binding.DoNothing;
    }
  }

  /// <summary>
  /// Not supported
  /// </summary>
  /// <exception cref="NotSupportedException"></exception>
  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}
