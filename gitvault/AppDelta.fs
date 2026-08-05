module AppDelta

open System
open System.Globalization
open System.IO

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open LibGit2Sharp

open FldVault.KeyServer
open FldVault.Core.Crypto
open FldVault.Core.Mvlt

open GitVaultLib.Bundles
open GitVaultLib.Configuration
open GitVaultLib.Delta
open GitVaultLib.GitThings

open ColorPrint
open CommonTools

type private NewEditOptions = {
  IsEdit: bool
  Zaps: string list
  Seeds: string list
  Exclusions: string list
  Recipe: string
  V2: bool
}

type private RecipeOnlyOptions = {
  Recipe: string
}

type private RecipeOrAllChoice =
  | Recipe of string
  | All

type private RecipeOrAllOptions = {
  RecipeOrAll: RecipeOrAllChoice option
}

type private RecipeOrClearChoice =
  | Recipe of string
  | Clear

type private RecipeOrClearOptions = {
  RecipeOrClear: RecipeOrClearChoice option
}

type private SendContext = {
  Root: GitRepoFolder
  Settings: RepoSettings
  RecipesOption: DeltaRecipes option
  GitvaultSettings: CentralSettings
  BundleCache: BundleRecordCache
  Kss: KeyServerService
}

type private RepoContext = {
  Root: GitRepoFolder
  Settings: RepoSettings
  RecipesOption: DeltaRecipes option
}

let private tryGetRecipe (recipes: DeltaRecipes) recipeName =
  let ok, recipeName =
    if recipeName |> String.IsNullOrEmpty then
      if recipes.HasDefaultRecipe then
        true, recipes.DefaultRecipe
      else
        false, null
    else
      true, recipeName
  if ok then
    let ok, recipe = recipeName |> recipes.Recipes.TryGetValue
    if ok then
      recipe |> Some
    else
      cp $"\foUnknown recipe \f0'\fy{recipeName}\f0'"
      None
  else
    cp "\foNo recipe specified and no default set\f0."
    None

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
    | "-R" :: rest ->
      if requireRecipe then
        cp "\foThis command requires an explicit recipe \fo(\fg-r\fo), not \fg-R\f0."
        None
      else
        rest |> parseMore {o with Recipe = null}
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

let private parseRecipeOrAll o args =
  let rec parseMore (o:RecipeOrAllOptions) args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      parseMore o rest
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-r" :: name :: rest ->
      rest |> parseMore {o with RecipeOrAll = name |> RecipeOrAllChoice.Recipe |> Some}
    | "-R" :: rest ->
      rest |> parseMore {o with RecipeOrAll = None}
    | "-all" :: rest ->
      rest |> parseMore {o with RecipeOrAll = RecipeOrAllChoice.All |> Some}
    | [] ->
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
      // Only do minimal validation here. Let Git and/or the V2 glue handle the true validation.
      if not(o.V2) && seed.StartsWith('-') && not(seed = "--branches" || seed = "--tags") then
        cp $"\fo'\fc{seed}\f0' is not a valid argument to \fg-s\fo \f0(in \fg-v1\f0 mode)"
        None
      else
        rest |> parseMore {o with Seeds = seed :: o.Seeds}
    | "-x" :: exclusion :: rest ->
      // Only do minimal validation here. Let Git and/or the V2 glue handle the true validation.
      if not(o.V2) && exclusion.StartsWith('-') then
        cp $"\fo'\fc{exclusion}\f0' is not a valid argument to \fg-x\fo \f0(in \fg-v1\f0 mode)"
        None
      else
        rest |> parseMore {o with Exclusions = exclusion :: o.Exclusions}
    | "-r" :: name :: rest ->
      rest |> parseMore {o with Recipe = name}
    | "-v1" :: rest ->
      if isEdit then
        cp "\fg-v1\fo is not supported for \fyedit\fo, only \fynew\f0."
        None
      elif o.Seeds |> List.isEmpty && o.Exclusions |> List.isEmpty then
        rest |> parseMore {o with V2 = false}
      else
        cp "\fg-v1\fo must appear before any \fg-s\fo or \fg-x\fo options\f0."
        None
    | "-v2" :: rest ->
      if isEdit then
        cp "\fg-v2\fo is not supported for \fyedit\fo, only \fynew\f0."
        None
      elif o.Seeds |> List.isEmpty && o.Exclusions |> List.isEmpty then
        rest |> parseMore {o with V2 = true}
      else
        cp "\fg-v2\fo must appear before any \fg-s\fo or \fg-x\fo options\f0."
        None
    | "-standard" :: rest ->
      if isEdit then
        cp "\fg-standard\fo is not supported for \fyedit\fo, only \fynew\f0."
        None
      elif o.Seeds |> List.isEmpty && o.Exclusions |> List.isEmpty then
        rest |> parseMore {o with V2 = true; Seeds = ["refs/heads/*"]; Exclusions = ["refs/remotes/*"]}
      else
        cp "\fg-standard\fo must appear before any \fg-s\fo or \fg-x\fo options\f0."
        None
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
      let recipe = new DeltaRecipe(recipeName, [], [], o.V2)
      // Start empty, so seeds and exclusions are better validated
      for seed in seeds do
        seed |> recipe.AddSeed
      for exclusion in exclusions do
        exclusion |> recipe.AddExclusion
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
    V2 = false
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
        let o = {o with V2 = recipe.V2} // ignore value passed in option
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
    V2 = false // IGNORED
  }
  match oo with
  | None ->
    cp ""
    Usage.usage "delta"
    1
  | Some o ->
    o |> runDeltaEditInner

let private refText (reference:string) =
  if reference.StartsWith("refs/heads/") then
    "\fg" + reference.Substring("refs/heads/".Length)
  elif reference.StartsWith("refs/tags/") then
    "\fb" + reference.Substring("refs/tags/".Length)
  elif reference.StartsWith("refs/remotes/") then
    "\fc" + reference.Substring("refs/remotes/".Length)
  else
    "\f0" + reference

let private runDeltaSendInner context (o:RecipeOrAllOptions) =
  let centralSettings = CentralSettings.Load()
  let bundleRecordCache = new BundleRecordCache(centralSettings, null, null, null)
  let recipes = context.RecipesOption.Value
  let recipeListOption =
    match o.RecipeOrAll with
    | None ->
      if recipes.HasDefaultRecipe |> not then
        cp "\foNo recipe specified and no default set\f0."
        None
      else
        let ok, recipe = recipes.DefaultRecipe |> recipes.Recipes.TryGetValue
        if ok then
          [ recipe ] |> Some
        else
          cp $"\foUnknown recipe \f0'\fy{recipes.DefaultRecipe}\f0'"
          None
    | Some(RecipeOrAllChoice.Recipe(recipeName)) ->
      let ok, recipe = recipeName |> recipes.Recipes.TryGetValue
      if ok then
        [ recipe ] |> Some
      else
        cp $"\foUnknown recipe \f0'\fy{recipeName}\f0'"
        None
    | Some(RecipeOrAllChoice.All) ->
      recipes.Recipes.Values |> Seq.toList |> Some
  match recipeListOption with
  | None ->
    // error message already printed
    1
  | Some recipeList ->
    let mutable status = 0
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
            cp $"\frSkipping encryption\f0. To fix, unlock the key in the key server GUI and try again."
            false
          | KeyPresence.Cloaked ->
            cp $"Key \fb{keyId}\f0 is present but currently \fohidden\f0 in the key server."
            cp $"\frSkipping encryption\f0. To fix, un-hide the key in the key server GUI and try again."
            false
          | KeyPresence.Present ->
            cp $"Key \fb{keyId}\f0 \fgloaded successfully\f0 from the key server\f0."
            true
          | x -> 
            cp $"\frInternal Error\fo: Unrecognized key presence status: \fr{x}\f0."
            false
        else
          cp $"\foKey server is not available, cannot load key \fb{keyId}\f0."
          cp $"\frSkipping encryption\f0. To fix, start the \fgZvault Key Server GUI\f0, and try again."
          false
    for recipe in recipeList do
      cpx $"Found delta bundle recipe '\fg{recipe.Name}\f0' with \fc{recipe.Seeds.Count}\f0"
      cp $" seeds and \fo{recipe.Exclusions.Count}\f0 prerequisites."
      let repoBundleSource = context.Root.GetBundleSource()
      let reporoots = context.Root.Folder |> GitRoots.ForRepository
      use repo2 = new Repository(context.Root.Folder);
      // Unlike "gitvault send" we make no attempt to avoid unnecessary work here.
      for repoAnchorSettings in context.Settings.ByAnchor.Values do
        cp $"Processing <\fc{repoAnchorSettings.VaultAnchor}\f0|\fg{repoAnchorSettings.HostName}\f0|\fy{repoAnchorSettings.RepoName}\f0>."
        let repoAnchorBundleSource = repoAnchorSettings.GetBundleSource(centralSettings)
        let fileName = repoAnchorSettings.GetDeltaBundleFileName(recipe.Name, centralSettings)
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
            let result, errorsShown =
              if recipe.V2 then
                use evaluator = new DeltaV2Evaluation(context.Root.Folder)
                let ok = recipe |> evaluator.Prepare
                
                let seedCount = evaluator.SeedCommitsByRef.Count
                cp $"Including \fb{seedCount}\f0 references to \fc{evaluator.SeedRefsByCommit.Count}\f0 distinct commits."
                cp $"Excluding commits reachable from \fb{evaluator.ExclusionsCommitsById.Count}\f0 exclusion commits."
                cp "Calculating expected bundle commits:"
                cp $"  Total bundle commit count = \fb{evaluator.BundleCommits.Count}\f0."
                let visibleSeedCount = evaluator.IncludedSeeds.Values |> Seq.sumBy (fun l -> l.Count)
                let visibleSeedCommitCount = evaluator.IncludedSeeds.Count
                let droppedSeedCount = evaluator.DroppedSeeds.Values |> Seq.sumBy (fun l -> l.Count)
                let droppedSeedCommitCount = evaluator.DroppedSeeds.Count
                cp $"  Seeds to be bundled: \fc{visibleSeedCount}\f0 (of \fb{seedCount}\f0) references (\fc{visibleSeedCommitCount}\f0 commits)"
                let names = evaluator.IncludedSeedRefs()
                for name in names do
                  cp $"    + \fg{name}\f0."
                cpx $"  Dropped seeds: \fr{droppedSeedCount}\f0 (of \fb{seedCount}\f0) references (\fr{droppedSeedCommitCount}\f0 commits)"
                if verbose then
                  cp ""
                  let names = evaluator.DroppedSeedRefs()
                  for name in names do
                    cp $"    \fk{name}\f0."
                else
                  cp " (\fkpass \fg-v\fk for details\f0)"
                cp $"  Found \fb{evaluator.TailCommits.Count}\f0 prerequisite commits"
                let tails =
                  evaluator.TailCommits.Values
                  |> Seq.sortByDescending (fun c -> (c.Committer.When, c.Author.When))
                  |> Seq.toArray
                for tail in tails do
                  let stamp = tail.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
                  cp $"    - \fo{tail.Sha.Substring(0, 8)}\f0  {stamp}."

                // Get on with it ...
                if ok then
                  if evaluator.Warnings.Count > 0 then
                    cp $"Recipe preparation \fgsucceeded\f0 with \fb{evaluator.Warnings.Count}\f0 warnings:"
                    for warning in evaluator.Warnings do
                      cp $"\foWarning:\f0 {warning}"
                  cp $"Bundling to \fc{fileName}\f0 ..."
                  GitRunner.CreateBundle(fileName, context.Root.Folder, evaluator), false
                else
                  cp $"Recipe preparation \frfailed\f0 with \fr{evaluator.Errors.Count}\f0 errors and \fy{evaluator.Warnings.Count}\f0 warnings\f0."
                  for error in evaluator.Errors do
                    cp $"\frError:\f0 {error}"
                  for warning in evaluator.Warnings do
                    cp $"\foWarning:\f0 {warning}"
                  evaluator.ToErrorResult(), true
              else
                cp $"Bundling to \fc{fileName}\f0 ..."
                GitRunner.CreateBundle(fileName, null, recipe), false
            if result.StatusCode <> 0 then
              cp $"\frError\fo: Bundling failed with status code \fr{result.StatusCode}\f0."
              if errorsShown |> not then
                for line in result.ErrorLines do
                  cp $"\fo  {line}\f0"
              false
            else
              let fi = new FileInfo(fileName)
              cp $"\fgBundle created successfully\f0, size \fb{fi.Length}\f0."
              true
        if bundledOk then
          let bundleHeader = fileName |> BundleHeader.FromFile
          let metadata = JObject.FromObject(bundleHeader)
          // also add repo roots to metadata
          metadata.Add("roots", reporoots.Roots |> JArray.FromObject)
          let vaultFolder = repoAnchorSettings.GetRepoVaultFolder(centralSettings)
          let keyError = repoAnchorSettings.CanGetKey(centralSettings)
          if keyError |> String.IsNullOrEmpty |> not then
            cp $"\foKey unavailable\f0 ({vaultFolder.VaultFolder}) {keyError}\f0 \frSkipping encryption stage\f0."
          else
            let bundleRecord = repoAnchorSettings.GetBundleRecord(bundleRecordCache)
            let keyInfo = bundleRecord.GetZkeyOrFail()
            let keyId = keyInfo.KeyGuid
            // bundleRecord is about the normal full bundle. For delta bundles we need to construct the vault name manually
            let deltaVaultNameShort =
              $"{shortName}.{keyInfo.KeyTag}.mvlt"
            let deltaVaultName =
              Path.Combine(vaultFolder.VaultFolder, deltaVaultNameShort)
            if keyId |> loadkey then
              let encryptionTask =
                task {
                  let! (writtenFile:string) =
                    MvltWriter.CompressAndEncrypt(
                      fileName,
                      deltaVaultName,
                      keychain,
                      keyInfo.ToPassphraseKeyInfoFile(),
                      ?metadata = Some(metadata),
                      ?writeMetafile = Some(true))
                  return writtenFile
                }
              let writtenFile =
                encryptionTask |> Async.AwaitTask |> Async.RunSynchronously
              let writtenFileShort = writtenFile |> Path.GetFileName
              let writtenFileFolder = writtenFile |> Path.GetDirectoryName
              let writtenFileInfo = new FileInfo(writtenFile)
              cp $"Delta Vault file \fc{writtenFileShort}\f0 created \fgsuccessfully\f0."
              cp $"  (\fb{writtenFileInfo.Length}\fg bytes, \fkin {writtenFileFolder}\f0)"
          do
            let bundleInfo = BundleInfo.Build(repo2, bundleHeader)
            let commits =
              bundleInfo.Commits
              |> Seq.sortByDescending (fun c -> c.CommitDate)
              |> Seq.toArray
            for commit in commits do
              let shortId = commit.Commit.Substring(0, 8)
              let commitDate = commit.CommitDate.ToString("yyyy-MM-dd HH:mm K", CultureInfo.InvariantCulture)
              let authorDate = commit.AuthorDate.ToString("yyyy-MM-dd HH:mm K", CultureInfo.InvariantCulture)
              match commit with
              | :? BundleSeed as seed ->
                cpx $" \fg+ {shortId} \fc{commitDate}\f0"
                if authorDate <> commitDate then
                  cpx $" (\fk{authorDate}\f0)"
                cpx ":"
                for r in seed.Refs |> Seq.sort do
                  let rtxt = r |> refText
                  cpx $" {rtxt}"
                cp "\f0."
              | :? BundlePrerequisite as prerequisite ->
                cpx $" \fo- {shortId} \fy{commitDate}\f0"
                if authorDate <> commitDate then
                  cpx $" (\fk{authorDate}\f0)"
                cpx ":"
                for r in prerequisite.Labels |> Seq.sort do
                  let rtxt = r |> refText
                  cpx $" {rtxt}"
                cp "\f0."
              | _ ->
                failwith "Unexpected commit info type"
          ()
        else
          status <- 1
          cp $"\foSkipping further processing of this anchor\f0."
    status

let private runDeltaSend args =
  match getContext true with
  | None ->
    // error printed already
    1
  | Some(context) ->
    let recipes = context.RecipesOption.Value
    let requireRecipe = recipes.HasDefaultRecipe |> not
    let oo = args |> parseRecipeOrAll {
      RecipeOrAll = None
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
    let oo = args |> parseRecipeOnly false {
      Recipe = null
    }
    match oo with
    | None ->
      cp ""
      Usage.usage "delta"
      1
    | Some o ->
      let recipeOption = o.Recipe |> tryGetRecipe recipes
      match recipeOption with
      | Some recipe ->
        recipe |> showRecipe
        0
      | None ->
        // error printed already
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
        let version = if recipe.V2 then "\fbv2" else "\fwv1"
        cp $" {defaultText}  '\fg{recipe.Name}\f0'  ({version}\f0, \fc+{recipe.Seeds.Count}\f0, \fo-{recipe.Exclusions.Count}\f0)."
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
