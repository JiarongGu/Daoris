using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the look's tidy remembers of each repository whose last pass did nothing and said nothing: the stamp of everything the pass
/// reads without asking git (the refs' files, the working trees' entries, the folders under the repository's trees and the records
/// of its branches). While the stamp is the same, a later pass starts no git there; it is looked at again after <see cref="Idle"/>
/// whatever moved, so a change no file shows (a tree's uncommitted work, a session's end the records alone say) waits at most that long.
/// </summary>
/// <remarks>
/// A pass that removed, kept, held or said anything leaves nothing here, so a repository with work in flight is judged at every pace as
/// before. The stamp is read before the pass, so a file the pass itself writes makes the next pass real once and the one after it idle.
/// </remarks>
internal sealed class TidyIdleness(string home, Func<DateTimeOffset> clock)
{
    /// <summary>How long a quiet repository is left alone while its stamp stands.</summary>
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);

    private readonly object _gate = new();
    private readonly Dictionary<string, (string Stamp, DateTimeOffset At)> _quiet = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The stamp of <paramref name="root"/>'s repository now; null where its files cannot say (no stamp, no skip).</summary>
    public string? Stamp(string root, string treesRoot, string workspace, string repository)
    {
        try
        {
            if (TreeTips.CommonDirectory(root) is not { } common || Directory.Exists(Path.Combine(common, "reftable"))) return null;
            var text = new StringBuilder();
            File(text, Path.Combine(common, "HEAD"));
            File(text, Path.Combine(common, "packed-refs"));
            var refs = Path.Combine(common, "refs");
            if (Directory.Exists(refs))
            {
                foreach (var each in Directory.EnumerateFiles(refs, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)) File(text, each);
            }

            var linked = Path.Combine(common, "worktrees");
            if (Directory.Exists(linked))
            {
                foreach (var each in Directory.EnumerateDirectories(linked).Order(StringComparer.Ordinal))
                {
                    text.Append(each).Append('|');
                    File(text, Path.Combine(each, "HEAD"));
                }
            }

            var trees = Path.Combine(treesRoot, workspace, repository);
            if (Directory.Exists(trees))
            {
                foreach (var each in Directory.EnumerateFileSystemEntries(trees).Order(StringComparer.Ordinal)) text.Append(each).Append('\n');
            }

            text.Append(JsonSerializer.Serialize(new SessionBranches(home).Entries().Where(entry => Same(entry.Repository, repository))));
            text.Append(JsonSerializer.Serialize(new LandedBranches(home).Entries().Where(entry => Same(entry.Repository, repository))));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="root"/> is quiet at this <paramref name="stamp"/>: its last pass did nothing, within <see cref="Idle"/>.</summary>
    public bool Skips(string root, string? stamp)
    {
        if (stamp is null) return false;
        lock (_gate) return _quiet.TryGetValue(root, out var last) && last.Stamp == stamp && clock() - last.At < Idle;
    }

    /// <summary>A pass of <paramref name="root"/> begins: it is quiet again only once it ends so.</summary>
    public void Forget(string root)
    {
        lock (_gate) _quiet.Remove(root);
    }

    /// <summary>Remembers <paramref name="root"/> at <paramref name="stamp"/> where the pass did nothing and said nothing, and forgets it otherwise.</summary>
    public void Remember(string root, string? stamp, TidyPass pass)
    {
        var quiet = stamp is not null && pass.Held is null && pass.Results.Count == 0 && pass.Landed.Count == 0 && pass.Strays.Count == 0
                    && pass.Recovered.Count == 0 && pass.Released.Count == 0;
        lock (_gate)
        {
            if (quiet) _quiet[root] = (stamp!, clock());
            else _quiet.Remove(root);
        }
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void File(StringBuilder text, string path)
    {
        var info = new FileInfo(path);
        text.Append(path).Append('|').Append(info.Exists ? info.Length : -1).Append('|').Append(info.Exists ? info.LastWriteTimeUtc.Ticks : 0).Append('\n');
    }
}
