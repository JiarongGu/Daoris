using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// What the home holds, by kind, in bytes (HIST1c, the history-clearing design §2.4): a conversation, a transcript, a
/// session's files, the kept files of a quest or an ask, and the small files beside them (a conversation id, a marker,
/// a mark, a choice, the spawn files).
/// </summary>
public sealed record HistoryBytes(long Conversations, long Transcripts, long Files, long Kept, long Other)
{
    public static HistoryBytes None { get; } = new(0, 0, 0, 0, 0);

    public long Total => Conversations + Transcripts + Files + Kept + Other;

    public static HistoryBytes operator +(HistoryBytes a, HistoryBytes b) => new(
        a.Conversations + b.Conversations, a.Transcripts + b.Transcripts, a.Files + b.Files, a.Kept + b.Kept, a.Other + b.Other);

    /// <summary>A file's size, or a folder's whole size; nothing for what is not there or will not be read.</summary>
    public static long Of(string path)
    {
        try
        {
            if (File.Exists(path)) return new FileInfo(path).Length;
            if (!Directory.Exists(path)) return 0;
            return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}

/// <summary>
/// Every file the home keeps of one session (HIST1c, D153 point 6, the history-clearing design §2.2), removed by the one
/// helper both a clear and D126's delete call once the service has said yes, so a delete and a clear take the same files.
/// </summary>
/// <remarks>
/// <para><b>What goes, by name</b>, in this order: its conversation, its transcript, its files (the whole
/// <c>sessions/&lt;id&gt;/</c>), its harness conversation id, a leftover process marker, its go-on mark, its choice of a new
/// session, the spawn files a crash left, its held words, its closed automatic landing and its archive mark. Until HIST1c the
/// delete took the first five and the mark, and left the mark, the choice, the held words and the landing behind.</para>
///
/// <para><b>Never</b> a tree, a branch, the usage, the machine log, the harness's own conversation in an account's home
/// (§1.3), or an automatic landing still trying, which a clear refuses before it (<c>HISTORY_LIVE</c>). An id that is not a
/// session's (a teammate's, <c>origin/id</c>) removes no file, since this machine keeps none of it; only its archive mark.</para>
///
/// <para><b>A file the disk will not let go of is left</b>, named in <c>failed</c>: it is left over (§2.3), and the next
/// clear of a workspace takes it.</para>
/// </remarks>
public sealed class SessionHomeFiles(string home)
{
    public const string Conversation = "conversation";
    public const string Transcript = "transcript";
    public const string Files = "files";
    public const string Harness = "harness";
    public const string Marker = "marker";
    public const string Mark = "mark";
    public const string Choice = "choice";
    public const string Spawn = "spawn";
    public const string Held = "held";
    public const string Landing = "landing";
    public const string Archived = "archived";

    /// <summary>The suffixes a session's own files under <c>sessions/</c> carry after its id, the longest first.</summary>
    internal static readonly string[] SessionSuffixes =
        [NewSessionChoices.Suffix, ".events.jsonl", HarnessConversations.Suffix, GoOnMarks.Suffix, ".log", ".pid"];

    /// <summary>The suffixes a session's spawn files carry after its id under <c>spawn/</c>.</summary>
    internal static readonly string[] SpawnSuffixes = [".mcp.json", ".settings.json"];

    /// <summary>The home whose files these are.</summary>
    public string Home => home;

    private string Sessions => Path.Combine(home, "sessions");

    private string SpawnFolder => Path.Combine(home, SpawnServers.Folder);

    /// <summary>The session's own files, each with the name it goes under and whether it is a folder.</summary>
    private IEnumerable<(string Name, string Path, bool Folder)> Own(string id) =>
    [
        (Conversation, Path.Combine(Sessions, $"{id}.events.jsonl"), false),
        (Transcript, Path.Combine(Sessions, $"{id}.log"), false),
        (Files, Path.Combine(Sessions, id), true),
        (Harness, Path.Combine(Sessions, id + HarnessConversations.Suffix), false),
        (Marker, Path.Combine(Sessions, id + ".pid"), false),
        (Mark, Path.Combine(Sessions, id + GoOnMarks.Suffix), false),
        (Choice, Path.Combine(Sessions, id + NewSessionChoices.Suffix), false),
        .. SpawnSuffixes.Select(suffix => (Spawn, Path.Combine(SpawnFolder, id + suffix), false)),
    ];

    /// <summary>What the home keeps of one session, by kind; nothing for an id that is not a session's.</summary>
    public HistoryBytes Size(string id)
    {
        if (!SessionEvents.IsId(id)) return HistoryBytes.None;
        long conversation = 0, transcript = 0, files = 0, other = 0;
        foreach (var (name, path, _) in Own(id))
        {
            var bytes = HistoryBytes.Of(path);
            switch (name)
            {
                case Conversation: conversation += bytes; break;
                case Transcript: transcript += bytes; break;
                case Files: files += bytes; break;
                default: other += bytes; break;
            }
        }

        return new HistoryBytes(conversation, transcript, files, 0, other);
    }

    /// <summary>
    /// Remove what the home keeps of each session, once its record is gone: each name that went, per id, in the order
    /// above. The held words, the closed automatic landings and the archive marks are each one file, written once.
    /// </summary>
    /// <param name="events">The loop's record of conversations, so it forgets their numbering too; null removes the files alone.</param>
    /// <param name="failed">Where each path the disk would not let go of is named; null drops them.</param>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Remove(
        IReadOnlyCollection<string> ids, SessionEvents? events, ICollection<string>? failed = null)
    {
        var removed = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var id in ids) removed.TryAdd(id, []);
        var sessions = removed.Keys.Where(SessionEvents.IsId).ToList();

        foreach (var id in sessions)
        {
            var went = removed[id];
            foreach (var (name, path, folder) in Own(id))
            {
                try
                {
                    var gone = name == Conversation && events is not null ? events.Forget(id)
                        : folder ? FolderGone(path)
                        : FileGone(path);
                    if (gone && !went.Contains(name)) went.Add(name);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    failed?.Add(path);
                }
            }
        }

        if (sessions.Count > 0)
        {
            Each(HeldWordsFile.PathOf(home), () => HeldWordsFile.Forget(home, sessions), Held);
            Each(new AutoLandings(home).FilePath, () => new AutoLandings(home).Forget(sessions), Landing);
        }

        // Every id, a teammate's included: this machine marks their records archived too (D126 §5.2), by the record's id.
        var all = removed.Keys.ToList();
        if (all.Count > 0)
        {
            Each(new SessionArchive(home).FilePath, () =>
            {
                var answer = new SessionArchive(home).Unarchive(all);
                return all.Where(id => !answer.NotArchived.Contains(id, StringComparer.Ordinal)).ToList();
            }, Archived);
        }

        return removed.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal);

        void Each(string file, Func<IReadOnlyCollection<string>> forget, string name)
        {
            try
            {
                foreach (var id in forget())
                {
                    if (removed.TryGetValue(id, out var went)) went.Add(name);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                failed?.Add(file);
            }
        }
    }

    private static bool FileGone(string path)
    {
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    private static bool FolderGone(string path)
    {
        if (!Directory.Exists(path)) return false;
        Directory.Delete(path, recursive: true);
        return true;
    }
}

/// <summary>
/// The words a shell holds for a session winding up, kept across a restart (MSG1d4): <c>&lt;home&gt;/sessions/held-words.json</c>,
/// written whole by the shell's <c>SessionWords</c>. The driver library reads its place from here, and takes a cleared or
/// deleted session's entries out of it (HIST1c), since a word held for a record that is gone has nothing to reach.
/// </summary>
public static class HeldWordsFile
{
    public const string FileName = "held-words.json";

    public static string PathOf(string home) => Path.Combine(home, "sessions", FileName);

    /// <summary>
    /// Drop every entry held for one of <paramref name="sessions"/>, keeping the rest as written; the file is removed once it
    /// holds none, as its writer removes it. The sessions whose words were dropped come back; a file that does not read holds
    /// nothing to drop.
    /// </summary>
    public static IReadOnlyCollection<string> Forget(string home, IReadOnlyCollection<string> sessions)
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return [];
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
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

        var text = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";
        AtomicFile.WriteText(path, text);
        return dropped;
    }
}
