using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FldVault.Core.Utilities;

/// <summary>
/// Utility for converting between DateTime and 64-bit values
/// representing epoch-ticks: the number of 100 nanosecond intervals
/// since the Unix Epoch
/// </summary>
public static class EpochTicks
{
  /// <summary>
  /// Offset between .net ticks and epoch ticks:
  /// The number of .net ticks at 1970-01-01 00:00:00 UTC
  /// </summary>
  public const long Offset = 0x089F7FF5F7B58000L;

  /// <summary>
  /// Convert a UTC DateTime to epoch ticks
  /// </summary>
  /// <param name="utcTime">
  /// The time to convert. This value must have a <see cref="DateTime.Kind"/>
  /// of <see cref="DateTimeKind.Utc"/>.
  /// </param>
  /// <returns>
  /// The equivalent in epoch ticks
  /// </returns>
  /// <exception cref="ArgumentException">
  /// Thrown if the argument is a local or unspecified time instead of a UTC time.
  /// </exception>
  public static long FromUtc(DateTime utcTime)
  {
    if(utcTime.Kind != DateTimeKind.Utc)
    {
      throw new ArgumentException("Expecting a UTC time");
    }
    return utcTime.Ticks - Offset;
  }

  /// <summary>
  /// Returns the UTC DateTime corresponding to the given
  /// epoch ticks value.
  /// </summary>
  public static DateTime ToUtc(long epochTicks)
  {
    return new DateTime(epochTicks + Offset, DateTimeKind.Utc);
  }
}
