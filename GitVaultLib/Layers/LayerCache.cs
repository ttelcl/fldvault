using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitVaultLib.Layers;

/// <summary>
/// A temporary cache of the layers known in the work folder of a 
/// <see cref="LayerContext"/>
/// </summary>
public class LayerCache
{
  private readonly Dictionary<string, LayerInfo> _cache;
  private readonly Dictionary<string, IReadOnlySet<string>> _brokenDependencies;

  /// <summary>
  /// Create a new <see cref="LayerCache"/>.
  /// Consider using <see cref="SafeToUse(string)"/> or <see cref="BrokenDependencies"/>
  /// to discover broken layers before use.
  /// </summary>
  public LayerCache(LayerContext context)
  {
    Context = context;
    _cache = new Dictionary<string, LayerInfo>(StringComparer.OrdinalIgnoreCase);
    _brokenDependencies = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
    Reload();
  }

  /// <summary>
  /// The context for which this <see cref="LayerCache"/> is caching <see cref="LayerInfo"/>s.
  /// </summary>
  public LayerContext Context { get; }

  /// <summary>
  /// The mapping from layer names to <see cref="LayerInfo"/> instances
  /// </summary>
  public IReadOnlyDictionary<string, LayerInfo> Layers => _cache;

  /// <summary>
  /// A mapping from existing layers to their dependencies that are missing. Only
  /// layers that have broken dependencies are included.
  /// </summary>
  public IReadOnlyDictionary<string, IReadOnlySet<string>> BrokenDependencies => _brokenDependencies;

  /// <summary>
  /// Check if a layer is safe to use. That is: it is present and all its dependencies are present too
  /// </summary>
  /// <param name="layer"></param>
  /// <returns></returns>
  public bool SafeToUse(string layer)
  {
    return _cache.ContainsKey(layer) && !_brokenDependencies.ContainsKey(layer);
  }

  /// <summary>
  /// Try to get the cached <see cref="LayerInfo"/>
  /// </summary>
  /// <param name="layerName"></param>
  /// <param name="layer"></param>
  /// <returns></returns>
  public bool TryGetLayer(string layerName, [NotNullWhen(true)] out LayerInfo? layer)
  {
    return _cache.TryGetValue(layerName, out layer);
  }

  /// <summary>
  /// Build a new layer named <paramref name="layer"/> with dependencies specified by
  /// <paramref name="directDependencies"/>.
  /// </summary>
  /// <param name="layer">
  /// The name of the new layer to create
  /// </param>
  /// <param name="directDependencies">
  /// The primary dependencies of the new layer (indirect dependencies will be calculated
  /// and checked by this method)
  /// </param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown when a dependency (direct or indirect) is missing, or the new layer depends on itself
  /// </exception>
  public LayerInfo BuildNewLayer(
    string layer,
    IEnumerable<string> directDependencies)
  {
    var directs = new HashSet<string>(directDependencies, StringComparer.OrdinalIgnoreCase);
    if(directs.Contains(layer))
    {
      throw new InvalidOperationException(
        $"A layer cannot depend on itself ({layer})");
    }
    var indirects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(var dependency in directs)
    {
      if(!_cache.TryGetValue(dependency, out var dependencyLayer))
      {
        throw new InvalidOperationException(
          $"Error: direct dependency layer '{dependency}' does not exist");
      }
      if(_brokenDependencies.ContainsKey(dependency))
      {
        throw new InvalidOperationException(
          $"Error: cannot use layer '{dependency}'. One or more of its dependencies are missing");
      }
      indirects.UnionWith(dependencyLayer.Dependencies.Keys);
    }
    if(indirects.Contains(layer))
    {
      throw new InvalidOperationException(
        $"A layer cannot indirectly depend on itself ({layer})");
    }
    var depmap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    foreach(var dependency in indirects)
    {
      depmap[dependency] = false;
    }
    // Load directs after indirects, so directs can overrule indirects
    foreach(var dependency in directs)
    {
      depmap[dependency] = true;
    }
    return new LayerInfo(layer, Context.RepoName, Context.HostName, depmap);
  }

  /// <summary>
  /// Reload <see cref="Layers"/> 
  /// </summary>
  public void Reload()
  {
    _cache.Clear();
    _brokenDependencies.Clear();
    var mapping = Context.LoadLayers();
    foreach(var layer in mapping.Values)
    {
      _cache[layer.LayerTag] = layer;
    }
    foreach(var layer in _cache.Values)
    {
      var brokenSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach(var dependency in layer.Dependencies.Keys)
      {
        if(!_cache.ContainsKey(dependency))
        {
          brokenSet.Add(dependency);
        }
      }
      if(brokenSet.Count > 0)
      {
        _brokenDependencies[layer.LayerTag] = brokenSet;
      }
    }
  }
}
