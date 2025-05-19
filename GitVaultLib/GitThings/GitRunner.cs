/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
}
