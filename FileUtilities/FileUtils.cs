/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUtilities;

/// <summary>
/// Description of FileUtils
/// </summary>
public static class FileUtils
{
  /// <summary>
  /// Check if the target file is outdated compared to the source file.
  /// If the source file does not exist, the target file is considered up to date.
  /// If the target file does not exist, it is considered outdated.
  /// </summary>
  public static bool IsFileOutdated(string targetFile, string sourceFile)
  {
    if(!File.Exists(sourceFile))
    {
      // If the source file does not exist, the target file is considered up to date.
      return false;
    }

    if(!File.Exists(targetFile))
    {
      // If the target file does not exist, it is considered outdated.
      return true;
    }

    var sourceInfo = new FileInfo(sourceFile);
    var targetInfo = new FileInfo(targetFile);

    // Compare last write times
    return sourceInfo.LastWriteTimeUtc > targetInfo.LastWriteTimeUtc;
  }
}
