using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace KeyLoader.Converters;

/// <summary>
/// Caches conversions from color strings to brushes
/// </summary>
public class BrushCache
{
  private readonly Dictionary<string, Brush> _colorCache;
  private readonly BrushConverter _colorConverter;

  /// <summary>
  /// Create a new BrushCache
  /// </summary>
  public BrushCache()
  {
    _colorCache = new Dictionary<string, Brush>();
    _colorConverter = new BrushConverter();
    DefaultColor = BrushForColor("#CCFF0000");
  }

  /// <summary>
  /// Returns the brush for the color, either created newly or
  /// from a cache. Supports the syntaxes supported by
  /// <see cref="BrushConverter"/> for <see cref="SolidColorBrush"/>.
  /// </summary>
  public Brush BrushForColor(string colorText)
  {
    if(!_colorCache.TryGetValue(colorText, out var color))
    {
      color = (Brush)_colorConverter.ConvertFrom(colorText)!;
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
  public Brush BrushOrDefault(string colorText)
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

  /// <summary>
  /// Returns the brush for the given name if known, null otherwise.
  /// </summary>
  /// <param name="colorText"></param>
  /// <returns></returns>
  public Brush? KnownColor(string colorText)
  {
    return _colorCache.TryGetValue(colorText, out var color) ? color : null;
  }

  /// <summary>
  /// Returns the brush for the color. This indexer is equivalent to
  /// <see cref="BrushForColor(string)"/>. You can add non-standard colors
  /// using <see cref="AddAlias(string, Brush)"/> or <see cref="AddAlias(string, string)"/>
  /// </summary>
  public Brush this[string colorText] {
    get => BrushForColor(colorText);
  }

  /// <summary>
  /// The default color. Settable, Initial value is a bright slightly transparent red.
  /// </summary>
  public Brush DefaultColor { get; set; }

  /// <summary>
  /// Default (singleton-like) <see cref="BrushCache"/> instance
  /// </summary>
  public static BrushCache Default { get; } = new BrushCache();

  /// <summary>
  /// Set the brush for <paramref name="alias"/> to <paramref name="brush"/>
  /// (potentially overwriting an existing instance). This is the only way to
  /// insert brushes that are not <see cref="SolidColorBrush"/>.
  /// </summary>
  /// <param name="alias"></param>
  /// <param name="brush"></param>
  public void AddAlias(string alias, Brush brush)
  {
    _colorCache[alias] = brush;
  }

  /// <summary>
  /// Set the brush for <paramref name="alias"/> to the brush that 
  /// <see cref="BrushForColor(string)"/> would return for <paramref name="colorText"/>
  /// (aliasing an existing brush or interpreting a new brush)
  /// </summary>
  /// <param name="alias"></param>
  /// <param name="colorText"></param>
  public void AddAlias(string alias, string colorText)
  {
    AddAlias(alias, BrushForColor(colorText));
  }
}
