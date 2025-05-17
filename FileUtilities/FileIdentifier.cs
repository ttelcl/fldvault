/*
 * (c) 2009  ttelcl / ttelcl
 */

// copied from an old project

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.ComponentModel;

namespace FileUtilities;

/// <summary>
/// Provides low level file / folder identification information, including
/// the volume identifier of the disk it resides on.
/// </summary>
public class FileIdentifier
{
  /// <summary>
  /// Create a new FileInformation from a file or directory handle
  /// </summary>
  private FileIdentifier(nint handle)
  {
    LoadFromHandle(handle);
  }

  /// <summary>
  /// Create a new FileInformation from a file or directory handle
  /// </summary>
  private FileIdentifier(Microsoft.Win32.SafeHandles.SafeFileHandle sfh)
    : this(sfh.DangerousGetHandle())
  {
    GC.KeepAlive(sfh);
  }

  /// <summary>
  /// Create a new FileInformation from a filestream
  /// </summary>
  public FileIdentifier(FileStream fs)
    : this(fs.SafeFileHandle)
  {
  }

  /// <summary>
  /// Create a new FileInformation from the name of an existing file
  /// or directory
  /// </summary>
  public FileIdentifier(string pathName)
  {
    LoadFromPathname(pathName);
  }

  /// <summary>
  /// Create a new FileInformation from the name of an existing file
  /// or directory, returning null on failure
  /// </summary>
  public static FileIdentifier? FromPath(string pathName)
  {
    try
    {
      return new FileIdentifier(pathName);
    }
    catch(Win32Exception)
    {
      return null;
    }
  }

  /// <summary>
  /// Check if the named file exists and is present on the disk volume
  /// with the specified serial number
  /// </summary>
  /// <param name="fileName">
  /// The name to check
  /// </param>
  /// <param name="volumeSerial">
  /// The serial number as a 8-digit hexadecimal uppercase string
  /// </param>
  public static bool FileAvailable(string fileName, string volumeSerial)
  {
    if(File.Exists(fileName))
    {
      var fi = FromPath(fileName);
      if(fi!=null)
      {
        return fi.VolumeSerial==volumeSerial;
      }
    }
    return false;
  }

  /// <summary>
  /// Return true if both files (or folders) exist and refer to the
  /// same physical file
  /// </summary>
  /// <param name="path1">
  /// The first path
  /// </param>
  /// <param name="path2">
  /// The second path
  /// </param>
  /// <returns>
  /// true if both paths exist and refer to the same file or folder
  /// </returns>
  public static bool AreSame(
    string? path1, string? path2)
  {
    var fid2 = string.IsNullOrEmpty(path2) ? null : FromPath(path2);
    if(fid2 == null)
    {
      return false;
    }
    var fid1 = string.IsNullOrEmpty(path1) ? null : FromPath(path1);
    if(fid1 == null)
    {
      return false;
    }
    return
      fid1.VolumeSerialNumber == fid2.VolumeSerialNumber
      && fid1.FileIndex == fid2.FileIndex;
  }

  /// <summary>
  /// Test if the file identified by this identifier is the same as
  /// the one identified by <paramref name="path"/>.
  /// </summary>
  /// <param name="path">
  /// The path to the file to check against. If null or not existing,
  /// the result will be false
  /// </param>
  /// <returns>
  /// True if <paramref name="path"/> exists and has the same
  /// <see cref="VolumeSerialNumber"/> and <see cref="FileIndex"/>
  /// as this file
  /// </returns>
  public bool SameAs(string? path)
  {
    if(string.IsNullOrEmpty(path))
    {
      return false;
    }
    var fid2 = FromPath(path);
    return
      fid2 != null
      && fid2.VolumeSerialNumber == VolumeSerialNumber
      && fid2.FileIndex == FileIndex;
  }

  /// <summary>
  /// Test if this <see cref="FileIdentifier"/> points to the same file
  /// as <paramref name="fid"/>
  /// </summary>
  public bool SameAs(FileIdentifier? fid)
  {
    return
      fid != null
      && fid.VolumeSerialNumber == VolumeSerialNumber
      && fid.FileIndex == FileIndex;
  }

  /// <summary>
  /// The file attributes
  /// </summary>
  public FileAttributes Attributes { get; private set; }

  /// <summary>
  /// The file creation time (in UTC time zone)
  /// </summary>
  public DateTime CreationTimeUtc { get; private set; }

  /// <summary>
  /// The last file access time (in UTC timezone)
  /// </summary>
  public DateTime LastAccessTimeUtc { get; private set; }

  /// <summary>
  /// The last file write time (in UTC timezone)
  /// </summary>
  public DateTime LastWriteTimeUtc { get; private set; }

  /// <summary>
  /// The volume serial number of the disk volume the file resides on
  /// </summary>
  public uint VolumeSerialNumber { get; private set; }

  /// <summary>
  /// The volume serial number as a hexadecimal string
  /// (without a dash between the 4th an 5th digits
  /// </summary>
  public string VolumeSerial { get { return VolumeSerialNumber.ToString("X8"); } }

  /// <summary>
  /// The file size
  /// </summary>
  public long FileSize { get; private set; }

  /// <summary>
  /// The number of links to the file
  /// </summary>
  public int NumberOfLinks { get; private set; }

  /// <summary>
  /// The file index, uniquely identifying the file on its volume
  /// </summary>
  public long FileIndex { get; private set; }

  void LoadFromPathname(string pathName)
  {
    if(pathName==null)
    {
      throw new ArgumentNullException("pathName");
    }
    using(var sfh = CreateFile(
      pathName,
      0, // only query information, no read or write access required
      3, // share read and write
      nint.Zero,
      3, // OPEN_EXISTING
      0x02000080, // FILE_ATTRIBUTE_NORMAL | FILE_FLAG_BACKUP_SEMANTICS
                  // 'FILE_FLAG_BACKUP_SEMANTICS' is needed to open directories
      nint.Zero))
    {
      if(sfh.IsInvalid)
      {
        throw new Win32Exception();
      }
      LoadFromHandle(sfh.DangerousGetHandle());
    }
  }

  void LoadFromHandle(nint handle)
  {
    BY_HANDLE_FILE_INFORMATION bhfi = new BY_HANDLE_FILE_INFORMATION();
    if(!GetFileInformationByHandle(handle, out bhfi))
    {
      throw new Win32Exception();
    }
    Attributes = (FileAttributes)bhfi.FileAttributes;
    CreationTimeUtc = DateTime.FromFileTimeUtc(bhfi.CreationTime);
    LastAccessTimeUtc = DateTime.FromFileTimeUtc(bhfi.LastAccessTime);
    LastWriteTimeUtc = DateTime.FromFileTimeUtc(bhfi.LastWriteTime);
    VolumeSerialNumber = bhfi.VolumeSerialNumber;
    FileSize = (long)bhfi.FileSizeHigh<<32 | bhfi.FileSizeLow;
    NumberOfLinks = (int)bhfi.NumberOfLinks;
    FileIndex = (long)bhfi.FileIndexHigh<<32 | bhfi.FileIndexLow;
  }

  #region PInvoke Voodoo

  [StructLayout(LayoutKind.Explicit)]
  struct BY_HANDLE_FILE_INFORMATION
  {
    [FieldOffset(0)]
    public uint FileAttributes;
    [FieldOffset(4)]
    public /*FILETIME*/long CreationTime;
    [FieldOffset(12)]
    public /*FILETIME*/long LastAccessTime;
    [FieldOffset(20)]
    public /*FILETIME*/long LastWriteTime;
    [FieldOffset(28)]
    public uint VolumeSerialNumber;
    [FieldOffset(32)]
    public uint FileSizeHigh;
    [FieldOffset(36)]
    public uint FileSizeLow;
    [FieldOffset(40)]
    public uint NumberOfLinks;
    [FieldOffset(44)]
    public uint FileIndexHigh;
    [FieldOffset(48)]
    public uint FileIndexLow;
  }

  [DllImport("kernel32.dll", SetLastError = true)]
  static extern bool GetFileInformationByHandle(nint hFile,
     out BY_HANDLE_FILE_INFORMATION lpFileInformation);

  [DllImport("kernel32.dll", CharSet = CharSet.Unicode,
    CallingConvention = CallingConvention.StdCall, SetLastError = true)]
  static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        nint SecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        nint hTemplateFile
        );

  #endregion PInvoke Voodoo
}

