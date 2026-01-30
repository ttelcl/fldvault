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
  public DeltaRecipes(
    IReadOnlyDictionary<string, DeltaRecipe> recipes)
  {
    _recipes = new Dictionary<string, DeltaRecipe>(recipes);
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
  /// A mapping from recipe name to recipe instance
  /// </summary>
  [JsonProperty("recipes")]
  public IReadOnlyDictionary<string, DeltaRecipe> Recipes => _recipes;

  /// <summary>
  /// A flag set when a recipe is added, replaced or removed. Not set when
  /// recipes are changed!
  /// is called.
  /// </summary>
  [JsonIgnore]
  public bool Modified { get; private set; }

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
  /// Remove a recipe
  /// </summary>
  /// <param name="recipeName"></param>
  /// <returns></returns>
  public bool Drop(string recipeName)
  {
    if(_recipes.Remove(recipeName))
    {
      Modified = true;
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
      File.WriteAllText(file, json);
      Modified = false;
      foreach(var recipe in _recipes.Values)
      {
        recipe.Modified = false;
      }
    }
    return modified;
  }

}
