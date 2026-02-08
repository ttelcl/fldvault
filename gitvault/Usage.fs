// (c) 2025  ttelcl / ttelcl
module Usage

open CommonTools
open ColorPrint

let usage focus =
  let showSection section =
    focus = "" || focus = section || focus = "all"
  let showDetail section =
    focus = section || focus = "all"
  if showSection "" then
    cp "\foGit encrypted bundle tool\f0"
    cp ""
  if showSection "settings-show" then
    cp "\fogitvault \fysettings\f0"
    cp "  Show settings for this machine"
    cp "  (aliases: \fogitvault \fysettings show\f0,  \fogitvault \fyanchors\f0)"
  if showDetail "settings-show" then
    cp ""
  if showSection "anchor-add" then
    cp "\fogitvault \fyanchor add \f0[\fg-a \fcanchorname\f0] \fg-f \fcanchorfolder\f0"
    cp "  Register a new anchor: a folder in which your encrypted bundles (vaults) will be"
  if showDetail "anchor-add" then
    cp "  \fg-a \fcanchorname     \f0Name of the anchor (that you use as an alias for the folder)"
    cp "                    If \fcanchorfolder\f0 ends with \fy/\fcname\fy.gitvault\f0 or \fy/\fygitvault.\fcname\f0 this is optional"
    cp "  \fg-f \fcanchorfolder   \f0Path to the folder. Vault anchor folders should be on a cloud-backed folder"
    cp "                    A child folder named \fcanchorname\fy.gitvault\f0 will be created in this folder"
    cp "                    and used as the actual anchor folder, unless:"
    cp "                    - \fcanchorfolder\f0 already ends with '\fy/\fcname\fy.gitvault\f0'"
    cp "                    - \fcanchorfolder\f0 ends with '\fy/GitVault\f0'"
    cp ""
  if showSection "repo-init" then
    cp "\fogitvault \fyrepo init \fg-a \fcanchorname\f0[\fo::\fcreponame\f0] [\fg-host \fchostname\f0] [\fg-f \fcwitnessfolder\f0]"
    cp "  Initialize an existing git repository for use with gitvault. Note that key initialization is separate"
  if showDetail "repo-init" then
    cp "  \fg-a \fcanchorname     \f0Alias for the vault anchor folder in which the repo vaults will be published"
    cp "  \fo   ::\fcreponame     \f0Name of the repository (default: derived from the current repo folder name as set via \fg-f\f0)"
    cp "  \fg-f \fcwitnessfolder  \f0Path to any folder in the git repository. (default: current directory)"
    cp "  \fg-host \fchostname    \f0Name used as 'host' (default: the value set as default host name for this computer)"
    cp ""
  if showSection "push" then // the actual name was changed from "push" to "send"
    cp "\fogitvault \fysend \f0[\fg-all\f0] [\fg-full\f0]"
    cp "  Update the bundle(s) and encrypt the updated bundle(s) for the current repo to a new vault(s)."
    cp "  There can be multiple anchors connected; this command will push to all of them."
    cp "  (alias: \fogitvault \fyrepo send\f0)"
  if showDetail "push" then
    cp "  \fg-all\f0              Send to all anchors - this is the default behavior, and this flag is currently a dummy."
    cp "  \fg-full\f0             Confirm full bundle mode. Optional if no delta recipes exist, required otherwise."
    cp "  \fx\fx                  (this option exists to avoid accidentally running this command when you intended \fogitvault delta send\f0)"
    cp ""
  if showSection "delta" && not(showDetail "delta") then
    cp "\fogitvault \fydelta\f0 \fmsubcommand \fg...\f0"
    cp "  Delta recipe management and use: tooling for creating partial bundles."
    cp "  See '\fogitvault \fydelta \fg-h\f0' for more details."
  if showDetail "delta" then
    cp "\fogitvault \fydelta\f0 \fmsubcommand \fg...\f0"
    cp "  'Delta recipe' management and use: tooling for creating partial bundles."
    cp ""
    cp "\fogitvault \fydelta new\f0 \fg-r \fcrecipe\f0 \fg-s \fcseed \fg-x \fcexclusion\f0"
    cp "\fogitvault \fydelta edit\f0 [\fg-r \fcrecipe\f0] \fg-s \fcseed \fg-x \fcexclusion\f0 \fg-z \fczapref\f0"
    cp "  Create or modify a delta bundle recipe"
    cp "  \fg-s \fcseed\f0        A git branch, tag or other ref to include. Can also be \fo--all\f0, \fo--branches\f0 or"
    cp "  \fx   \fx    \fx        \fo--tags\f0 to include all refs, local branches, and/or tags."
    cp "  \fg-x \fcexclusion\f0   A branch, tag or other ref to exclude (assuming that ref to be known)."
    cp "  \fx   \fx         \fx   This can also be a commit ID."
    cp "  \fg-z \fczapref\f0      A seed or exclusion to remove (in \fyedit\f0 only)"
    cp "\fogitvault \fydelta list\f0"
    cp "  List the delta recipes for this repository."
    cp "\fogitvault \fydelta show\f0 \fg-r \fcrecipe\f0"
    cp "  Show the seeds and exclusions for \fcrecipe\f0."
    cp "\fogitvault \fydelta drop\f0 \fg-r \fcrecipe\f0"
    cp "  Delete the specified \fcrecipe\f0."
    cp "\fogitvault \fydelta default\f0 [\fg-r \fcrecipe\f0|\fg-clear\f0]"
    cp "  Change which recipe is the default, or clear the existing default."
    cp "\fogitvault \fydelta send\f0 [\fg-r \fcrecipe\f0]"
    cp "  Run the recipe, creating the delta bundle, and if possible also its vault file."
    cp ""
  if showSection "bundles-fetch" then // name changed to "ingest"
    cp "\fogitvault \fyingest \f0[\fg-f \fcwitnessfolder\f0|\fg-a \fcanchorname\f0[\fo::\fcreponame\f0]|\fg-all\f0]"
    cp "  Decrypt all incoming vaults for the current repo to their bundles."
    cp "  (alias: \fogitvault \fybundles ingest\f0)"
  if showDetail "bundles-fetch" then
    cp "  \fg-f \fcwitnessfolder  \f0          Path to any folder in the git repository"
    cp "  \fg-a \fcanchorname\f0[\fo::\fcreponame\f0]]  Vault anchor plus optional repo name"
    cp "  \fg-all \fx\f0                       Process all repos in all anchors"
    cp "  (without \fg-f\f0, \fg-a\f0, or \fg-all\f0 the effect is equivalent to '\fg-f \fc.\f0')"
    cp ""
  if showSection "bundles-status" then
    cp "\fogitvault \fystatus \f0[\fg-f \fcwitnessfolder\f0|\fg-a \fcanchorname\f0[\fo::\fcreponame\f0]|\fg-all\f0]"
    cp "  Show status information for all bundles / vaults for the current repository."
    cp "  (alias: \fogitvault \fybundles status\f0)"
  if showDetail "bundles-status" then
    cp "  \fg-f \fcwitnessfolder  \f0          Path to any folder in the git repository"
    cp "  \fg-a \fcanchorname\f0[\fo::\fcreponame\f0]]  Vault anchor plus optional repo name"
    cp "  \fg-all \fx\f0                       Process all repos in all anchors"
    cp "  (without \fg-f\f0, \fg-a\f0, or \fg-all\f0 the effect is equivalent to '\fg-f \fc.\f0')"
    cp ""
  if showSection "bundles-connect" then
    cp "\fogitvault \fyconnect \f0[\fg-all\f0|\fg-a \fcanchorname\f0[\fo.\fchostname\f0]] [\fg-fetch\f0]"
    cp "\fogitvault \fyreceive \f0[\fg-all\f0|\fg-a \fcanchorname\f0[\fo.\fchostname\f0]] [\fg-nofetch\f0]"
    cp "  Make sure there is a git 'remote' defined for the given anchor+host combination(s)"
    cp "  Optionally '\fmgit fetch\f0' them."
    cp "  (use \fogitvault status\f0 to list anchors and hosts for this repo)"
  if showDetail "bundles-connect" then
    cp "  \fg-all \fx\fx\f0                    Create missing remotes all hosts in all anchors. This is default for \fogitvault receive\f0."
    cp "  \fg-a \fcanchorname\f0\fo.\fchostname\f0   Create the remote if missing"
    cp "  \fg-a \fcanchorname\f0\f0            Create missing remotes for all hosts"
    cp "  \fg-nofetch \fx\f0\f0                Do not fetch the remote(s). This is default for \fogitvault connect\f0."
    cp "  \fg-fetch \fx\f0\f0                  Also fetch the remote(s). This is default for \fogitvault receive\f0."
    cp ""
  if showSection "bundle" then
    cp "\fogitvault \fybundle \f0[\fg-f \fcbundlefile\f0|\frTBD\f0]"
    cp "  Describe seeds and requirements of a bundle file"
    cp "  (aliases: \fogitvault \fybundle info\f0,  \fogitvault \fybundleinfo\f0)"
  cp "\fg-v               \f0Verbose mode"



