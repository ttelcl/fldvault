module AppDelta

open System
open System.IO

open Newtonsoft.Json

open FileUtilities

open FldVault.KeyServer
open FldVault.Core.Crypto
open FldVault.Core.Mvlt
open FldVault.Core.Vaults

open GitVaultLib.Configuration
open GitVaultLib.Delta
open GitVaultLib.GitThings
open GitVaultLib.VaultThings

open ColorPrint
open CommonTools

type private NewEditOptions = {
  IsEdit: bool
  Zaps: string list
  Seeds: string list
  Exclusions: string list
  Recipe: string
}

type private RecipeOnlyOptions = {
  Recipe: string
}

type private RepoContext = {
  Root: GitRepoFolder
  Settings: RepoSettings
  RecipesOption: DeltaRecipes option
}

let private getContext requireRecipes =
  let repoRoot = "." |> GitRepoFolder.LocateRepoRootFrom
  if repoRoot = null then
    cp "\frNo git repository found in the current folder or its parents\f0."
    None
  else
    let repoSettings = repoRoot.TryLoadGitVaultSettings()
    if repoSettings = null then
      cp $"\foRepository \fg{repoRoot.Folder}\fo has not been initialized for use with gitvault\f0."
      None
    else
      let recipes = DeltaRecipes.TryLoad(repoRoot)
      if recipes = null && requireRecipes then
        cp $"\foRepository \fg{repoRoot.Folder}\fo does not yet have any delta bundle recipes\f0."
        None
      else
        {
          Root = repoRoot
          Settings = repoSettings
          RecipesOption = recipes |> Option.ofObj
        } |> Some

let private parseRecipeOnly requireRecipe o args =
  let rec parseMore (o:RecipeOnlyOptions) args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      parseMore o rest
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-r" :: name :: rest ->
      rest |> parseMore {o with Recipe = name}
    | [] ->
      if requireRecipe && String.IsNullOrEmpty(o.Recipe) then
        cp "\foMissing '\fg-r\fo' option\f0."
        None
      else
        o |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  args |> parseMore o

let private parseNewEdit o args =
  let isEdit = o.IsEdit
  let isNew = o.IsEdit |> not
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      parseMore o rest
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-z" :: zap :: rest when isEdit ->
        rest |> parseMore {o with Zaps = zap :: o.Zaps}
    | "-s" :: seed :: rest ->
      // Only do minimal validation here. Let Git handle the true validation.
      if seed.StartsWith('-') && not(seed = "--all" || seed = "--branches" || seed = "--tags") then
        cp $"\fo'\fc{seed}\f0' is not a valid argument to \fg-s\fo \f0."
        None
      else
        rest |> parseMore {o with Seeds = seed :: o.Seeds}
    | "-x" :: exclusion :: rest ->
      // Only do minimal validation here. Let Git handle the true validation.
      if exclusion.StartsWith('-') then
        cp $"\fo'\fc{exclusion}\f0' is not a valid argument to \fg-x\fo \f0."
        None
      else
        rest |> parseMore {o with Exclusions = exclusion :: o.Exclusions}
    | "-r" :: name :: rest ->
      rest |> parseMore {o with Recipe = name}
    | [] ->
      if isNew && String.IsNullOrEmpty(o.Recipe) then
        cp "\frMissing \fg-r\fr argument\f0."
        None
      elif isNew && o.Seeds.IsEmpty then
        cp "\frExpecting at least one \fg-s\fr argument\f0."
        None
      elif isEdit && (o.Seeds.IsEmpty && o.Exclusions.IsEmpty && o.Zaps.IsEmpty) then
        cp "\frExpecting at least one \fg-s\fr, \fg-x\fr, or \fg-z\fr argument\f0."
        None
      else
        {o with
           Zaps = o.Zaps |> List.rev
           Seeds = o.Seeds |> List.rev
           Exclusions = o.Exclusions |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  args |> parseMore o

let private runDeltaNewInner o =
  match getContext false with
  | None ->
    // error printed already
    1
  | Some(context) ->
    cp "\frNYI\f0."
    1

let private runDeltaNew args =
  let oo = args |> parseNewEdit {
    IsEdit = false
    Zaps = []
    Seeds = []
    Exclusions = []
    Recipe = null
  }
  match oo with
  | None ->
    cp ""
    Usage.usage "delta"
    1
  | Some o ->
    o |> runDeltaNewInner

let private runDeltaEditInner args =
  match getContext true with
  | None ->
    // error printed already
    1
  | Some(context) ->
    cp "\frNYI\f0."
    1

let private runDeltaEdit args =
  let oo = args |> parseNewEdit {
    IsEdit = true
    Zaps = []
    Seeds = []
    Exclusions = []
    Recipe = null
  }
  match oo with
  | None ->
    cp ""
    Usage.usage "delta"
    1
  | Some o ->
    o |> runDeltaEditInner

let private runDeltaDrop args =
  cp "\frNYI\f0."
  1

let private runDeltaDefault args =
  cp "\frNYI\f0."
  1

let private runDeltaSendInner context (o:RecipeOnlyOptions) =
  let recipes = context.RecipesOption.Value
  let recipeName =
    if String.IsNullOrEmpty(o.Recipe) then
      recipes.DefaultRecipe
    else
      o.Recipe
  if recipeName |> String.IsNullOrEmpty then
    failwith "Internal error - expecting 'recipe name' to be defined"
  let ok, recipe = recipeName |> recipes.Recipes.TryGetValue
  if ok |> not then
    cp $"\foUnknown recipe \f0'{recipeName}\f0'"
    1
  else
    cpx $"Found delta bundle recipe '\fg{recipe.Name}\f0' with \fb{recipe.Seeds.Count}\f0"
    cp $" seeds and \fc{recipe.Exclusions.Count}\f0 exclusions."
    cp $"\frNYI\f0."
    1

let private runDeltaSend args =
  match getContext true with
  | None ->
    // error printed already
    1
  | Some(context) ->
    let recipes = context.RecipesOption.Value
    let requireRecipe = recipes.HasDefaultRecipe |> not
    let oo = args |> parseRecipeOnly requireRecipe {
      Recipe = null
    }
    match oo with
    | None ->
      cp ""
      Usage.usage "delta"
      1
    | Some o ->
      runDeltaSendInner context o

let private runDeltaShow args =
  cp "\frNYI\f0."
  1

let private runDeltaList args =
  // there are no additional arguments to parse
  match getContext false with
  | None ->
    // error printed already
    1
  | Some(context) ->
    match context.RecipesOption with
    | None ->
      cp $"\foNo delta bundle recipes defined in \fg{context.Root.Folder}\f0."
      1
    | Some recipes ->
      cp $"\fg{context.Root.Folder}\f0 contains \fb{recipes.Recipes.Count}\f0 recipes:"
      for recipe in recipes.Recipes.Values do
        let defaultText =
          if recipe.Name = recipes.DefaultRecipe then
            " (\fbdefault\f0)."
          else
            ""
        cp $" \fg{recipe.Name,24}\f0{defaultText}\f0."
      if recipes.HasDefaultRecipe then
        cp $"The default recipe is '\fc{recipes.DefaultRecipe}\f0'."
      else
        cp "\foNo recipe is set as default."
      0

let run args =
  match args with
  | "-h" :: _
  | [] ->
    Usage.usage "delta"
    1
  | "new" :: rest -> rest |> runDeltaNew
  | "edit" :: rest -> rest |> runDeltaEdit
  | "drop" :: rest -> rest |> runDeltaDrop
  | "default" :: rest -> rest |> runDeltaDefault
  | "send" :: rest -> rest |> runDeltaSend
  | "list" :: rest -> rest |> runDeltaList
  | "show" :: rest -> rest |> runDeltaShow
  | x :: _ ->
    cp $"\frUnknown delta subcommand \f0'\fo{x}\f0'"
    Usage.usage "delta"
    1
