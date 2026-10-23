using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The two machine-local records registration following keeps under the home (WSSETUP5, D124 §3): which repositories'
/// lines Daoris moved, and what following each last came to.
/// </summary>
/// <remarks>
/// <para><b><c>lines-moved.json</c></b> is written where Daoris moves a line (<see cref="SessionTrees"/>: a merge landing,
/// a fast-forward), whichever door pressed it, and read by the next look of whichever loop runs on this home. That is how
/// a landing from the screen, a terminal or Ask Daoris reaches the registry without each door knowing the service, and
/// how one made while no loop runs is followed when one starts.</para>
///
/// <para><b><c>registry-followed.json</c></b> is the follower's: each repository's last outcome, the sentence its row says
/// and when. A line is due when Daoris moved it after it was last followed.</para>
///
/// <para><b>Machine-local, under the home (D63), and neither is a reason to fail.</b> A record that does not read is
/// none, and a write that fails is dropped: the move has happened, and the next start follows every line anyway. Each
/// file has one kind of writer, so two processes can only race on the same file kind; the loser's entry is followed
/// again at the next start.</para>
///
/// <para>The records' shapes and what is due are read here, and the files are read and written beside them.</para>
/// </remarks>
public static class RegistryFollowing
{

    // One writer at a time in this process, per file: two presses in one shell would otherwise drop each other's entry.
    private static readonly object Gate = new();

    /// <summary>Record that Daoris moved <paramref name="repository"/>'s line now. Never throws.</summary>
    public static void Moved(string home, string repository, DateTimeOffset at)
    {
        try
        {
            lock (Gate)
            {
                var moved = ReadMoved(home);
                moved[repository] = at;
                var file = new JsonObject
                {
                    ["moved"] = new JsonObject(moved.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => KeyValuePair.Create(pair.Key, (JsonNode?)Stamp(pair.Value)))),
                };
                JsonFile.Write(DaorisHome.Entry(home, HomeEntry.LinesMoved), file);
                // The loop's own write, which its watch of the file hears and wakes no look for (FABRIC-EVENT4).
                HomeWrites.Mine(DaorisHome.Entry(home, HomeEntry.LinesMoved));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Dropped: the line has moved, and the next start follows every line.
        }
    }

    /// <summary>
    /// The ref watch heard <paramref name="repository"/>'s refs move (SETUPSTATE1): where git's own files say its line, as it was last
    /// followed, now stands at another commit than the one followed, it is recorded moved, so the next look follows it. A line moved
    /// outside Daoris (a pull request merged on the remote, then pulled) is how a set-up landed on a branch for review reaches its line,
    /// and no Daoris landing says so. Read with no process: one never followed is the start's to follow, and a line the files cannot
    /// say (the reftable format, a ref being replaced) waits for the next start. Never throws.
    /// </summary>
    /// <returns>Whether it was recorded moved now.</returns>
    public static bool Heard(string home, RefRepository repository)
    {
        try
        {
            if (!Read(home).TryGetValue(repository.Repository, out var last) || last.Line is not { Length: > 0 } line
                || last.Commit is not { Length: > 0 } commit
                || ReadMoved(home).TryGetValue(repository.Repository, out var moved) && moved > last.At)
            {
                return false;
            }

            var common = TreeTips.CommonDirectory(repository.Root);
            if (common is null || TreeTips.Named(common, $"refs/heads/{line}") is not { } now
                || string.Equals(now, commit, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Moved(home, repository.Repository, DateTimeOffset.UtcNow);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>The repositories whose line Daoris moved after each was last followed, by name.</summary>
    public static IReadOnlyList<string> Due(string home)
    {
        var followed = Read(home);
        return
        [
            .. ReadMoved(home)
                .Where(pair => !followed.TryGetValue(pair.Key, out var last) || last.At < pair.Value)
                .Select(pair => pair.Key)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>Keep what following <paramref name="followed"/> came to, at <paramref name="at"/>. Never throws.</summary>
    public static void Remember(string home, RegistrationFollowed followed, DateTimeOffset at)
    {
        try
        {
            lock (Gate)
            {
                var entries = Read(home);
                entries[followed.Repository] = new(followed.Repository, at, followed.Outcome, followed.Said, followed.Line, followed.Commit);
                var repositories = new JsonObject();
                foreach (var entry in entries.Values.OrderBy(entry => entry.Repository, StringComparer.Ordinal))
                {
                    repositories[entry.Repository] = new JsonObject
                    {
                        ["at"] = Stamp(entry.At),
                        ["outcome"] = entry.Outcome,
                        ["said"] = entry.Said,
                        ["line"] = entry.Line,
                        ["commit"] = entry.Commit,
                    };
                }

                JsonFile.Write(DaorisHome.Entry(home, HomeEntry.RegistryFollowed),new JsonObject { ["repositories"] = repositories });
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Dropped: the log still has the outcome, and the next follow writes it again.
        }
    }

    /// <summary>What following last came to for each repository, by name; none where the record does not read.</summary>
    public static Dictionary<string, FollowedEntry> Read(string home)
    {
        var entries = new Dictionary<string, FollowedEntry>(StringComparer.OrdinalIgnoreCase);
        if (Load(DaorisHome.Entry(home, HomeEntry.RegistryFollowed))?["repositories"] is not JsonObject repositories) return entries;
        foreach (var (name, value) in repositories)
        {
            if (value is not JsonObject entry || When(entry["at"]) is not { } at || Text(entry["outcome"]) is not { } outcome) continue;
            entries[name] = new(name, at, outcome, Text(entry["said"]) ?? "", Text(entry["line"]), Text(entry["commit"]));
        }

        return entries;
    }

    private static Dictionary<string, DateTimeOffset> ReadMoved(string home)
    {
        var moved = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        if (Load(DaorisHome.Entry(home, HomeEntry.LinesMoved))?["moved"] is not JsonObject lines) return moved;
        foreach (var (name, value) in lines)
        {
            if (When(value) is { } at) moved[name] = at;
        }

        return moved;
    }

    private static string? TextIfThere(string path) => File.Exists(path) ? AtomicFile.ReadText(path) : null;

    private static JsonNode? Load(string path)
    {
        try
        {
            return TextIfThere(path) is { } text ? JsonNode.Parse(text) : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Stamp(DateTimeOffset at) => Moments.Write(at.ToUniversalTime());

    private static DateTimeOffset? When(JsonNode? node) => Moments.Read(node);
}
