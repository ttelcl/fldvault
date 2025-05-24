// (c) 2025  ttelcl / ttelcl
module Usage

open CommonTools
open ColorPrint

let usage focus =
  let showSection section =
    focus = "" || focus = section
  if showSection "" then
    cp "\foGit encrypted bundle tool\f0"
    cp ""
  if showSection "settings-show" then
    cp "\fogitvault \fysettings show\f0"
    cp "  Show settings for this machine"
    cp ""
  if showSection "anchor-add" then
    cp "\fogitvault \fyanchor add \f0[\fg-a \fcanchorname\f0] \fg-f \fcanchorfolder\f0"
    cp "  Register a new anchor: a folder in which your encrypted bundles (vaults) will be"
    cp "  \fg-a \fcanchorname     \f0Name of the anchor (that you use as an alias for the folder)"
    cp "                    If \fcanchorfolder\f0 ends with \fy/\fcname\fy.gitvault\f0 or \fy/\fygitvault.\fcname\f0 this is optional"
    cp "  \fg-f \fcanchorfolder   \f0Path to the folder. Vault anchor folders should be on a cloud-backed folder"
    cp "                    A child folder named \fcanchorname\fy.gitvault\f0 will be created in this folder"
    cp "                    and used as the actual anchor folder, unless:"
    cp "                    - \fcanchorfolder\f0 already ends with '\fy/\fcname\fy.gitvault\f0'"
    cp "                    - \fcanchorfolder\f0 ends with '\fy/GitVault\f0'"
    cp ""
  if showSection "repo-init" then
    cp "\fogitvault \fyrepo init \fg-a \fcanchorname\f0[\fo::\fcreponame\f0] [\fg-host \fchostname\f0] [\fg-f \fcwitnessfolder\f0] [\fg-b \fcbundleanchor\f0]"
    cp "  Initialize an existing git repository for use with gitvault. Note that key initialization is separate"
    cp "  \fg-a \fcanchorname     \f0Alias for the vault anchor folder in which the repo vaults will be published"
    cp "  \fo   ::\fcreponame     \f0Name of the repository (default: derived from the current repo folder name as set via \fg-f\f0)"
    cp "  \fg-f \fcwitnessfolder  \f0Path to any folder in the git repository. (default: current directory)"
    cp "  \fg-host \fchostname    \f0Name used as 'host' (default: the value set as default host name for this computer)"
    cp "  \fg-b \fcbundleanchor   \f0Name of the bundle anchor folder below which the raw bundles will be stored. Default '\fodefault\f0'."
    cp ""
  cp "\fg-v               \f0Verbose mode"



