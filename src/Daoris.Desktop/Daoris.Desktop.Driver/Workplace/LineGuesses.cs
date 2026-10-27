using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A checkout's guess at its line (<see cref="WorkingTree.DefaultBranchAsync"/>), asked of git once for as long as the files git
/// reads to answer stay as they were (IDLECPU1). The screen's LINES, the remote sync and the registration's line asked git of
/// every checkout with no line set at each ask, one to three program starts each; on a machine whose every start costs a quarter
/// of a second or more (DEVDRIVE1), an idle window's list cost seconds of the shell's time at each ask.
/// </summary>
/// <remarks>
/// <para><b>What git reads is what is watched</b>, in the files backend's layout: origin's <c>HEAD</c>, which is a symbolic ref and
/// so never packed; the loose <c>main</c> and <c>master</c>; and <c>packed-refs</c>, where either may be packed. Each by its write
/// time and its size, so a ref set, moved, packed or removed asks git again.</para>
///
/// <para><b>What the files cannot tell is asked every time</b>, as before: a checkout whose <c>.git</c> is a file (a linked
/// worktree's or a submodule's), and a reftable store. A guess that failed is never kept, and one whose files moved while git
/// was asked is not kept either.</para>
/// </remarks>
public sealed class LineGuesses(Func<string, CancellationToken, Task<string?>> guess)
{
    /// <summary>The one the process asks through, over git.</summary>
    public static LineGuesses Shared { get; } = new(WorkingTree.DefaultBranchAsync);

    private static readonly string[] Read = ["refs/remotes/origin/HEAD", "refs/heads/main", "refs/heads/master", "packed-refs"];

    private readonly ConcurrentDictionary<string, (string Stamp, string? Branch)> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The checkout at <paramref name="root"/>'s guess at its line: the one kept while its files stand, else git's.</summary>
    public async Task<string?> GuessAsync(string root, CancellationToken ct)
    {
        var key = Path.GetFullPath(root);
        var stamp = Stamp(key);
        if (stamp is not null && _known.TryGetValue(key, out var known) && known.Stamp == stamp) return known.Branch;

        var branch = await guess(root, ct).ConfigureAwait(false);
        if (stamp is not null && Stamp(key) == stamp) _known[key] = (stamp, branch);
        return branch;
    }

    /// <summary>What git reads for the guess, as one string; null where the files cannot tell it.</summary>
    internal static string? Stamp(string root)
    {
        try
        {
            var git = Path.Combine(root, ".git");
            if (!Directory.Exists(git) || Directory.Exists(Path.Combine(git, "reftable"))) return null;
            return string.Join("|", Read.Select(name => Of(Path.Combine(git, name.Replace('/', Path.DirectorySeparatorChar)))));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Of(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? $"{file.LastWriteTimeUtc.Ticks}:{file.Length}" : "-";
    }
}
