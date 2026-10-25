using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>What one pass over a home's binding files came to (<see cref="BindingStore.ImportFiles"/>).</summary>
/// <param name="Imported">The files whose conversation the store now holds, each set aside.</param>
/// <param name="Unread">The files that do not read as a conversation: left where they are, as evidence.</param>
/// <param name="Held">Why nothing was imported, where the start's confirmation is not settled or the store would not take it; else null.</param>
public sealed record BindingImport(int Imported, int Unread, string? Held)
{
    /// <summary>Nothing to import: no binding file in the home.</summary>
    public static readonly BindingImport None = new(0, 0, null);
}

/// <summary>
/// The participants' bindings in the desktop store (FOUNDATION1 z-iii, D212; the design §13.3, §13.5, §13.6): the kept conversation of each
/// session record of this machine, <c>bindings</c>, which row z-iii takes from each session's <c>&lt;session&gt;.harness.json</c>.
/// </summary>
/// <remarks>
/// <para><b>Imported once the start's confirmation is settled, then set aside.</b> A conversation id is what an older build *resumes*, and a
/// backup left in place would hand it a stale one, since the kept id is replaced whenever the wire names a conversation. So each file is
/// read into the store and moved to <c>&lt;file&gt;.imported</c> under the import's receipt (<c>imports</c>), never deleted, and a build from
/// before the store finds none and carries on (<see cref="ContinueWhy.Unkept"/>). Until then every reader reads the file, as before.</para>
/// <para><b>A home is in use of the store from its first import</b> (an <c>imports</c> receipt of this family), and from then every keep
/// writes the row, naming its own columns and never replacing the row (O3-16), and every read reads it. A home with no receipt keeps and
/// reads the file, so a build rolled back before it confirmed meets every file as it was.</para>
/// <para><b>A file an older build writes after the import</b>, during a downgrade, is the later evidence: it is imported at the return,
/// at the next look or at the read that meets it, its conversation id winning for its own session, and set aside again.</para>
/// <para><b>Opened for one use and closed.</b> Callers build these over any home, often for one read, so the store is opened for each use and
/// never held, a read never creating or writing it. Every open of the process names one run (<see cref="Run"/>), so <c>store_runs</c> holds
/// the process once.</para>
/// <para><b>Only the rows' side.</b> The file's format is <see cref="HarnessConversations"/>', which hands this its reader of one file, so the
/// store names nothing of the course's records and the conversation is a pair of strings here.</para>
/// </remarks>
/// <param name="home">The home whose store and files these are.</param>
/// <param name="settled">The reading of the start's confirmation; null reads this install's swap journal.</param>
/// <param name="readFile">What a binding file at a path holds, the adapter and the conversation, or null where it does not read as one.</param>
public sealed class BindingStore(string home, Func<StoreSettled>? settled, Func<string, (string Adapter, string Conversation)?> readFile)
{
    /// <summary>The family's name in <c>imports</c> and <c>store_revisions</c>.</summary>
    public const string Family = "bindings";

    /// <summary>What an imported file's name gains when it is set aside.</summary>
    public const string SetAsideSuffix = ".imported";

    /// <summary>This process's run, drawn once: what every open of the store here is recorded under.</summary>
    public static string Run { get; } = Guid.NewGuid().ToString("N");

    /// <summary>What this process is, for its run: a verb until a composition root says it is a shell or a host.</summary>
    public static string Kind { get; set; } = "verb";

    private static readonly ConcurrentDictionary<string, byte> Said = new(StringComparer.Ordinal);

    private Func<StoreSettled> Settled => settled ?? StartConfirmation.Read;

    /// <summary>The store's file in this home.</summary>
    public string Location => DesktopStore.LocationIn(home);

    /// <summary>Where a session's binding file is, from before the import and from an older build.</summary>
    public string FileOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + HarnessConversations.Suffix);

    /// <summary>Where the file is set aside.</summary>
    public string SetAsideOf(string session) => FileOf(session) + SetAsideSuffix;

    /// <summary>
    /// Whether this home keeps its bindings in the store: the store is there at the build's version and holds an import of this family. A store
    /// that cannot be read says no, so the files, as they were, are read.
    /// </summary>
    public bool InUse()
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            return store is { Version: >= 2 }
                   && store.Query("SELECT 1 FROM imports WHERE family = $family LIMIT 1", row => row.GetInt32(0), ("$family", Family)).Count > 0;
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The conversation the store holds for a session, or null for none; the store's failure is thrown.</summary>
    public (string Adapter, string Conversation)? Read(string session)
    {
        using var store = DesktopStoreReader.Open(Location);
        if (store is not { Version: >= 2 }) return null;
        return store.Query(
            "SELECT adapter, conversation FROM bindings WHERE session = $session",
            row => ((string, string)?)(row.GetString(0), row.GetString(1)),
            ("$session", session)).FirstOrDefault();
    }

    /// <summary>
    /// Keep what the wire named a session's conversation: its row's adapter and conversation set by name, the row's other columns left as
    /// they are, in one transaction of the store's. Throws what the store throws where it cannot write.
    /// </summary>
    public void Keep(string session, string adapter, string conversation)
    {
        using var store = Open();
        store.Write(Family, write => Upsert(write, session, adapter, conversation));
    }

    /// <summary>
    /// Keep that the person picked this session's account (FOUNDATION1a-ii, the design §3.2 step 4): the account a start they named one for
    /// opened on, set by name beside the binding, so the account rule reads it as its constraint. Only a session of this machine has one, and
    /// a blank account keeps nothing. Throws what the store throws where it cannot write.
    /// </summary>
    public void Pick(string session, string account)
    {
        using var store = Open();
        store.Write(
            Family,
            write => write.Execute(
                """
                INSERT INTO binding_picks (session, account, at) VALUES ($session, $account, $at)
                ON CONFLICT (session) DO UPDATE SET account = excluded.account, at = excluded.at
                """,
                ("$session", session), ("$account", account),
                ("$at", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// The account the person picked for a session, or null where they picked none: an agent's pick, the curve's account, a record from before
    /// the pick was kept and a teammate's mirror hold none, and a home with no store holds none. The store's failure is thrown.
    /// </summary>
    public string? Picked(string session)
    {
        using var store = DesktopStoreReader.Open(Location);
        if (store is not { Version: >= 3 }) return null;
        return store.Query("SELECT account FROM binding_picks WHERE session = $session", row => row.GetString(0), ("$session", session)).FirstOrDefault();
    }

    /// <summary>The bindings of these sessions gone, each by its record's deletion (§13.6): the ones that held a row. Nothing is made for a home with no store.</summary>
    public IReadOnlyList<string> Forget(IReadOnlyCollection<string> sessions)
    {
        var held = Holding(sessions);
        if (held.Count == 0) return [];
        using var store = Open();
        store.Write(Family, write =>
        {
            foreach (var session in held)
            {
                write.Execute("DELETE FROM bindings WHERE session = $session", ("$session", session));
                write.Execute("DELETE FROM binding_picks WHERE session = $session", ("$session", session));
            }
        });
        return held;
    }

    /// <summary>
    /// Which of these sessions the store holds a binding for, or a pick of its account, read without writing or making anything; none for a
    /// home with no store.
    /// </summary>
    public IReadOnlyList<string> Holding(IReadOnlyCollection<string> sessions)
    {
        if (sessions.Count == 0) return [];
        using var store = DesktopStoreReader.Open(Location);
        if (store is not { Version: >= 2 }) return [];
        var picks = store.Version >= 3;
        return [.. sessions.Where(session =>
            store.Query("SELECT 1 FROM bindings WHERE session = $session", row => row.GetInt32(0), ("$session", session)).Count > 0
            || (picks && store.Query("SELECT 1 FROM binding_picks WHERE session = $session", row => row.GetInt32(0), ("$session", session)).Count > 0))];
    }

    /// <summary>
    /// Import every session's binding file the home holds, once the start's confirmation is settled, and set each aside: the conversation its
    /// file names replaces the row's, the file's being the later evidence (the first import's, or an older build's during a downgrade).
    /// A file that does not read as a conversation stays where it is; where the start is not settled, or the store will not take the import,
    /// every file stays and the outcome says why. Called at each look until the home has none.
    /// </summary>
    public BindingImport ImportFiles()
    {
        var found = Files();
        if (found.Count == 0) return BindingImport.None;
        if (Settled() is { Is: false } waiting) return Held(waiting.Said ?? "the start's confirmation is not settled");
        try
        {
            using var store = Open();
            var imported = 0;
            var unread = 0;
            foreach (var (session, path) in found)
            {
                switch (ImportOne(store, session, path, out var why))
                {
                    case Imported.Done: imported++; break;
                    case Imported.Unread: unread++; break;
                    default: return new BindingImport(imported, unread, Held(why!).Held);
                }
            }

            return new BindingImport(imported, unread, null);
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return Held(error.Message);
        }
    }

    /// <summary>
    /// Import one session's file now, as <see cref="ImportFiles"/> does each: whether the store holds its conversation, a file that does not
    /// read being no import, and the store's own gate holding it until the start's confirmation is settled. Throws what the store throws.
    /// </summary>
    public bool ImportFile(string session)
    {
        var path = FileOf(session);
        if (!File.Exists(path)) return false;
        using var store = Open();
        return ImportOne(store, session, path, out _) == Imported.Done;
    }

    private enum Imported
    {
        Done,
        Unread,
        Held,
    }

    private Imported ImportOne(DesktopStore store, string session, string path, out string? why)
    {
        why = null;
        byte[] bytes;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Imported.Unread;
        }

        if (readFile(path) is not { } conversation) return Imported.Unread;
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var receipt = Path.GetRelativePath(home, path).Replace('\\', '/');

        // A file whose very content an import already holds was imported, and a crash stopped short of setting it aside: only its
        // set-aside is left to do, for the store may have kept a later conversation since, which this stale one must not replace.
        if (store.LastImport(Family, receipt) is not { } last || last.Import.Sha256 != sha)
        {
            var outcome = store.Import(
                new StoreImport(Family, receipt, bytes.Length, sha, 1, ImportClass.SetAside),
                write => Upsert(write, session, conversation.Adapter, conversation.Conversation));
            if (!outcome.Done)
            {
                why = outcome.Said ?? "the store would not take it";
                return Imported.Held;
            }
        }

        SetAside(path, session, sha);
        return Imported.Done;
    }

    /// <summary>The file moved to its set-aside name, unless it changed since it was read: that one is a later write, imported at the next look.</summary>
    private void SetAside(string path, string session, string sha)
    {
        try
        {
            using (var again = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (Convert.ToHexString(SHA256.HashData(again)).ToLowerInvariant() != sha) return;
            }

            File.Move(path, SetAsideOf(session), overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left where it is: its receipt says it was imported, and the next look sets it aside.
        }
    }

    private static void Upsert(DesktopWrite write, string session, string adapter, string conversation) =>
        write.Execute(
            """
            INSERT INTO bindings (session, adapter, conversation, kept) VALUES ($session, $adapter, $conversation, $kept)
            ON CONFLICT (session) DO UPDATE SET adapter = excluded.adapter, conversation = excluded.conversation, kept = excluded.kept
            """,
            ("$session", session), ("$adapter", adapter), ("$conversation", conversation),
            ("$kept", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>The binding files of the home, by session: only a local session's, never a set-aside one.</summary>
    private List<(string Session, string Path)> Files()
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Sessions);
        if (!Directory.Exists(folder)) return [];
        try
        {
            return [.. Directory.EnumerateFiles(folder, "*" + HarnessConversations.Suffix)
                .Select(path => (Session: Path.GetFileName(path)[..^HarnessConversations.Suffix.Length], Path: path))
                .Where(each => SessionEventText.IsLocalId(each.Session))];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private DesktopStore Open() => DesktopStore.Open(Location, new DesktopStoreOptions { Kind = Kind, Run = Run, Settled = Settled });

    /// <summary>A held pass, its reason said once for the process and home, since the look asks again until it is settled.</summary>
    private BindingImport Held(string why)
    {
        if (Said.TryAdd(home + "\n" + why, 0))
        {
            using var log = new MachineLog(home, "driver");
            log.Warn("bindings.held", ("why", why));
        }

        return new BindingImport(0, 0, why);
    }
}
