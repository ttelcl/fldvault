using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LibGit2Sharp;

namespace GitVaultLib.Delta;

/// <summary>
/// Extension methods related to LibGit2Sharp
/// </summary>
public static class LibGit2Extensions
{

  /// <summary>
  /// Try to resolve a <see cref="Reference"/> to the <see cref="Commit"/> it
  /// ultimately points to. This resolves both symbolic references as well as
  /// tag annotations. Returns null if resolution fails.
  /// </summary>
  /// <param name="r"></param>
  /// <returns></returns>
  public static Commit? ResolveReferenceToCommit(this Reference r)
  {
    var dr = r.ResolveToDirectReference();
    var commit =
      dr.Target switch {
        Commit c1 => c1,
        TagAnnotation ta =>
          ta.Target switch {
            Commit c2 => c2,
            _ => null // give up; too complex to bother
          },
        _ => null // unrecognized
      };
    return commit;
  }
}
