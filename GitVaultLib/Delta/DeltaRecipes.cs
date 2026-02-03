using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GitVaultLib.GitThings;

using Newtonsoft.Json;

namespace GitVaultLib.Delta;

/// <summary>
/// A collection of <see cref="DeltaRecipe"/> instances, indexed by name
/// </summary>
public class DeltaRecipes
{
  private readonly Dictionary<string, DeltaRecipe> _recipes;

  /// <summary>
  /// Create a new <see cref="DeltaRecipes"/> instance
  /// </summary>
  /// <param name="recipes"></param>
  /// <param name="defaultRecipe"></param>
  public DeltaRecipes(
    IReadOnlyDictionary<string, DeltaRecipe> recipes,
    [JsonProperty("default")] string? defaultRecipe = null)
  {
    _recipes = new Dictionary<string, DeltaRecipe>(recipes, StringComparer.OrdinalIgnoreCase);
    DefaultRecipe=defaultRecipe;
  }

  /// <summary>
  /// Create a brand new, empty, <see cref="DeltaRecipes"/> instance.
  /// This pseudo-constructor does not save it to a file, but does set the
  /// <see cref="Modified"/> flag.
  /// </summary>
  /// <returns></returns>
  public static DeltaRecipes CreateNew()
  {
    var recipes = new DeltaRecipes(new Dictionary<string, DeltaRecipe>(StringComparer.OrdinalIgnoreCase), null);
    recipes.Modified = true;
    return recipes;
  }

  /// <summary>
  /// Try to load the delta recipes file if it exists, returning null if not found.
  /// The file not existing may mean that the repo is not set up for GitVault use
  /// at all, or that no recipes were ever defined.
  /// </summary>
  /// <param name="gitRepoFolder"></param>
  /// <returns></returns>
  public static DeltaRecipes? TryLoad(GitRepoFolder gitRepoFolder)
  {
    if(File.Exists(gitRepoFolder.GitvaultRecipesFile))
    {
      var json = File.ReadAllText(gitRepoFolder.GitvaultRecipesFile);
      return JsonConvert.DeserializeObject<DeltaRecipes>(json);
    }
    return null;
  }

  /// <summary>
  /// A mapping from recipe name to recipe instance. Recipe names are case insensitive.
  /// </summary>
  [JsonProperty("recipes")]
  public IReadOnlyDictionary<string, DeltaRecipe> Recipes => _recipes;

  /// <summary>
  /// The name of the default recipe, if any. Use <see cref="ChangeDefault(string?)"/>
  /// to modify, and subsequently call <see cref="SaveIfModified(GitRepoFolder)"/>.
  /// </summary>
  [JsonProperty("default")]
  public string? DefaultRecipe { get; private set; }

  /// <summary>
  /// True if a default recipe is defined
  /// </summary>
  [JsonIgnore]
  public bool HasDefaultRecipe => !String.IsNullOrEmpty(DefaultRecipe);

  /// <summary>
  /// A flag set when a recipe is added, replaced or removed. Not set when
  /// recipes are changed!
  /// is called.
  /// </summary>
  [JsonIgnore]
  public bool Modified { get; private set; }

  /// <summary>
  /// Change the recorded default recipe. Call <see cref="SaveIfModified(GitRepoFolder)"/>
  /// afterward to save the change; this method does not do that (because it does not know
  /// where to save it)
  /// </summary>
  /// <param name="recipeName"></param>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the argument is not null and not a known recipe name
  /// </exception>
  public void ChangeDefault(string? recipeName)
  {
    Modified = true;
    if(recipeName != null && !_recipes.ContainsKey(recipeName))
    {
      throw new InvalidOperationException(
        $"Unknow recipe '{recipeName}'");
    }
    DefaultRecipe = recipeName;
  }

  /// <summary>
  /// Get the default recipe instance, if defined.
  /// Returns null if no default is configured or if the configured default
  /// does not actually exist.
  /// </summary>
  /// <returns></returns>
  public DeltaRecipe? GetDefaultRecipe()
  {
    if(DefaultRecipe != null && _recipes.TryGetValue(DefaultRecipe, out var defaultRecipe))
    {
      return defaultRecipe;
    }
    return null;
  }

  /// <summary>
  /// Add or replace a recipe
  /// </summary>
  /// <param name="recipe"></param>
  public void Put(DeltaRecipe recipe)
  {
    Modified = true;
    _recipes[recipe.Name] = recipe;
  }

  /// <summary>
  /// Remove a recipe. If this is the default recipe, <see cref="DefaultRecipe"/>
  /// is also set to null.
  /// </summary>
  /// <param name="recipeName"></param>
  /// <returns></returns>
  public bool Drop(string recipeName)
  {
    if(_recipes.Remove(recipeName))
    {
      Modified = true;
      if(recipeName.Equals(DefaultRecipe, StringComparison.OrdinalIgnoreCase))
      {
        DefaultRecipe = null;
      }
      return true;
    }
    return false;
  }

  /// <summary>
  /// Save this instance if modified
  /// </summary>
  /// <param name="repoFolder"></param>
  /// <returns></returns>
  public bool SaveIfModified(
    GitRepoFolder repoFolder)
  {
    var modified = Modified || _recipes.Values.Any(r => r.Modified);
    if(modified)
    {
      var json = JsonConvert.SerializeObject(this, Formatting.Indented);
      var file = repoFolder.GitvaultRecipesFile;
      var tmp = file + ".tmp";
      File.WriteAllText(tmp, json);
      Modified = false;
      foreach(var recipe in _recipes.Values)
      {
        recipe.Modified = false;
      }
      if(File.Exists(file))
      {
        var bak = file + ".bak";
        if(File.Exists(bak))
        {
          File.Delete(bak);
        }
        File.Replace(tmp, file, bak);
      }
      else
      {
        File.Move(tmp, file);
      }
    }
    return modified;
  }

}
