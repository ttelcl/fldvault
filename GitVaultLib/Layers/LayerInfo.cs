using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GitVaultLib.Configuration;

using Newtonsoft.Json;

namespace GitVaultLib.Layers;

/// <summary>
/// JSON-serializable description of a layer bundle
/// </summary>
/// <remarks>
/// To create a brand new <see cref="LayerInfo"/>, start with a <see cref="LayerContext"/>,
/// use it to create a <see cref="LayerCache"/>, use that to validate dependencies,
/// the use its <see cref="LayerCache.BuildNewLayer(string, IEnumerable{string})"/> method
/// to create the new <see cref="LayerInfo"/> describing the new layer.
/// </remarks>
public class LayerInfo
{
  /// <summary>
  /// Create a new LayerInfo (deserialization constructor)
  /// </summary>
  public LayerInfo(
    string layer,
    string repo,
    string host,
    Dictionary<string, bool> dependencies)
  {
    if(!LayerUtilities.IsValidLayerName(layer))
    {
      throw new ArgumentOutOfRangeException(nameof(layer), $"'{layer}' is not a valid layer name");
    }
    LayerTag = layer;
    RepoName = repo;
    HostTag = host;
    Dependencies = new Dictionary<string, bool>(dependencies, StringComparer.InvariantCultureIgnoreCase);
    FilePrefix = $"{RepoName}.{HostTag}.{LayerTag}";
  }

  /// <summary>
  /// The identifier of the layer within its repository and host
  /// </summary>
  [JsonProperty("layer")]
  public string LayerTag { get; }

  /// <summary>
  /// The name used to identify the repository on this host
  /// </summary>
  [JsonProperty("repo")]
  public string RepoName { get; }

  /// <summary>
  /// The name used to identify this pseudo-host on this machine. Usually the machine name,
  /// but a single machine may identify itself under other pseudo-host names.
  /// </summary>
  [JsonProperty("host")]
  public string HostTag { get; }

  /// <summary>
  /// A list of the direct and indirect layer dependencies. The keys are the 
  /// layer tags being depended on, the values are <see langword="true"/> for
  /// explicit (direct) dependencies, <see langword="false"/> for implicit
  /// (indirect) dependencies.
  /// </summary>
  [JsonProperty("dependencies")]
  public IReadOnlyDictionary<string, bool> Dependencies { get; }

  /// <summary>
  /// The prefix expected for files related to this layer (calculated from
  /// other properties)
  /// </summary>
  [JsonIgnore]
  public string FilePrefix { get; }

  /// <summary>
  /// Save this object to its intended file name into the given folder.
  /// If there already is a file with the same name, a backup copy is created.
  /// </summary>
  /// <param name="context"></param>
  /// <returns>
  /// The full path of the saved file
  /// </returns>
  public string Save(LayerContext context)
  {
    var folder = context.BundleFolder;
    var fileName = Path.Combine(folder, $"{FilePrefix}.layer.json");
    var tmpName = fileName + ".tmp";
    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
    using(var writer = File.CreateText(tmpName))
    {
      writer.WriteLine(json);
    }
    if(File.Exists(fileName))
    {
      var bakName = fileName + ".bak";
      File.Replace(tmpName, fileName, bakName);
    }
    else
    {
      File.Move(tmpName, fileName);
    }
    return fileName;
  }

  /// <summary>
  /// "Deletes" the layer info file for this <see cref="LayerInfo"/> instance if it
  /// exists in the <see cref="LayerContext.BundleFolder"/> of <paramref name="context"/>.
  /// </summary>
  /// <remarks>
  /// The file is not truly deleted, but renamed to a backup file name (potentially overwriting an
  /// existing backup).
  /// </remarks>
  /// <param name="context"></param>
  /// <returns>
  /// True if the file existed and was now renamed, false if it did not exist before this call.
  /// </returns>
  public bool Delete(LayerContext context)
  {
    var folder = context.BundleFolder;
    var fileName = Path.Combine(folder, $"{FilePrefix}.layer.json");
    if(File.Exists(fileName))
    {
      var bakName = fileName + ".bak";
      File.Move(fileName, bakName, true);
      return true;
    }
    return false;
  }

  /// <summary>
  /// Try to load a <see cref="LayerInfo"/> from a file that may or may not exist.
  /// </summary>
  /// <param name="fileName">
  /// The full path to the <c>*.layer.json</c> to load. This overload accepts any name.
  /// </param>
  /// <param name="layerInfo">
  /// The loaded <see cref="LayerInfo"/> instance on success, <see langword="null"/> if
  /// the file did not exist.
  /// </param>
  /// <returns></returns>
  public static bool TryLoad(
    string fileName,
    [NotNullWhen(true)] out LayerInfo? layerInfo)
  {
    if(File.Exists(fileName))
    {
      var json = File.ReadAllText(fileName);
      layerInfo = JsonConvert.DeserializeObject<LayerInfo>(json);
    }
    else
    {
      layerInfo = null;
    }
    return layerInfo != null;
  }

  /// <summary>
  /// Try to load a <see cref="LayerInfo"/> from its file in the folder determined by <paramref name="context"/>,
  /// and the file name constructed from <paramref name="layer"/> and <paramref name="context"/>
  /// </summary>
  /// <param name="context"></param>
  /// <param name="layer">
  /// The layer tag (which must satisfy <see cref="LayerUtilities.IsValidLayerName"/>) in
  /// the repository identified by <paramref name="context"/>.
  /// </param>
  /// <param name="layerInfo">
  /// The loaded <see cref="LayerInfo"/> instance on success, <see langword="null"/> if
  /// the file did not exist.
  /// </param>
  /// <returns></returns>
  /// <exception cref="ArgumentOutOfRangeException"></exception>
  public static bool TryLoad(
    LayerContext context,
    string layer,
    [NotNullWhen(true)] out LayerInfo? layerInfo)
  {
    if(!LayerUtilities.IsValidLayerName(layer))
    {
      throw new ArgumentOutOfRangeException(nameof(layer), $"'{layer}' is not a valid layer name");
    }
    var filePrefix = $"{context.RepoName}.{context.HostName}.{layer}";
    var fileName = Path.Combine(context.BundleFolder, $"{filePrefix}.layer.json");
    return TryLoad(fileName, out layerInfo);
  }
}
