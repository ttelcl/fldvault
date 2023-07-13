/*
 * (c) 2023  ttelcl / ttelcl
 */

using FldVault.Core.Zvlt2;

namespace FldVault.Core.KeyResolution;

/// <summary>
/// A service that can create <see cref="IKeySeed"/> implementing
/// objects (and may assist its seeds in resolving keys)
/// </summary>
public interface IKeySeedService
{
  /// <summary>
  /// Try to create a key seed given the given *.key-info file name,
  /// returning null if this <see cref="IKeySeedService"/> cannot provide one.
  /// </summary>
  IKeySeed? TryCreateFromKeyInfoFile(string keyInfoFileName);

  /// <summary>
  /// Try to create a key seed for the specified *.zvlt file,
  /// returning null if this <see cref="IKeySeedService"/> cannot provide one.
  /// </summary>
  IKeySeed? TryCreateSeedForVault(VaultFile vaultFile);
}

/// <summary>
/// A <see cref="IKeySeedService"/> that specializes in supporting
/// one specific kind of key
/// </summary>
public interface IKeyKindSeedService: IKeySeedService
{
  /// <summary>
  /// The kind of key handled by this service
  /// </summary>
  string Kind { get; }
}
