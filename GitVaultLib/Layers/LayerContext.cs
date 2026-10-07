using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GitVaultLib.Configuration;
using GitVaultLib.VaultThings;

using LibGit2Sharp;

namespace GitVaultLib.Layers;

/// <summary>
/// The information needed to manage the collection of layers for a particular
/// host/repo combination
/// </summary>
public class LayerContext
{
  /// <summary>
  /// Create a new <see cref="LayerContext"/> using a pre-created <paramref name="logicalRepo"/>.
  /// </summary>
  /// <param name="repository">
  /// The LibGit2Sharp GIT repository wrapper
  /// </param>
  /// <param name="anchorRepo">
  /// The gitvault settings
  /// </param>
  /// <param name="logicalRepo">
  /// </param>
  public LayerContext(
    Repository repository,
    AnchorRepoSettings anchorRepo,
    LogicalRepository logicalRepo)
  {
    LogicalRepo = logicalRepo;
    Repo = repository;
    Settings = anchorRepo;
  }

  /// <summary>
  /// Create a new <see cref="LayerContext"/> using a <see cref="LogicalRepository"/>
  /// instantiated based on <paramref name="centralSettings"/>.
  /// </summary>
  /// <param name="repository">
  /// The LibGit2Sharp GIT repository wrapper
  /// </param>
  /// <param name="anchorRepo">
  /// The gitvault per anchor-repo settings
  /// </param>
  /// <param name="centralSettings">
  /// The gitvault global settings
  /// </param>
  public LayerContext(
    Repository repository,
    AnchorRepoSettings anchorRepo,
    CentralSettings centralSettings)
    : this(repository, anchorRepo, centralSettings.GetLogicalRepository(anchorRepo))
  {
  }

  /// <summary>
  /// The repository instance (lifetime restricted by whatever called the <see cref="LayerContext"/> constructor)
  /// </summary>
  public Repository Repo { get; }

  /// <summary>
  /// Information for the logical repository
  /// </summary>
  public LogicalRepository LogicalRepo { get; }

  /// <summary>
  /// The detailed settings for this anchor + repo combination
  /// </summary>
  public AnchorRepoSettings Settings { get; }

  /// <summary>
  /// The host name used by this context
  /// </summary>
  public string HostName => Settings.HostName;

  /// <summary>
  /// The repository name used by this context
  /// </summary>
  public string RepoName => Settings.RepoName;

  /// <summary>
  /// The gitvault anchor name used by this context
  /// </summary>
  public string VaultAnchor => Settings.VaultAnchor;

  /// <summary>
  /// The folder where unencrypted layer bundles and layer info files reside
  /// </summary>
  public string BundleFolder => LogicalRepo.BundleFolder;

  /// <summary>
  /// Provides access to functionality related to the folder where encrypted
  /// bundles reside
  /// </summary>
  public RepoVaultFolder VaultFolder => LogicalRepo.VaultFolder;

  /// <summary>
  /// Load all <see cref="LayerInfo"/>s for this <see cref="LayerContext"/>.
  /// </summary>
  /// <returns></returns>
  public IReadOnlyDictionary<string, LayerInfo> LoadLayers()
  {
    var map = new Dictionary<string, LayerInfo>(StringComparer.OrdinalIgnoreCase);
    var pattern = $"{RepoName}.{HostName}.*.layer.json";
    var di = new DirectoryInfo(BundleFolder);
    foreach(var fi in di.GetFiles(pattern))
    {
      var parts = fi.Name.Split('.');
      // The pattern is such that we can expect more than three parts (or at least three parts even with a bogus host)
      var layerpart = parts[^3];
      if(LayerUtilities.IsValidLayerName(layerpart) && LayerInfo.TryLoad(fi.FullName, out var layerInfo))
      {
        map[layerpart] = layerInfo;
      }
    }
    return map;
  }

  /// <summary>
  /// Create a new <see cref="LayerCache"/> for this <see cref="LayerContext"/>.
  /// </summary>
  /// <returns></returns>
  public LayerCache NewLayerCache()
  {
    return new LayerCache(this);
  }
}
