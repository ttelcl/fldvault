﻿/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using FldVault.Core.Zvlt2;

namespace ZvaultViewerEditor.Main;

/// <summary>
/// Describes a file entry inside a vault file
/// (only entries that have a name and size)
/// </summary>
public class VaultEntry
{
  /// <summary>
  /// Create a new VaultEntry
  /// </summary>
  private VaultEntry(
    FileElement element,
    FileMetadata metadata)
  {
    if(String.IsNullOrEmpty(metadata.Name)
      || !metadata.Size.HasValue
      || !metadata.Stamp.HasValue)
    {
      throw new NotSupportedException(
        "Anonymous entries and entries without a size annotation are not supported");
    }
    Metadata = metadata;
    Element = element;
    CompressedSize = Element.GetContentLength();
  }

  /// <summary>
  /// Try to create a new VaultEntry. If the metadata has a null name
  /// or null size, null is returned.
  /// </summary>
  /// <param name="metadata"></param>
  /// <param name="element"></param>
  /// <returns></returns>
  public static VaultEntry? TryCreate(
    FileElement element,
    FileMetadata metadata)
  {
    if(String.IsNullOrEmpty(metadata.Name)
      || !metadata.Size.HasValue
      || !metadata.Stamp.HasValue)
    {
      return null;
    }
    return new VaultEntry(element, metadata);
  }

  public static VaultEntry? TryCreate(
    FileElement element,
    VaultFileReader reader)
  {
    var metadata = element.GetMetadata(reader);
    return TryCreate(element, metadata);
  }

  public FileMetadata Metadata { get; }

  public FileElement Element { get; }

  /// <summary>
  /// The logical file name, or null if it is anonymous
  /// </summary>
  public string FileName { get => Metadata.Name!; }

  public long CompressedSize { get; }

  public long DecompressedSize { get => Metadata.Size!.Value; }

  public DateTime UtcStamp { get => Metadata.UtcStamp!.Value; }

  public string UtcText { get => UtcStamp.ToString("yyyy-MM-dd HH:mm:ss.fff"); }

  public DateTime LocalStamp { get => Metadata.UtcStamp!.Value.ToLocalTime(); }

  public string LocalText { get => LocalStamp.ToString("yyyy-MM-dd HH:mm:ss.fff"); }
}
