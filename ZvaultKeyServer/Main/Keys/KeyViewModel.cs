/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Upi.Implementation.Keys;

using ZvaultKeyServer.WpfUtilities;

namespace ZvaultKeyServer.Main.Keys;

/// <summary>
/// ViewModel wrapping a <see cref="KeyState"/>
/// </summary>
public class KeyViewModel: ViewModelBase
{
  public KeyViewModel(KeyState model)
  {
    Model = model;
  }

  public KeyState Model { get; }

  public Guid KeyId { get => Model.KeyId; }

}
