/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace FldVault.Core.Vaults;

/// <summary>
/// JSON serializable variant of PassphraseKeyInfoFile.
/// </summary>
public class Zkey
{
  /// <summary>
  /// Create a new Zkey
  /// </summary>
  public Zkey(
    string id,
    string salt,
    DateTime created)
  {
    KeyId = id;
    KeyGuid = Guid.Parse(id);
    Salt = salt;
    Created = created;
  }

  /// <summary>
  /// Create a new Zkey from a JSON string
  /// </summary>
  public static Zkey FromJson(string json)
  {
    return
      JsonConvert.DeserializeObject<Zkey>(json)
      ?? throw new ArgumentException("Invalid JSON");
  }

  /// <summary>
  /// Load from a *.zkey file.
  /// </summary>
  /// <param name="fileName">
  /// The file name of the Zkey file to load.
  /// </param>
  /// <returns>
  /// The loaded Zkey object.
  /// </returns>
  /// <exception cref="FileNotFoundException"></exception>
  public static Zkey FromJsonFile(string fileName)
  {
    if(!File.Exists(fileName))
    {
      throw new FileNotFoundException("Zkey file not found", fileName);
    }
    var json = File.ReadAllText(fileName, Encoding.UTF8);
    return FromJson(json);
  }

  /// <summary>
  /// Save the Zkey to a *.zkey (JSON) file.
  /// </summary>
  public void SaveToJsonFile(string fileName)
  {
    var json = ToString(true);
    File.WriteAllText(fileName, json);
  }

  /// <summary>
  /// Create a new Zkey from a PassphraseKeyInfoFile
  /// </summary>
  public static Zkey FromPassphraseKeyInfoFile(PassphraseKeyInfoFile pkif)
  {
    return new Zkey(
      pkif.KeyId.ToString(),
      pkif.SaltBase64,
      pkif.UtcKeyStamp);
  }

  /// <summary>
  /// Convert the Zkey to PassphraseKeyInfoFile (binary serializable) format.
  /// </summary>
  /// <returns></returns>
  public PassphraseKeyInfoFile ToPassphraseKeyInfoFile()
  {
    return PassphraseKeyInfoFile.FromZkey(this);
  }

  /// <summary>
  /// Try to retrieve a Zkey from the lines in a ZKEY transfer string.
  /// This version will ignore the passphrase slot, if any.
  /// Use <see cref="ZkeyEx.TryFromTransferLines(IEnumerable{string})"/>
  /// to include the passphrase slot.
  /// </summary>
  public static Zkey? TryFromTransferLines(IEnumerable<string> lines)
  {
    var props = ParsePropertyLines(lines, "ZKEY");
    if(props == null
      || !props.TryGetValue("ID", out var idText)
      || !props.TryGetValue("SALT", out var salt)
      || !(props.TryGetValue("TIME", out var timeText) ||
           props.TryGetValue("CREATED", out timeText)))
    {
      return null;
    }
    if(!Guid.TryParse(idText, out var _))
    {
      return null;
    }
    if(!DateTime.TryParse(
      timeText,
      CultureInfo.InvariantCulture,
      DateTimeStyles.RoundtripKind,
      out var created) || created.Kind != DateTimeKind.Utc)
    {
      return null;
    }
    if(!Base64Url.IsValid(salt))
    {
      return null;
    }
    return new Zkey(
      idText,
      salt,
      created);
  }

  /// <summary>
  /// Split a transfer string into lines and try to retrieve a Zkey from it.
  /// This version will ignore the passphrase slot, if any.
  /// Use <see cref="ZkeyEx.TryFromTransferString(string)"/> to include
  /// the passphrase slot.
  /// </summary>
  public static Zkey? TryFromTransferString(string text)
  {
    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    return TryFromTransferLines(lines);
  }

  /// <summary>
  /// The key ID.
  /// </summary>
  [JsonProperty("id")]  
  public string KeyId { get; }

  /// <summary>
  /// The key ID as a GUID. This is the same value as <see cref="KeyId"/>,
  /// but as a GUID.
  /// </summary>
  [JsonIgnore]
  public Guid KeyGuid { get; }

  /// <summary>
  /// The bas64url encoded salt. 
  /// </summary>
  [JsonProperty("salt")]
  public string Salt { get; }

  /// <summary>
  /// The time the key was created (in UTC)
  /// </summary>
  [JsonProperty("created")]
  public DateTime Created { get; }

  /// <summary>
  /// Return the first 8 characters of the key ID.
  /// </summary>
  [JsonIgnore]
  public string KeyTag => KeyId.Substring(0, 8);

  /// <summary>
  /// Decode the salt to a byte array.
  /// </summary>
  public byte[] GetSalt()
  {
    return Base64Url.DecodeFromChars(Salt);
  }

  /// <summary>
  /// Convert the Zkey to a JSON string.
  /// </summary>
  /// <param name="indent">
  /// Whether to return indented JSON or not.
  /// </param>
  /// <returns></returns>
  public string ToString(bool indent)
  {
    return JsonConvert.SerializeObject(
      this, indent ? Formatting.Indented : Formatting.None);
  }

  /// <summary>
  /// Convert the Zkey to a non-indented JSON string.
  /// </summary>
  public override string ToString()
  {
    return ToString(false);
  }

  /// <summary>
  /// Convert the Zkey to 'transfer string' format.
  /// An empty spot for the passphrase is optionally added.
  /// </summary>
  /// <returns></returns>
  public string ToZkeyTransferString(bool addPassSlot)
  {
    var lines = new List<string> {
      "<ZKEY>",
      $"ID: {KeyId}",
      $"SALT: {Salt}",
      $"TIME: {Created.ToString("o")}"
    };
    if(addPassSlot)
    {
      lines.Add("PASS:");
    }
    lines.Add("</ZKEY>");
    return string.Join("\r\n", lines);
  }

  /// <summary>
  /// Parse a transfer string into a map of key-value pairs.
  /// Only lines between the start (&lt;<paramref name="tag"/>&gt;) and
  /// end tag (&lt;/<paramref name="tag"/>&gt;) lines are parsed.
  /// In that range only lines looking like "KEY: value" or "KEY=value" are
  /// parsed (space around ':' and '=' is optional)
  /// </summary>
  /// <param name="lines">
  /// The lines to parse
  /// </param>
  /// <param name="tag">
  /// The tag used to derive the start and end tag. Default is "ZKEY".
  /// </param>
  /// <returns></returns>
  public static Dictionary<string, string>? ParsePropertyLines(
    IEnumerable<string> lines,
    string tag = "ZKEY")
  {
    var list = lines.ToList();
    var result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
    var startTag = $"<{tag}>";
    var endTag = $"</{tag}>";
    var active = false;
    foreach(var line in lines)
    {
      if(!active && line == startTag)
      {
        active = true;
        continue;
      }
      if(active && line.StartsWith(endTag))
      {
        return result;
      }
      if(active)
      {
        var match = Regex.Match(line, @"^([a-zA-Z]+)(\s*[:=]\s*|\s+)(.*)$");
        if(match.Success)
        {
          var key = match.Groups[1].Value.ToUpper();
          var value = match.Groups[3].Value.Trim();
          result[key] = value;
        }
        // ignore any other lines
      }
    }
    return null; // No start tag followed later by end tag: bad data
  }
}

/// <summary>
/// A Zkey with an optional passphrase.
/// </summary>
public class ZkeyEx: Zkey, IDisposable
{
  private SecureString? _passphrase;

  /// <summary>
  /// Create a new ZkeyEx
  /// </summary>
  /// <param name="id">
  /// The key ID.
  /// </param>
  /// <param name="salt">
  /// The salt, base64url encoded.
  /// </param>
  /// <param name="created">
  /// The UTC time the key was created.
  /// </param>
  /// <param name="passphraseOriginal">
  /// Optional: the passphrase. This constructor will copy the passphrase,
  /// the caller is responsible for disposing the original.
  /// </param>
  public ZkeyEx(
    string id,
    string salt,
    DateTime created,
    SecureString? passphraseOriginal = null)
    : base(id, salt, created)
  {
    _passphrase = passphraseOriginal?.Copy();
  }

  /// <summary>
  /// Get or set the passphrase. If different from before, the old
  /// passphrase will be disposed. NOTE! No guarantee is given that
  /// this passphrase is actually correct!
  /// </summary>
  public SecureString? Passphrase {
    get => _passphrase;
    set {
      // Don't dispose the passphrase if it's the same object.
      if(!ReferenceEquals(_passphrase, value))
      {
        if(_passphrase != null)
        {
          _passphrase.Dispose();
        }
        _passphrase = value;
      }
    }
  }

  /// <summary>
  /// Clean up the passphrase, if any.
  /// </summary>
  public void Dispose()
  {
    if(_passphrase != null)
    {
      _passphrase.Dispose();
      _passphrase = null;
    }
  }


  /// <summary>
  /// Try to retrieve an extended Zkey from the lines in a ZKEY transfer string.
  /// If there is a passphrase slot, the passphrase will be set. If not,
  /// the passphrase will be null.
  /// </summary>
  public new static ZkeyEx? TryFromTransferLines(IEnumerable<string> lines)
  {
    var props = 
      ParsePropertyLines(lines, "ZKEY")
      ?? ParsePropertyLines(lines, "ZKEYPASS");
    if(props == null
      || !props.TryGetValue("ID", out var idText)
      || !props.TryGetValue("SALT", out var salt)
      || !(props.TryGetValue("TIME", out var timeText) ||
           props.TryGetValue("CREATED", out timeText)))
    {
      return null;
    }
    if(!Guid.TryParse(idText, out var _))
    {
      return null;
    }
    if(!DateTime.TryParse(
      timeText,
      CultureInfo.InvariantCulture,
      DateTimeStyles.RoundtripKind,
      out var created) || created.Kind != DateTimeKind.Utc)
    {
      return null;
    }
    if(!Base64Url.IsValid(salt))
    {
      return null;
    }
    SecureString? ss = null;
    if(props.TryGetValue("PASS", out var passText))
    {
      passText = passText.Trim();
      if(passText.Length > 3) // reject super short passphrases!
      {
        ss = new SecureString();
        foreach(var c in passText)
        {
          ss.AppendChar(c);
        }
        ss.MakeReadOnly();
      }
    }
    return new ZkeyEx(
      idText,
      salt,
      created,
      ss);
  }

  /// <summary>
  /// Split a transfer string into lines and try to retrieve a ZkeyEx from it.
  /// </summary>
  public new static ZkeyEx? TryFromTransferString(string text)
  {
    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    return TryFromTransferLines(lines);
  }

}

