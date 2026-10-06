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
/// What <see cref="SessionHomeFiles.Remove"/> took (HIST1j): each session's names that went, in the inventory's order, and the
/// bytes that went with them, by kind; and each path the disk would not let go of, which stays, left over (the history-clearing
/// design §2.3).
/// </summary>
public sealed record SessionFilesRemoved(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Went,
    IReadOnlyDictionary<string, HistoryBytes> Bytes,
    IReadOnlyList<string> Failed);

/// <summary>
/// Every file the home keeps of one session (HIST1c, D153 point 6, the history-clearing design §2.2), held in one inventory that
/// measures them, removes them and reads a left-over one by its name (HIST1j): the one helper both a clear and D126's delete call
/// once the service has said yes, so a delete and a clear take the same files, and a clear says the bytes that went.
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
/// <para><b>A file the disk will not let go of is left</b>, named in what failed, and none of its bytes count as gone: it is
/// left over (§2.3), and the next clear of a workspace takes it. A folder the disk let go of only in part counts the part that
/// went. Until HIST1j a clear measured each session before the removal and counted it whole, whatever failed.</para>
/// </remarks>
public sealed class SessionHomeFiles(string home, Action<string, bool>? remover = null)
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

    /// <summary>Where a session's file lies under the home.</summary>
    private enum Place
    {
        /// <summary><c>sessions/</c>.</summary>
        Sessions,

        /// <summary><c>spawn/</c>, where a session's servers and settings are handed to it (<see cref="SpawnServers"/>).</summary>
        Spawn,
    }

    /// <summary>
    /// One file the home keeps of a session: the name it goes under, where it lies, what follows the session's id in its name,
    /// whether it is a folder, and the kind of <see cref="HistoryBytes"/> its size counts in.
    /// </summary>
    private sealed record SessionFile(string Name, Place Place, string Suffix, bool Folder, Func<long, HistoryBytes> Counts);

    /// <summary>
    /// The inventory (§2.2), in the order the files go, written once (HIST1j): what a reading measures, what a clear and a delete
    /// remove, and what a left-over file is read by. A per-session file a later change adds is named here, and every door takes it.
    /// </summary>
    private static readonly SessionFile[] Inventory =
    [
        new(Conversation, Place.Sessions, ".events.jsonl", false, bytes => HistoryBytes.None with { Conversations = bytes }),
        new(Transcript, Place.Sessions, ".log", false, bytes => HistoryBytes.None with { Transcripts = bytes }),
        new(Files, Place.Sessions, "", true, bytes => HistoryBytes.None with { Files = bytes }),
        new(Harness, Place.Sessions, HarnessConversations.Suffix, false, Beside),
        new(Marker, Place.Sessions, ".pid", false, Beside),
        new(Mark, Place.Sessions, GoOnMarks.Suffix, false, Beside),
        new(Choice, Place.Sessions, NewSessionChoices.Suffix, false, Beside),
        new(Spawn, Place.Spawn, ".mcp.json", false, Beside),
        new(Spawn, Place.Spawn, ".settings.json", false, Beside),
    ];

    /// <summary>The suffixes a session's own files under <c>sessions/</c> carry after its id, the longest first.</summary>
    internal static readonly string[] SessionSuffixes = Suffixes(Place.Sessions);

    /// <summary>The suffixes a session's spawn files carry after its id under <c>spawn/</c>, the longest first.</summary>
    internal static readonly string[] SpawnSuffixes = Suffixes(Place.Spawn);

    private readonly Action<string, bool> _remove = remover ?? FromDisk;

    /// <summary>A path removed from the disk: a file, or a folder and everything in it.</summary>
    public static void FromDisk(string path, bool folder)
    {
        if (folder) Directory.Delete(path, recursive: true);
        else File.Delete(path);
    }

    /// <summary>The home whose files these are.</summary>
    public string Home => home;

    private string Sessions => Path.Combine(home, "sessions");

    private string SpawnFolder => Path.Combine(home, SpawnServers.Folder);

    private static HistoryBytes Beside(long bytes) => HistoryBytes.None with { Other = bytes };

    private static string[] Suffixes(Place place) =>
    [
        .. Inventory.Where(file => file.Place == place && !file.Folder).Select(file => file.Suffix).OrderByDescending(suffix => suffix.Length),
    ];

    private string PathOf(SessionFile file, string id) => Path.Combine(file.Place == Place.Spawn ? SpawnFolder : Sessions, id + file.Suffix);

    /// <summary>What the home keeps of one session, by kind; nothing for an id that is not a session's.</summary>
    public HistoryBytes Size(string id) =>
        SessionEvents.IsId(id)
            ? Inventory.Aggregate(HistoryBytes.None, (sum, file) => sum + file.Counts(HistoryBytes.Of(PathOf(file, id))))
            : HistoryBytes.None;

    /// <summary>
    /// Remove what the home keeps of each session, once its record is gone: each name that went, per id, in the inventory's
    /// order, and the bytes that went with them; what the disk would not let go of stays, named in what failed, and only what
    /// went is counted (HIST1j). The held words, the closed automatic landings and the archive marks are each one file, written
    /// once.
    /// </summary>
    /// <param name="events">The loop's record of conversations, so it forgets their numbering too; null removes the files alone.</param>
    public SessionFilesRemoved Remove(IReadOnlyCollection<string> ids, SessionEvents? events)
    {
        var removed = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var bytes = new Dictionary<string, HistoryBytes>(StringComparer.Ordinal);
        var failed = new List<string>();
        foreach (var id in ids)
        {
            removed.TryAdd(id, []);
            bytes.TryAdd(id, HistoryBytes.None);
        }

        var sessions = removed.Keys.Where(SessionEvents.IsId).ToList();
        foreach (var id in sessions)
        {
            foreach (var file in Inventory)
            {
                var path = PathOf(file, id);
                // The conversation goes through the loop's record where there is one, so its numbering is forgotten too.
                var (gone, went) = file.Name == Conversation && events is not null
                    ? Take(path, () => events.Forget(id), failed)
                    : Take(path, file.Folder, failed);
                bytes[id] += file.Counts(went);
                if (gone && !removed[id].Contains(file.Name)) removed[id].Add(file.Name);
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

        return new SessionFilesRemoved(
            removed.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal), bytes, failed);

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
                failed.Add(file);
            }
        }
    }

    /// <summary>
    /// Remove one path of the home, a file or a folder whole, through this home's remover (HIST1j): whether it went, and the bytes
    /// that went with it. Where the disk will not let go of it, the path is named in <paramref name="failed"/>, and only what it
    /// let go of before refusing counts. Nothing, for a path that is not there.
    /// </summary>
    internal (bool Gone, long Bytes) Take(string path, bool folder, ICollection<string> failed) =>
        Take(path, () =>
        {
            if (folder ? !Directory.Exists(path) : !File.Exists(path)) return false;
            _remove(path, folder);
            return true;
        }, failed);

    /// <summary>Measured before, removed by <paramref name="remove"/>, and on a refusal measured again: what went is the difference.</summary>
    private static (bool Gone, long Bytes) Take(string path, Func<bool> remove, ICollection<string> failed)
    {
        var before = HistoryBytes.Of(path);
        try
        {
            return remove() ? (true, before) : (false, 0);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            failed.Add(path);
            return (false, Math.Max(0, before - HistoryBytes.Of(path)));
        }
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
