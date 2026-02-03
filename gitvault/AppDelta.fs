module AppDelta

open System
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

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

type private RecipeOrClearChoice =
  | Recipe of string
  | Clear

type private RecipeOrClearOptions = {
  RecipeOrClear: RecipeOrClearChoice option
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

let private showRecipe (recipe: DeltaRecipe) =
  cp $"Recipe '\fc{recipe.Name}\f0' has \fg{recipe.Seeds.Count}\f0 seeds and \fo{recipe.Exclusions.Count}\f0 exclusions:"
  for seed in recipe.Seeds do
    cp $" \fg+  \f0'\fg{seed}\f0'"
  for exclusion in recipe.Exclusions do
    cp $" \fo-  \f0'\fo{exclusion}\f0'"

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
      // Allow "edit" with any edits (effectively an alias for "show")
      //elif isEdit && (o.Seeds.IsEmpty && o.Exclusions.IsEmpty && o.Zaps.IsEmpty) then
      //  cp "\frExpecting at least one \fg-s\fr, \fg-x\fr, or \fg-z\fr argument\f0."
      //  None
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
    let root = context.Root
    let recipes =
      match context.RecipesOption with
      | Some recipes ->
        recipes
      | None ->
        // create and mark as modified, but do not save just yet
        DeltaRecipes.CreateNew()
    if o.IsEdit then
      failwith "Not expecting 'edit' mode"
    let recipeName = o.Recipe
    if recipeName |> String.IsNullOrEmpty then
      failwith "Expecting a recipe name"
    if o.Zaps.IsEmpty |> not then
      failwith "Not expecting -z arguments"
    let seeds = o.Seeds
    let exclusions = o.Exclusions
    if seeds.IsEmpty then
      failwith "Expecting at least one seed"
    if exclusions.IsEmpty then
      cp "\frWarning: \foNo exclusions specified. \fyThat is valid, but not expected\f0."
    let existing, oldrecipe = recipeName |> recipes.Recipes.TryGetValue
    if existing then
      cp $"\frError. \foThe recipe '\fy{recipeName}\fo' already exists\f0."
      1
    else
      cp $"There are {seeds.Length} seeds and {exclusions.Length} exclusions."
      let recipe = new DeltaRecipe(recipeName, seeds, exclusions)
      recipe |> recipes.Put
      let fileName = root.GitvaultRecipesFile
      cp $"Saving \fg{fileName}\f0."
      root |> recipes.SaveIfModified |> ignore
      cp ""
      recipe |> showRecipe
      0

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

let private runDeltaEditInner (o:NewEditOptions) =
  match getContext true with
  | None ->
    // error printed already
    1
  | Some(context) ->
    let root = context.Root
    let recipes = context.RecipesOption.Value
    let recipeName =
      if o.Recipe |> String.IsNullOrEmpty then
        recipes.DefaultRecipe
      else
        o.Recipe
    if recipeName |> String.IsNullOrEmpty then
      cp $"\frNo recipe (\fg-r\fr) provided and no default recipe known.\f0."
      1
    else
      let ok, recipe = recipeName |> recipes.Recipes.TryGetValue
      if ok |> not then
        cp $"\frUnknown recipe \f0'{recipeName}\f0'"
        1
      else
        for zap in o.Zaps do
          zap |> recipe.Zap |> ignore
        for seed in o.Seeds do
          seed |> recipe.AddSeed |> ignore
        for exclusion in o.Exclusions do
          exclusion |> recipe.AddExclusion |> ignore
        if recipe.Seeds.Count = 0 then
          cp $"\frAfter applying edits, no seeds are left. \fyNot saving the resulting invalid recipe\f0."
          1
        else
          let fileName = root.GitvaultRecipesFile
          let saved = root |> recipes.SaveIfModified
          if saved then
            cp $"Saving \fg{fileName}\f0."
          else
            cp $"\foNo changes made\f0."
            cp $"  -> not saving \fg{fileName}\f0."
          cp ""
          recipe |> showRecipe
          0

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

let private runDeltaSendInner context (o:RecipeOnlyOptions) =
  let centralSettings = CentralSettings.Load()
  let bundleRecordCache = new BundleRecordCache(centralSettings, null, null, null)
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
    let kss = new KeyServerService()
    use keychain = new KeyChain()
    let loadkey keyId =
      if keyId |> keychain.ContainsKey then
        true
      else
        if kss.ServerAvailable then
          let presence = kss.LookupKeySync(keyId, keychain)
          match presence with
          | KeyPresence.Unavailable ->
            cp $"\foKey \fb{keyId}\fo not found in the key server\f0."
            cp $"\fySkipping encryption\f0. To fix, unlock the key in the key server GUI and try again."
            false
          | KeyPresence.Cloaked ->
            cp $"Key \fb{keyId}\f0 is present but currently \fohidden\f0 in the key server."
            cp $"\fySkipping encryption\f0. To fix, un-hide the key in the key server GUI and try again."
            false
          | KeyPresence.Present ->
            cp $"Key \fb{keyId}\f0 \fgloaded successfully\f0 from the key server\f0."
            true
          | x -> 
            cp $"\frInternal Error\fo: Unrecognized key presence status: \fr{x}\f0."
            false
        else
          cp $"\foKey server is not available, cannot load key \fb{keyId}\f0."
          cp $"\fySkipping encryption\f0. To fix, start the \fgZvault Key Server GUI\f0, and try again."
          false
    let mutable status = 0
    cpx $"Found delta bundle recipe '\fg{recipe.Name}\f0' with \fc{recipe.Seeds.Count}\f0"
    cp $" seeds and \fo{recipe.Exclusions.Count}\f0 exclusions."
    let repoBundleSource = context.Root.GetBundleSource()
    let reporoots = context.Root.Folder |> GitRoots.ForRepository
    // Unlike "gitvault send" we make no attempt to avoid unnecessary work here.
    for repoAnchorSettings in context.Settings.ByAnchor.Values do
      cp $"Processing <\fc{repoAnchorSettings.VaultAnchor}\f0|\fg{repoAnchorSettings.HostName}\f0|\fy{repoAnchorSettings.RepoName}\f0>."
      let repoAnchorBundleSource = repoAnchorSettings.GetBundleSource(centralSettings)
      let fileName = repoAnchorSettings.GetDeltaBundleFileName(recipeName, centralSettings)
      let shortName = fileName |> Path.GetFileName
      let folderName = fileName |> Path.GetDirectoryName
      cp $"Creating Delta Bundle: \fc{shortName}\f0 (in \fk{folderName}\f0)."
      let bundledOk =
        if repoAnchorBundleSource = null then
          cp $"\frNo bundle source found for this anchor+repo+host.\fo This repo is not the owner\f0 (is there a name conflict with an external bundle?) Skipping."
          false
        elif repoBundleSource.SameSource(repoAnchorBundleSource) |> not then
          cp $"\frThis repo is not the owner of 'its' bundles.\fo It is owned by \fc{repoAnchorBundleSource.SourceFolder}\f0. Skipping."
          false
        else
          let result = GitRunner.CreateBundle(fileName, null, recipe)
          if result.StatusCode <> 0 then
            cp $"\frError\fo: Bundling failed with status code \fr{result.StatusCode}\f0."
            for line in result.ErrorLines do
              cp $"\fo  {line}\f0"
            false
          else
            let fi = new FileInfo(fileName)
            cp $"\fgBundle created successfully\f0, size \fb{fi.Length}\f0."
            true
      if bundledOk then
        let vaultFolder = repoAnchorSettings.GetRepoVaultFolder(centralSettings)
        let keyError = repoAnchorSettings.CanGetKey(centralSettings)
        if keyError |> String.IsNullOrEmpty |> not then
          cp $"\foKey unavailable\f0 ({vaultFolder.VaultFolder}) {keyError}\f0 \frSkipping encryption stage\f0."
        else
          let bundleRecord = repoAnchorSettings.GetBundleRecord(bundleRecordCache)
          let keyInfo = bundleRecord.GetZkeyOrFail()
          let keyId = keyInfo.KeyGuid
          // bundleRecord is about the normal bundle. For delta bundles we need to construct the vault name manually
          let deltaVaultNameShort =
            $"{shortName}.{keyInfo.KeyTag}.mvlt"
          let deltaVaultName =
            Path.Combine(vaultFolder.VaultFolder, deltaVaultNameShort)
          if keyId |> loadkey then
            let metadata = new JObject()
            let bundleTips = fileName |> GitTips.ForBundleFile
            metadata.Add("tips", bundleTips.TipMap |> JToken.FromObject)
            // there is no easy way to get the bundle roots, but we could add the repo roots
            metadata.Add("roots", reporoots.Roots |> JArray.FromObject)
            let encryptionTask =
              task {
                let! writtenFile =
                  MvltWriter.CompressAndEncrypt(
                    fileName,
                    deltaVaultName,
                    keychain,
                    keyInfo.ToPassphraseKeyInfoFile(),
                    ?metadata = Some(metadata))
                return writtenFile
              }
            let writtenFile =
              encryptionTask |> Async.AwaitTask |> Async.RunSynchronously
            let writtenFileShort = writtenFile |> Path.GetFileName
            let writtenFileFolder = writtenFile |> Path.GetDirectoryName
            let writtenFileInfo = new FileInfo(writtenFile)
            cp $"Delta Vault file \fc{writtenFileShort}\f0 created \fgsuccessfully\f0."
            cp $"  (\fb{writtenFileInfo.Length}\fg bytes, \fkin {writtenFileFolder}\f0)"
      else
        status <- 1
        cp $"\foSkiping further processing of this anchor\f0."
    status

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
  match getContext true with
  | None ->
    // error printed already
    1
  | Some(context) ->
    let recipes = context.RecipesOption.Value
    let oo = args |> parseRecipeOnly true {
      Recipe = null
    }
    match oo with
    | None ->
      cp ""
      Usage.usage "delta"
      1
    | Some o ->
      let ok, recipe = o.Recipe |> recipes.Recipes.TryGetValue
      if ok then
        recipe |> showRecipe
        0
      else
        cp $"\foUnknown recipe '\fc{o.Recipe}\fo'\f0."
        1

let private runDeltaDrop args =
  match getContext true with
  | None ->
    // error printed already
    1
  | Some(context) ->
    let recipes = context.RecipesOption.Value
    let oo = args |> parseRecipeOnly true {
      Recipe = null
    }
    match oo with
    | None ->
      cp ""
      Usage.usage "delta"
      1
    | Some o ->
      let ok, recipe = o.Recipe |> recipes.Recipes.TryGetValue
      if ok then
        // retrieve the canonical spelling
        let recipeName = recipe.Name
        let hadDefault = recipes.HasDefaultRecipe
        recipeName |> recipes.Drop |> ignore
        context.Root |> recipes.SaveIfModified |> ignore
        cp $"\fyDeleted recipe \f0'\fr{recipeName}\f0'."
        // Check the possible side effect and inform the user
        if hadDefault && not(recipes.HasDefaultRecipe) then
          cp $"\foAlso cleared the default recipe name\f0."
        0
      else
        cp $"\foUnknown recipe '\fc{o.Recipe}\fo'\f0."
        1

let private parseRecipeOrClear o args =
  let rec parseMore (o:RecipeOrClearOptions) args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      parseMore o rest
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-r" :: name :: rest ->
      rest |> parseMore {o with RecipeOrClear = name |> RecipeOrClearChoice.Recipe |> Some}
    | "-none" :: rest 
    | "-clear" :: rest ->
      rest |> parseMore {o with RecipeOrClear = RecipeOrClearChoice.Clear |> Some}
    | [] ->
      // Allow RecipeOrClear to be None (to only inquire the default)
      o |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  args |> parseMore o

let private runDeltaDefault args =
  match getContext true with
  | None ->
    // error printed already
    1
  | Some(context) ->
    let recipes = context.RecipesOption.Value
    let oo = args |> parseRecipeOrClear {
      RecipeOrClear = None
    }
    match oo with
    | None ->
      cp ""
      Usage.usage "delta"
      1
    | Some o ->
      match o.RecipeOrClear with
      | Some(Recipe(recipeName)) ->
        let ok, recipe = recipeName |> recipes.Recipes.TryGetValue
        if ok then
          recipes.ChangeDefault(recipe.Name)
          context.Root |> recipes.SaveIfModified |> ignore
          cp $"\fgSuccessfully changed default recipe to '\fy{recipe.Name}\fg'\f0."
          0
        else
          cp $"\frCannot set '\fc{recipeName}\fr' as delta bundle recipe name: \fyit is not a known recipe name\f0."
          if recipes.Recipes.Count = 0 then
            cp "\foThere are currently no known recipes defined at all; you cannot set a default right now\f0."
          else
            cpx "Currently defined recipes:"
            for recipe in recipes.Recipes.Values do
              cpx $"  '\fg{recipe.Name}\f0'"
            cp "\f0."
          1
      | Some(Clear) ->
        recipes.ChangeDefault(null)
        context.Root |> recipes.SaveIfModified |> ignore
        cp "\fySuccessfully cleared the default recipe to none\f0."
        0
      | None ->
        if recipes.DefaultRecipe |> String.IsNullOrEmpty then
          cp "There is currently \fyno default recipe\f0 name set."
        else
          cp $"Current default recipe name: \fg{recipes.DefaultRecipe}\f0."
        0

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
            " (\fbdefault\f0)"
          else
            "  \fx       \fx "
        cp $" {defaultText}  '\fg{recipe.Name}\f0'  (\fc+{recipe.Seeds.Count}\f0, \fo-{recipe.Exclusions.Count}\f0)."
      if recipes.HasDefaultRecipe then
        cp $"The default recipe is '\fc{recipes.DefaultRecipe}\f0'."
      else
        cp "\foNo recipe is set as default\f0."
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
