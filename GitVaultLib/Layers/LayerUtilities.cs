using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GitVaultLib.Layers;

/// <summary>
/// Utilities related to layer bundles
/// </summary>
public static class LayerUtilities
{
  private static readonly Regex __layerNameRegex = new Regex(@"^[a-zA-Z0-9]+(-[a-zA-Z0-9]+)*$");
  private static readonly Regex __hexRegex = new Regex(@"^[a-fA-F0-9]+$");

  /// <summary>
  /// Check if <paramref name="layerName"/> is a valid layer name.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Valid layer names are one or more blocks separated by '-', each of which only contains
  /// just letters and numbers, with the exception of single-block groups only containing
  /// hexadecimal characters.
  /// </para>
  /// <para>
  /// Examples of valid layer names are "foobar", "foo-bar", "foo-bar-baz", "F00bar", "0000-0000",
  /// "2026-10-07", "20261007-155959", "deadbeef-c0ffee".
  /// </para>
  /// <para>
  /// Examples of invalid layer names are "foobar!@#" (invalid characters), "foo.bar" (invalid character '.'),
  /// "-foobar" ('-' can only appear as separator), "foobar-" (same), "foo--bar" (blocks between '-' cannot be empty),
  /// "deadbeef" (valid hexadecimal strings are excluded), "c0ffee" (same), "20261007" (same).
  /// </para>
  /// </remarks>
  /// <param name="layerName"></param>
  /// <returns></returns>
  public static bool IsValidLayerName(string layerName)
  {
    return __layerNameRegex.IsMatch(layerName) && !__hexRegex.IsMatch(layerName);
  }
}
