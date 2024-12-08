/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ZvaultViewerEditor.Main;

/// <summary>
/// Constants for file / folder dialog guids
/// </summary>
public static class DialogGuids
{
  public static readonly Guid VaultFileGuid =
    new Guid("FD183F85-70B0-4EC7-B06B-62E0C6D414DB");

  public static readonly Guid PeerFolderGuid =
    new Guid("0C60482F-D992-453D-8C93-63F360EC7D6C");
}
