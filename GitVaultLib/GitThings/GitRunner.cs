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

namespace GitVaultLib.GitThings;

/// <summary>
/// Static utilities for running git commands.
/// </summary>
public static class GitRunner
{
  /// <summary>
  /// Run a (git) command and return the output as a list of lines.
  /// </summary>
  /// <param name="args">
  /// Arguments to pass to the command.
  /// </param>
  /// <param name="workingDirectory">
  /// Working directory (default: current directory).
  /// </param>
  /// <param name="command">
  /// The command to run (default: "git").
  /// </param>
  /// <returns></returns>
  public static GitRunResult RunToLines(
    IEnumerable<string> args,
    string? workingDirectory,
    string command = "git")
  {
    var startInfo = new ProcessStartInfo {
      FileName = command,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
      WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
    };
    var result = new GitRunResult();
    foreach(var arg in args)
    {
      startInfo.ArgumentList.Add(arg);
      result.Arguments.Add(arg);
    }
    using(var process = new Process { StartInfo = startInfo })
    {
      process.OutputDataReceived += (sender, e) => {
        if(e.Data != null)
        {
          result.OutputLines.Add(e.Data);
        }
      };
      process.ErrorDataReceived += (sender, e) => {
        if(e.Data != null)
        {
          result.ErrorLines.Add(e.Data);
        }
      };
      process.Start();
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();
      process.WaitForExit();
      // The process is closed next, which may cause extra lines
      // to be flushed. Only after that we can be sure that all
      // output has been received.
      result.StatusCode = process.ExitCode;
    }
    return result;
  }

  /// <summary>
  /// Retrieve the git remotes for the current repository
  /// </summary>
  /// <param name="workingDirectory">
  /// The folder to derive the repository from. If null, the
  /// current directory is used.
  /// </param>
  /// <param name="status">
  /// Status of the command. 0 if successful, otherwise
  /// another number.
  /// </param>
  /// <returns>
  /// The created GitRemotes object, or null if the command
  /// failed with non-zero status.
  /// </returns>
  public static GitRemotes? GetRemotes(
    string? workingDirectory,
    out GitRunResult status)
  {
    status = RunToLines(
      ["remote", "-v"],
      workingDirectory);
    if(status.StatusCode != 0)
    {
      return null;
    }
    return GitRemotes.FromLines(status.OutputLines);
  }

  /// <summary>
  /// Create a new bare repository in the specified folder.
  /// </summary>
  public static GitRunResult CreateBareRepository(string folder)
  {
    var args = new List<string> {
      "init",
      "--bare",
      folder
    };
    var result = RunToLines(args, null);
    return result;
  }

  /// <summary>
  /// Add a remote to the current repository.
  /// </summary>
  public static GitRunResult AddRemote(
    string workingDirectory,
    string remoteName,
    string remoteTarget)
  {
    var args = new List<string> {
      "remote",
      "add",
      remoteName,
      remoteTarget
    };
    var result = RunToLines(args, workingDirectory);
    return result;
  }

  /// <summary>
  /// Fetch a remote by remote name
  /// </summary>
  public static GitRunResult FetchRemote(
    string workingDirectory,
    string remoteName)
  {
    var args = new List<string> {
      "fetch",
      //"-v",
      remoteName,
    };
    var result = RunToLines(args, workingDirectory);
    return result;
  }

  /// <summary>
  /// Enumerate the root commits of the repository that
  /// <paramref name="witnessFolder"/> is part of.
  /// </summary>
  /// <param name="witnessFolder">
  /// Any folder that is part of the repository. Or null to use
  /// the current directory as the witness folder.
  /// </param>
  /// <returns>
  /// A GitRunResult containing the output of the command.
  /// </returns>
  public static GitRunResult EnumRoots(string? witnessFolder)
  {
    if(string.IsNullOrEmpty(witnessFolder))
    {
      witnessFolder = Environment.CurrentDirectory;
    }
    else
    {
      witnessFolder = Path.GetFullPath(witnessFolder);
    }
    var args = new List<string> {
      "-C",
      witnessFolder,
      "rev-list",
      "--max-parents=0",
      "--all"
    };
    var result = RunToLines(args, null);
    return result;
  }

  /// <summary>
  /// Enumerate the commit IDs plus ref names for the tips of branches and tags in the repository.
  /// </summary>
  public static GitRunResult EnumTips(string? witnessFolder)
  {
    if(string.IsNullOrEmpty(witnessFolder))
    {
      witnessFolder = Environment.CurrentDirectory;
    }
    else
    {
      witnessFolder = Path.GetFullPath(witnessFolder);
    }
    var args = new List<string> {
      "-C",
      witnessFolder,
      "show-ref",
      "--branches",
      "--tags"
    };
    var result = RunToLines(args, null);
    return result;
  }

  /// <summary>
  /// Enumerate the tips/heads of a git bundle file.
  /// </summary>
  public static GitRunResult TipsFromBundle(string bundleFile)
  {
    var args = new List<string> {
      "bundle",
      "list-heads",
      bundleFile
    };
    var result = RunToLines(args, null);
    return result;
  }

  /// <summary>
  /// Create a bundle file containing all branches and tags.
  /// If the output file already exists, it is moved to a backup file
  /// </summary>
  /// <param name="bundleFile">
  /// The output file.
  /// </param>
  /// <param name="witnessFolder">
  /// Any folder that is part of the repository. If null or empty,
  /// Environment.CurrentDirectory is used as the witness folder.
  /// </param>
  /// <returns></returns>
  public static GitRunResult CreateBundle(
    string bundleFile, string? witnessFolder)
  {
    if(string.IsNullOrEmpty(witnessFolder))
    {
      witnessFolder = Environment.CurrentDirectory;
    }
    else
    {
      witnessFolder = Path.GetFullPath(witnessFolder);
    }
    var tmpFile = bundleFile + ".tmp";
    var args = new List<string> {
      "-C",
      witnessFolder,
      "bundle",
      "create",
      tmpFile,
      "--branches",
      "--tags",
    };
    var result = RunToLines(args, null);
    if(result.StatusCode != 0)
    {
      return result; // Return early on error
    }
    // If backup is true, we need to move the tmp file to the target
    // and then rename it to the final bundle file.
    if(File.Exists(bundleFile))
    {
      var backupFile = bundleFile + ".bak";
      if(File.Exists(backupFile))
      {
        // If the backup file already exists, delete it
        File.Delete(backupFile);
      }
      File.Replace(tmpFile, bundleFile, backupFile);
    }
    else
    {
      File.Move(tmpFile, bundleFile);
    }
    return result;
  }

  /// <summary>
  /// Create a bundle file containing the objects implied by <paramref name="revListArgs"/>.
  /// </summary>
  /// <param name="bundleFile">
  /// The name of the bundle file to create
  /// </param>
  /// <param name="witnessFolder">
  /// If not null: a folder within the target repository. Defaults to
  /// the current directory.
  /// </param>
  /// <param name="revListArgs">
  /// Revisions, branches, tags, refs etc. to include. Or to Exclude, when prefixed with '^'.
  /// Also accepts other filter options as documented for <c>git bundle</c>
  /// (see https://git-scm.com/docs/git-bundle).
  /// </param>
  /// <returns></returns>
  /// <remarks>
  /// Example 1 (same as <see cref="CreateBundle(string, string?)"/>):
  /// <code>["--branches", "--tags"]</code>
  /// Example 2 (all 'release/*' branches, but exclude anything reachable from the
  /// 'main' branch):
  /// <code>["^refs/heads/main", "--glob", "refs/heads/release/*"]</code>
  /// </remarks>
  public static GitRunResult CreateBundle(
    string bundleFile,
    string? witnessFolder,
    IEnumerable<string> revListArgs)
  {
    if(string.IsNullOrEmpty(witnessFolder))
    {
      witnessFolder = Environment.CurrentDirectory;
    }
    else
    {
      witnessFolder = Path.GetFullPath(witnessFolder);
    }
    var tmpFile = bundleFile + ".tmp";
    var args = new List<string> {
      "-C",
      witnessFolder,
      "bundle",
      "create",
      tmpFile,
    };
    args.AddRange(revListArgs);
    var result = RunToLines(args, null);
    if(result.StatusCode != 0)
    {
      return result; // Return early on error
    }
    // If backup is true, we need to move the tmp file to the target
    // and then rename it to the final bundle file.
    if(File.Exists(bundleFile))
    {
      var backupFile = bundleFile + ".bak";
      if(File.Exists(backupFile))
      {
        // If the backup file already exists, delete it
        File.Delete(backupFile);
      }
      File.Replace(tmpFile, bundleFile, backupFile);
    }
    else
    {
      File.Move(tmpFile, bundleFile);
    }
    return result;
  }
}
