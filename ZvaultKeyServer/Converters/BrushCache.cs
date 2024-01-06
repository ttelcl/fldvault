/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ZvaultKeyServer.Converters;

/// <summary>
/// Caches conversions from color strings to brushes
/// </summary>
public class BrushCache
{
  private readonly Dictionary<string, SolidColorBrush> _colorCache;
  private readonly BrushConverter _colorConverter;

  /// <summary>
  /// Create a new BrushCache
  /// </summary>
  public BrushCache()
  {
    _colorCache = new Dictionary<string, SolidColorBrush>();
    _colorConverter = new BrushConverter();
    DefaultColor = BrushForColor("#CCFF0000");
  }

  /// <summary>
  /// Returns the brush for the color, either created newly or
  /// from a cache. Supports the syntaxes supported by
  /// <see cref="BrushConverter"/> for <see cref="SolidColorBrush"/>.
  /// </summary>
  public SolidColorBrush BrushForColor(string colorText)
  {
    if(!_colorCache.TryGetValue(colorText, out var color))
    {
      color = (SolidColorBrush)_colorConverter.ConvertFrom(colorText)!;
      color.Freeze();
      _colorCache[colorText] = color;
    }
    return color;
  }

  /// <summary>
  /// If not known and the text contains a '/' or a '.',
  /// <see cref="DefaultColor"/> is returned.
  /// Behaves the same as <see cref="BrushForColor(string)"/> otherwise.
  /// </summary>
  public SolidColorBrush BrushOrDefault(string colorText)
  {
    if(!_colorCache.TryGetValue(colorText, out var color))
    {
      if(colorText.Contains('/') || colorText.Contains('.'))
      {
        Trace.TraceWarning($"Color not found '{colorText}' (falling back to default)");
        return DefaultColor;
      }
      return BrushForColor(colorText);
    }
    return color;
  }

  public SolidColorBrush? KnownColor(string colorText)
  {
    return _colorCache.TryGetValue(colorText, out var color) ? color : null;
  }

  /// <summary>
  /// Returns the brush for the color. This indexer is equivalent to
  /// <see cref="BrushForColor(string)"/>
  /// </summary>
  public SolidColorBrush this[string colorText] {
    get => BrushForColor(colorText);
  }

  public SolidColorBrush DefaultColor { get; set; }

  public static BrushCache Default { get; } = new BrushCache();

  public void AddAlias(string alias, SolidColorBrush brush)
  {
    _colorCache[alias] = brush;
  }

  public void AddAlias(string alias, string colorText)
  {
    AddAlias(alias, BrushForColor(colorText));
  }
}
