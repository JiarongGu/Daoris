using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Whoever keeps the held words in memory beside their file, which it then owns: it drops a session's words from both together,
/// so the file it writes next from memory never brings one back.
/// </summary>
public interface IHeldWordsHolder
{
    /// <summary>Drop every word held for one of <paramref name="sessions"/>; the sessions that had any come back.</summary>
    IReadOnlyCollection<string> Forget(IReadOnlyCollection<string> sessions);
}

/// <summary>
/// The words a shell holds for a session winding up, kept across a restart: <c>&lt;home&gt;/sessions/held-words.json</c>,
/// written whole by the shell's <c>SessionWords</c> queue. The driver library reads its place from here, and takes a cleared or
/// deleted session's words out of it, since a word held for a record that is gone has nothing to reach.
/// </summary>
/// <remarks>
/// A process that holds the words in memory (<see cref="Holding"/>) owns the file: <see cref="Forget"/> asks it, so memory and
/// file change together. A process with none (a terminal's own delete, no Daoris running) edits the file in place, which nothing
/// else is then writing.
/// </remarks>
public static class HeldWordsFile
{
    public const string FileName = "held-words.json";

    private static readonly ConcurrentDictionary<string, IHeldWordsHolder> Holders = new(StringComparer.OrdinalIgnoreCase);

    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Sessions, FileName);

    /// <summary>
    /// <paramref name="holder"/> keeps this home's held words in this process, until the answer is disposed. A second holder of the
    /// same home replaces the first, and the first's disposal then leaves the second.
    /// </summary>
    public static IDisposable Holding(string home, IHeldWordsHolder holder)
    {
        var key = Path.GetFullPath(PathOf(home));
        Holders[key] = holder;
        return new Release(key, holder);
    }

    private sealed class Release(string key, IHeldWordsHolder holder) : IDisposable
    {
        public void Dispose() => Holders.TryRemove(new KeyValuePair<string, IHeldWordsHolder>(key, holder));
    }

    /// <summary>
    /// Drop every word held for one of <paramref name="sessions"/>, keeping the rest. The sessions whose words were dropped come
    /// back. Through the process's holder where it has one; else in the file, which is removed once it holds none, as its writer
    /// removes it, and a file that does not read holds nothing to drop.
    /// </summary>
    public static IReadOnlyCollection<string> Forget(string home, IReadOnlyCollection<string> sessions) =>
        Holders.TryGetValue(Path.GetFullPath(PathOf(home)), out var holder) ? holder.Forget(sessions) : ForgetInFile(home, sessions);

    private static IReadOnlyCollection<string> ForgetInFile(string home, IReadOnlyCollection<string> sessions)
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return [];
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(AtomicFile.ReadText(path)) as JsonObject;
        }
        catch (JsonException)
        {
            return [];
        }

        if (root?["held"] is not JsonArray held) return [];
        var named = new HashSet<string>(sessions, StringComparer.OrdinalIgnoreCase);
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in held.ToList())
        {
            if (entry is JsonObject word && word["session"] is JsonValue value && value.TryGetValue<string>(out var session)
                && named.Contains(session))
            {
                held.Remove(entry);
                dropped.Add(sessions.First(id => string.Equals(id, session, StringComparison.OrdinalIgnoreCase)));
            }
        }

        if (dropped.Count == 0) return [];
        if (held.Count == 0)
        {
            File.Delete(path);
            return dropped;
        }

        JsonFile.Write(path, root);
        return dropped;
    }
}
