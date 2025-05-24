/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FldVault.Core.Vaults;

using GitVaultLib.Configuration;
using GitVaultLib.VaultThings;

namespace GitVaultLib.GitThings;

/// <summary>
/// The potential outcomes of a test for being a git repo.
/// </summary>
public enum GitRepoTestResult
{
  /// <summary>
  /// The folder is not a repository (but may yet be inside one).
  /// </summary>
  NotAGitRepo = 0,

  /// <summary>
  /// The repository is a working copy.
  /// </summary>
  WorkingCopy = 1,

  /// <summary>
  /// The repository is a bare repository.
  /// </summary>
  Bare = 2,
}

/// <summary>
/// Represents a folder that is a git repository root folder.
/// It can be either a bare repository or a working copy.
/// Use <see cref="LocateRepoRootFrom(string)"/> to get instances
/// of this class.
/// </summary>
public class GitRepoFolder
{
  private GitRoots? _cachedGitRoots;

  /// <summary>
  /// Create a new GitRepoFolder.
  /// Called via <see cref="LocateRepoRootFrom(string)"/>.
  /// </summary>
  private GitRepoFolder(
    string folder)
  {
    folder = Path.GetFullPath(folder).TrimEnd('/', '\\');
    Folder = folder;
    if(!Directory.Exists(folder))
    {
      throw new ArgumentException(
        $"Folder '{folder}' does not exist.");
    }
    if(IsGitFolder(folder))
    {
      GitFolder = folder;
    }
    else
    {
      GitFolder = Path.Combine(
        folder,
        ".git");
      if(!Directory.Exists(GitFolder))
      {
        throw new ArgumentException(
          $"Folder '{folder}' is not a git repository (neither bare, nor has a .git subfolder)");
      }
      if(!IsGitFolder(GitFolder))
      {
        throw new ArgumentException(
          $"Folder '{GitFolder}' is not a git folder (despite its name).");
      }
    }
    var repoName = Path.GetFileName(Folder);
    if(repoName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
    {
      repoName = repoName.Substring(0, repoName.Length - 4);
    }
    AutoRepoName = repoName;
  }

  /// <summary>
  /// The root folder of the repository
  /// </summary>
  public string Folder { get; }

  /// <summary>
  /// The folder where the git repository itself is stored.
  /// This is either the same as <see cref="Folder"/> (for bare
  /// repositories) or a subfolder named ".git" (for normal
  /// repositories).
  /// </summary>
  public string GitFolder { get; }

  /// <summary>
  /// The repo name as derived from the git repository folder.
  /// </summary>
  public string AutoRepoName { get; }

  /// <summary>
  /// Try to load the gitvault settings, including the settings for
  /// this repository.
  /// Returns null if this repo has not been initialized for use
  /// with gitvault yet.
  /// </summary>
  public RepoSettings? TryLoadGitVaultSettings()
  {
    return RepoSettings.TryLoad(this);
  }

  /// <summary>
  /// The file name where the gitvault settings for this git repository
  /// are stored
  /// </summary>
  public string GitvaultSettingsFile {
    get {
      return Path.Combine(
        GitFolder,
        "gitvault-settings.json");
    }
  }

  /// <summary>
  /// Get the git roots for this repository. This is cached at first call.
  /// </summary>
  public GitRoots GetGitRoots()
  {
    if(_cachedGitRoots == null)
    {
      _cachedGitRoots = GitRoots.ForRepository(Folder);
    }
    return _cachedGitRoots;
  }

  /// <summary>
  /// True if the git roots of this repository are compatible with the
  /// one of the given <paramref name="repoVaultFolder"/>. That includes
  /// the case where either root set is empty.
  /// </summary>
  public bool GitRootsCompatible(RepoVaultFolder repoVaultFolder)
  {
    return repoVaultFolder.GitRootsCompatible(this);
  }

  /// <summary>
  /// Build a GitVaultSettings object for this repository and
  /// save it. Returns null on success, or an error message
  /// in case of failure.
  /// </summary>
  /// <param name="centralSettings">
  /// The gitvault central settings.
  /// </param>
  /// <param name="repoSettings">
  /// Upon success, this will contain the created repo settings object.
  /// In case of failure because the settings already exist, this will
  /// contain the existing settings object instead.
  /// In all other cases, this will be null.
  /// </param>
  /// <param name="vaultAnchor">
  /// The key name of the vault anchor to use in the settings.
  /// </param>
  /// <param name="bundleAnchor">
  /// The key name of the bundle anchor to use in the settings.
  /// Defaults to "default", which is automatically created upon
  /// setting up the central settings.
  /// </param>
  /// <param name="hostName">
  /// The host name to use in the settings. Defaults to the
  /// default host name defined in <paramref name="centralSettings"/>.
  /// </param>
  /// <param name="repoName">
  /// The repository name to use in the settings. Defaults to the name
  /// derived from this instance's <see cref="Folder"/> property.
  /// </param>
  /// <returns></returns>
  public string? TryInitGitVaultSettings(
    CentralSettings centralSettings,
    string vaultAnchor,
    out AnchorRepoSettings? repoSettings,
    string bundleAnchor = "default",
    string? hostName = null,
    string? repoName = null)
  {
    var existingSettings = TryLoadGitVaultSettings();
    if(existingSettings != null)
    {
      if(existingSettings.ByAnchor.TryGetValue(vaultAnchor, out repoSettings))
      {
        return $"GitVault is already initialized for this repository";
      }
    }
    else
    {
      existingSettings = new RepoSettings();
    }
    repoSettings = null;
    if(!centralSettings.Anchors.ContainsKey(vaultAnchor))
    {
      return $"Vault anchor '{vaultAnchor}' is not defined";
    }
    var vaultRoot = centralSettings.Anchors[vaultAnchor];
    if(!Directory.Exists(vaultRoot))
    {
      return $"Vault anchor folder '{vaultRoot}' does not exist";
    }
    if(!centralSettings.BundleAnchors.ContainsKey(bundleAnchor))
    {
      return $"Bundle anchor '{bundleAnchor}' is not defined";
    }
    var bundleRoot = centralSettings.BundleAnchors[bundleAnchor];
    if(!Directory.Exists(bundleRoot))
    {
      return $"Bundle anchor folder '{bundleRoot}' does not exist";
    }
    if(String.IsNullOrEmpty(hostName))
    {
      hostName = centralSettings.DefaultHostname;
    }
    if(!CentralSettings.IsValidName(hostName, false))
    {
      return $"Host name '{hostName}' is not valid for use with GitVault. " +
        "Only letters, digits, '-' and '_' are allowed";
    }
    if(String.IsNullOrEmpty(repoName))
    {
      repoName = Path.GetFileName(Folder);
      if(repoName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
      {
        repoName = repoName.Substring(0, repoName.Length - 4);
      }
    }
    if(!CentralSettings.IsValidName(repoName, true))
    {
      return $"Repository name '{repoName}' is not valid for use with GitVault. " +
        "Only letters, digits, '-', '_' and '.' are allowed";
    }
    var repoVaultFolder = new RepoVaultFolder(
      vaultRoot,
      repoName);
    var bundleFolder = Path.Combine(
      bundleRoot,
      vaultAnchor, // repos are only unique per vault anchor, so include it in the bundle path
      repoName);
    var vaultFolder = repoVaultFolder.VaultFolder;
    if(!Directory.Exists(bundleFolder))
    {
      Directory.CreateDirectory(bundleFolder);
    }
    if(!Directory.Exists(vaultFolder))
    {
      Directory.CreateDirectory(vaultFolder);
    }
    repoSettings = new AnchorRepoSettings(
      hostName,
      repoName,
      bundleAnchor);
    repoSettings.VaultAnchor = vaultAnchor;
    existingSettings.ByAnchor[vaultAnchor] = repoSettings;
    existingSettings.Save(this);
    return null;
  }

  /// <summary>
  /// Build a GitVaultSettings object for this repository and
  /// save it. Returns null on success, or an error message
  /// in case of failure. Same as 
  /// <see cref="TryInitGitVaultSettings(CentralSettings, string, out AnchorRepoSettings?, string, string?, string?)"/>,
  /// but with the out var as last argument, to be more F# friendly.
  /// </summary>
  public string? TryInitGitVaultSettings(
    CentralSettings centralSettings,
    string vaultAnchor,
    string? bundleAnchor,
    string? hostName,
    string? repoName,
    out AnchorRepoSettings? repoSettings)
  {
    bundleAnchor ??= "default"; // default bundle anchor
    return TryInitGitVaultSettings(
      centralSettings,
      vaultAnchor,
      out repoSettings,
      bundleAnchor,
      hostName,
      repoName);
  }


  /// <summary>
  /// Test if the folder looks like a git folder: it contains
  /// a "objects" and a "refs" folder, and a "config" file.
  /// </summary>
  public static bool IsGitFolder(string folder)
  {
    if(!Directory.Exists(folder))
    {
      return false;
    }
    var objectsFolder = Path.Combine(
      folder,
      "objects");
    var refsFolder = Path.Combine(
      folder,
      "refs");
    var configFile = Path.Combine(
      folder,
      "config");
    if(Directory.Exists(objectsFolder) &&
       Directory.Exists(refsFolder) &&
       File.Exists(configFile))
    {
      return true;
    }
    return false;
  }

  /// <summary>
  /// Return true if the folder is either a bare repository or a
  /// folder that contains a '.git' subfolder that is a git folder.
  /// </summary>
  public static GitRepoTestResult IsGitRootFolder(string folder)
  {
    if(!Directory.Exists(folder))
    {
      return GitRepoTestResult.NotAGitRepo;
    }
    if(Path.GetFileName(folder).Equals(".git", StringComparison.OrdinalIgnoreCase))
    {
      return GitRepoTestResult.NotAGitRepo;
    }
    if(IsGitFolder(folder))
    {
      // This is a git folder and a root folder at the same time.
      // i.e. it is a bare repository.
      return GitRepoTestResult.Bare;
    }
    var gitFolder = Path.Combine(
      folder,
      ".git");
    return Directory.Exists(gitFolder) && IsGitFolder(gitFolder)
      ? GitRepoTestResult.WorkingCopy
      : GitRepoTestResult.NotAGitRepo;
  }

  /// <summary>
  /// Locate the root folder of a git repository by walking up
  /// the folder tree. This will return the first folder that
  /// is a git repository (bare or working copy).
  /// </summary>
  public static GitRepoFolder? LocateRepoRootFrom(string startFolder)
  {
    if(string.IsNullOrEmpty(startFolder))
    {
      return null;
    }
    startFolder = Path.GetFullPath(startFolder);
    if(!Directory.Exists(startFolder))
    {
      return null;
    }
    if(IsGitRootFolder(startFolder) != GitRepoTestResult.NotAGitRepo)
    {
      return new GitRepoFolder(startFolder);
    }
    var parentFolder = Path.GetDirectoryName(startFolder);
    if(parentFolder == null)
    {
      return null;
    }
    return LocateRepoRootFrom(parentFolder);
  }
}
