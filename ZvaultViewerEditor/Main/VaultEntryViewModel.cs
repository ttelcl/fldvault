/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using FldVault.Core.Zvlt2;
using ZvaultViewerEditor.WpfUtilities;

namespace ZvaultViewerEditor.Main;

/// <summary>
/// Describes a file entry inside a vault file
/// (only entries that have a name and size)
/// </summary>
public class VaultEntryViewModel: ViewModelBase
{
  /// <summary>
  /// Create a new VaultEntry
  /// </summary>
  private VaultEntryViewModel(
    FileElement element,
    FileMetadata metadata,
    FileHeader header)
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
    Header = header;
    CompressedSize = Element.GetContentLength();
  }

  /// <summary>
  /// Try to create a new VaultEntry. If the metadata has a null name
  /// or null size, null is returned.
  /// </summary>
  /// <param name="metadata"></param>
  /// <param name="element"></param>
  /// <returns></returns>
  public static VaultEntryViewModel? TryCreate(
    FileElement element,
    FileMetadata metadata,
    FileHeader header)
  {
    if(String.IsNullOrEmpty(metadata.Name)
      || !metadata.Size.HasValue
      || !metadata.Stamp.HasValue)
    {
      return null;
    }
    return new VaultEntryViewModel(element, metadata, header);
  }

  public static VaultEntryViewModel? TryCreate(
    FileElement element,
    VaultFileReader reader)
  {
    var metadata = element.GetMetadata(reader);
    var header = element.GetHeader(reader);
    return TryCreate(element, metadata, header);
  }

  public FileMetadata Metadata { get; }

  public FileElement Element { get; }

  public FileHeader Header { get; }

  public Guid FileGuid { get => Header.FileId; }

  /// <summary>
  /// The logical file name, or null if it is anonymous
  /// </summary>
  public string FileName { get => Metadata.Name!; }

  public long CompressedSize { get; }

  public long DecompressedSize { get => Metadata.Size!.Value; }

  public int CompressionRatio {
    get => (int)(100 * (CompressedSize / (double)DecompressedSize));
  }

  public string CompressionRatioText {
    get => $"({CompressionRatio:F0}%)";
  }

  public DateTime UtcStamp { get => Metadata.UtcStamp!.Value; }

  public string UtcText { get => UtcStamp.ToString("yyyy-MM-dd HH:mm:ss.fff"); }

  public DateTime LocalStamp { get => Metadata.UtcStamp!.Value.ToLocalTime(); }

  public string LocalText { get => LocalStamp.ToString("yyyy-MM-dd HH:mm:ss"); }

  public DateTime UtcEncryptionStamp { get => Header.EncryptionStampUtc; }

  public string LocalEncryptionText {
    get => Header.EncryptionStampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
  }

  public bool Selected {
    get => _selected;
    set {
      if(SetValueProperty(ref _selected, value))
      {
      }
    }
  }
  private bool _selected = false;

  public bool Expanded {
    get => _expanded;
    set {
      if(SetValueProperty(ref _expanded, value))
      {
        RaisePropertyChanged(nameof(ExpanderIcon));
      }
    }
  }
  private bool _expanded = false;

  public string ExpanderIcon {
    get {
      return Expanded ? "ChevronUpCircleOutline" : "ChevronDownCircleOutline";
    }
  }

}
