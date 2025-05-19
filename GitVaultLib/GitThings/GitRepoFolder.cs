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
  /// Try to load the gitvault settings for this repository.
  /// Returns null if this repo has not been initialized for use
  /// with gitvault yet.
  /// </summary>
  public RepoSettings? TryLoadGitVaultSettings()
  {
    return RepoSettings.TryLoad(this);
  }

  /// <summary>
  /// Build a GitVaultSettings object for this repository and
  /// save it. Returns null on success, or an error message
  /// in case of failure.
  /// </summary>
  /// <param name="centralSettings"></param>
  /// <param name="keyinfo"></param>
  /// <param name="vaultAnchor"></param>
  /// <param name="bundleAnchor"></param>
  /// <param name="hostName"></param>
  /// <param name="repoName"></param>
  /// <returns></returns>
  public string? TryInitGitVaultSettings(
    CentralSettings centralSettings,
    Zkey keyinfo,
    string vaultAnchor,
    string bundleAnchor = "default",
    string? hostName = null,
    string? repoName = null)
  {
    var existingSettings = TryLoadGitVaultSettings();
    if(existingSettings != null)
    {
      return $"GitVault is already initialized for this repository.";
    }
    if(!centralSettings.Anchors.ContainsKey(vaultAnchor))
    {
      return $"Vault anchor '{vaultAnchor}' is not defined.";
    }
    var vaultRoot = centralSettings.Anchors[vaultAnchor];
    if(!Directory.Exists(vaultRoot))
    {
      return $"Vault anchor folder '{vaultRoot}' does not exist.";
    }
    if(!centralSettings.BundleAnchors.ContainsKey(bundleAnchor))
    {
      return $"Bundle anchor '{bundleAnchor}' is not defined.";
    }
    var bundleRoot = centralSettings.BundleAnchors[bundleAnchor];
    if(!Directory.Exists(bundleRoot))
    {
      return $"Bundle anchor folder '{bundleRoot}' does not exist.";
    }
    if(String.IsNullOrEmpty(hostName))
    {
      hostName = centralSettings.DefaultHostname;
    }
    if(!CentralSettings.IsValidName(hostName, false))
    {
      return $"Host name '{hostName}' is not valid for use with GitVault. " +
        "Only letters, digits, '-' and '_' are allowed.";
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
        "Only letters, digits, '-', '_' and '.' are allowed.";
    }
    var bundleFolder = Path.Combine(
      bundleRoot,
      repoName);
    var vaultFolder = Path.Combine(
      vaultRoot,
      repoName);
    if(!Directory.Exists(bundleFolder))
    {
      Directory.CreateDirectory(bundleFolder);
    }
    if(!Directory.Exists(vaultFolder))
    {
      Directory.CreateDirectory(vaultFolder);
    }
    var settings = new RepoSettings(
      hostName,
      repoName,
      vaultFolder,
      bundleFolder,
      keyinfo);
    settings.Save(this);
    return null;
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
