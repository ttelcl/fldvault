/*
 * (c) 2023  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FldVault.Core.Vaults
{
  /// <summary>
  /// Breaks down the name of *.key-info files into contituent parts
  /// </summary>
  public class KeyInfoName
  {
    private const string __rgxKind = @"^[A-Za-z][A-Za-z0-9]*$";
    private const string __rgxTag = @"^[A-Za-z0-9]+([-_][A-Za-z0-9]+)*$";

    /// <summary>
    /// Create a new KeyInfoName
    /// </summary>
    public KeyInfoName(
      Guid keyId,
      string kind,
      string? tag = null)
    {
      KeyId = keyId;
      Kind = kind;
      if(!Regex.IsMatch(kind, __rgxKind))
      {
        throw new ArgumentOutOfRangeException(nameof(kind),
          $"Nonconforming key-info kind '{kind}'");
      }
      Tag = String.IsNullOrEmpty(tag) ? null : tag;
      if(tag != null && !Regex.IsMatch(tag, __rgxTag))
      {
        throw new ArgumentOutOfRangeException(
          nameof(tag), $"Nonconforming key-info tag: '{tag}'");
      }
    }

    /// <summary>
    /// Create a KeyInfoName from the name of a conforming *.key-info file
    /// </summary>
    /// <param name="filePath">
    /// The path to the file. This can contain a directory part, but only the file part is used.
    /// </param>
    /// <returns>
    /// A new KeyInfoName object
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The name was not conforming.
    /// </exception>
    public static KeyInfoName FromFile(string filePath)
    {
      var shortName = Path.GetFileName(filePath);
      var parts = shortName.Split('.');
      if(parts.Length < 3)
      {
        throw new ArgumentException(
          $"Nonconforming key-info file name (too few segments): '{shortName}'");
      }
      else if(parts.Length > 4)
      {
        throw new ArgumentException(
          $"Nonconforming key-info file name (too many segments): '{shortName}'");
      }
      else
      {
        var id = Guid.ParseExact(parts[0], "D");
        if(parts[^1] != "key-info")
        {
          throw new ArgumentException(
            "Expecting the argument file to have the extension '.key-info'");
        }
        if(parts.Length == 3)
        {
          return new KeyInfoName(id, parts[1]);
        }
        else if(parts.Length == 4)
        {
          return new KeyInfoName(id, parts[2], parts[1]);
        }
        else
        {
          throw new InvalidOperationException("This cannot happen");
        }
      }
    }

    /// <summary>
    /// Try to create a KeyInfoName from the name of a *.key-info file,
    /// returning null if the file name doesn't conform to the expected
    /// naming convention
    /// </summary>
    /// <param name="filePath">
    /// The path to the file. This can contain a directory part, but only the file part is used.
    /// </param>
    /// <returns>
    /// A new KeyInfoName object, or null if the name was conforming
    /// </returns>
    public static KeyInfoName? TryFromFile(string filePath)
    {
      var shortName = Path.GetFileName(filePath);
      var parts = shortName.Split('.');
      if(parts.Length < 3)
      {
        return null;
      }
      else if(parts.Length > 4)
      {
        return null;
      }
      else
      {
        if(!Guid.TryParseExact(parts[0], "D", out var id))
        {
          return null;
        }
        if(parts[^1] != "key-info")
        {
          return null;
        }
        if(parts.Length == 3)
        {
          var kind = parts[1];
          if(!Regex.IsMatch(kind, __rgxKind))
          {
            return null;
          }
          return new KeyInfoName(id, kind);
        }
        else if(parts.Length == 4)
        {
          var kind = parts[2];
          var tag = parts[1];
          if(!Regex.IsMatch(kind, __rgxKind) || !Regex.IsMatch(tag, __rgxTag))
          {
            return null;
          }
          return new KeyInfoName(id, kind, tag);
        }
        else
        {
          return null;
        }
      }
    }

    /// <summary>
    /// The ID of the key the key-info file provides information on
    /// </summary>
    public Guid KeyId { get; init; }

    /// <summary>
    /// The kind of key source. Should be one of the KeyKind constants
    /// (*.{Kind}.key-info is the double file extension)
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// The tag string, or null or empty if missing.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Reconstruct the file name (without path)
    /// </summary>
    public string FileName {
      get => String.IsNullOrEmpty(Tag)
        ? $"{KeyId}.{Kind}.key-info"
        : $"{KeyId}.{Tag}.{Kind}.key-info";
    }

    /// <summary>
    /// Convert this KeyInfoName to a plain file name (without directory information)
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return FileName;
    }
  }
}
