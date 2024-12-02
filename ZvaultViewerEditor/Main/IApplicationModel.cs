﻿/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FldVault.Core.Crypto;
using FldVault.KeyServer;

namespace ZvaultViewerEditor.Main;

/// <summary>
/// Common Application Services for this application
/// (implemented through MainViewModel)
/// </summary>
public interface IApplicationModel
{
  KeyChain KeyChain { get; }

  string StatusMessage { get; set; }

  KeyServerService KeyServer { get; }
}

