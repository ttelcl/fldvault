/*
 * (c) 2025  ttelcl / ttelcl
 */

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
  /// The key ID.
  /// </summary>
  [JsonProperty("id")]  
  public string KeyId { get; }

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

}
