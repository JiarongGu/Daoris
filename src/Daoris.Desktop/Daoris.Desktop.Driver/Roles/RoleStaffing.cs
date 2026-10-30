using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// <c>driver.json</c>'s four staffing paths as that file shows them (FOUNDATION1w-s, the design §19.15): each path's own JSON text, compact
/// (<see cref="RoleStaffing.Compact"/>), or null where the file does not hold the path.
/// </summary>
/// <param name="Actors">The <c>actors</c> object, every entry of it as found, an unreadable one and an id this build does not know included.</param>
/// <param name="Adapter">The <c>adapter</c> value.</param>
/// <param name="IntakeAdapter">The <c>intakeAdapter</c> value.</param>
/// <param name="HelperAdapter">The <c>helperAdapter</c> value.</param>
public sealed record StaffingPaths(string? Actors, string? Adapter, string? IntakeAdapter, string? HelperAdapter);

/// <summary>
/// The staffing the store holds (§19.15): the five fields of today's loaded record, each as <c>driver.json</c> holds them.
/// </summary>
/// <param name="Actors">Each readable entry, its choices (<c>[]</c>, off, for Intake and Ask Daoris alone).</param>
/// <param name="Errors">Each entry that does not read, with why: it holds the starts that read it (<see cref="ActorUnreadableException"/>).</param>
/// <param name="AsFound">Each entry that does not read as the file showed it, compact JSON, which the store keeps whole.</param>
/// <param name="Adapter">Build's first-agent key, Chat's and Brainstorm's too where they are <i>as before</i>; none where the file showed none.</param>
/// <param name="IntakeAdapter">Intake's first-agent key; none where the file showed none.</param>
/// <param name="HelperAdapter">Ask Daoris's first-agent key; none where the file showed none.</param>
/// <param name="Written">The four paths as the projection last wrote them, or found them at the import.</param>
public sealed record StaffingRead(
    IReadOnlyDictionary<string, IReadOnlyList<ActorChoice>> Actors,
    IReadOnlyDictionary<string, string> Errors,
    IReadOnlyDictionary<string, string> AsFound,
    string? Adapter,
    string? IntakeAdapter,
    string? HelperAdapter,
    StaffingPaths Written);

/// <summary>How an import of the file's staffing came out.</summary>
public enum StaffingImported
{
    /// <summary>The store holds the file's staffing now.</summary>
    Done,

    /// <summary>The store held it already: it is imported once.</summary>
    Already,

    /// <summary>The home has no <c>driver.json</c>, so there is nothing to import.</summary>
    NoFile,

    /// <summary>The file does not read as a configuration: nothing was imported, and it is left as it is.</summary>
    Unread,

    /// <summary>Not made now: the start's confirmation is not settled, the staffing's lock stayed held or the store would not take it.</summary>
    Held,
}

/// <summary>An import's outcome and, where it was held or unread, why.</summary>
public sealed record StaffingImport(StaffingImported How, string? Said = null);

/// <summary>Where the check runs (§19.15): a look takes both locks with no wait; a start and a write's end wait as a writer does.</summary>
public enum StaffingCheckAt
{
    Start,
    Look,
    Write,
}

/// <summary>How a check came out.</summary>
public enum StaffingChecks
{
    /// <summary>The file's staffing paths are at <c>written</c>: nothing to do.</summary>
    Same,

    /// <summary>They are at the store's projection, a write's own cut before <c>written</c> moved: <c>written</c> set, nothing found.</summary>
    Written,

    /// <summary>Another writer changed them: kept in a found row, every turn-off applied, the file repaired where its baseline still stood.</summary>
    Found,

    /// <summary>A lock was held, or the store would not read: the comparison is left to the next check.</summary>
    Left,

    /// <summary>The file does not read: nothing is found and nothing written, as a look says it today.</summary>
    Unread,

    /// <summary>The store holds no import yet: the file is the staffing, and a start or a look imports it.</summary>
    NotImported,
}

/// <summary>A check's outcome, the found row it recorded or met, and what a refused repair or a held lock said.</summary>
public sealed record StaffingChecked(StaffingChecks How, long? Found = null, string? Said = null);

/// <summary>
/// The roles' staffing in the desktop store (FOUNDATION1 w-s part 1, D212; the design §19.15, §13.3, §13.5, §13.6): <c>role_staffing</c>
/// holding what <c>driver.json</c>'s staffing holds today, its projection row <c>written</c> and its found rows, imported once from the
/// file. Read through Settings' port by every load of a wired home's file (part 3); written by <see cref="Set(string, IReadOnlyList{ActorChoice}?)"/>
/// alone, every writer reaching it through <see cref="SetAt(string, string, IReadOnlyList{ActorChoice}?)"/> (part 4); checked at each start,
/// look and write's end (part 5).
/// </summary>
/// <remarks>
/// <para><b>Imported once, once the start's confirmation is settled</b> (§13.5, §19.7), under the staffing's record lock
/// (<c>locks/records/role-staffing.lock</c>), where nothing was imported yet: one store transaction reads the file's staffing through
/// today's reader (<see cref="ActorChoices.Read"/>) into <c>role_staffing</c>, an entry that does not read kept as found with why, and
/// records the file's four staffing paths as <c>written</c>. <b>The file is never written</b>: it already is the projection of what was
/// imported (<see cref="ImportClass.Projected"/>). A kill before the commit leaves the store as it was, and the next start imports again.</para>
/// <para><b>Only the home's own <c>driver.json</c></b>: a file <c>DAORIS_DRIVER_CONFIG</c> names is read and written as today, with no store.
/// A home with no <c>driver.json</c>, or one that does not read, imports nothing and is asked again at the next start.</para>
/// <para><b>Opened for one use and closed</b>, as every family's store is, a read never creating or writing the store.</para>
/// </remarks>
/// <param name="home">The home whose store and <c>driver.json</c> these are.</param>
/// <param name="settled">The reading of the start's confirmation; null reads this install's swap journal.</param>
public sealed class RoleStaffing(string home, Func<StoreSettled>? settled = null) : IRoleStaffing
{
    /// <summary>
    /// Wire the staffing of <paramref name="home"/>'s own <c>driver.json</c> to Settings' port (FOUNDATION1w-s part 3), once before the root's
    /// first load: from then on a load of that file fills its five staffing fields from the store where it holds an import. A file
    /// <c>DAORIS_DRIVER_CONFIG</c> names is another path and is never wired.
    /// </summary>
    public static RoleStaffing Wire(string home, Func<StoreSettled>? settled = null)
    {
        var staffing = new RoleStaffing(home, settled);
        RoleStaffingPort.Wire(staffing.FileOfHome, staffing);
        return staffing;
    }

    /// <summary>The port's read: the store's staffing as a loaded record holds it, or null before the import.</summary>
    StaffingHeld? IRoleStaffing.Held() => Read() is { } read ? new StaffingHeld(read.Adapter, read.IntakeAdapter, read.HelperAdapter, read.Actors, read.Errors) : null;

    /// <summary>The family's name in <c>imports</c> and <c>store_revisions</c>.</summary>
    public const string Family = "role_staffing";

    /// <summary>The staffing's record lock, <c>locks/records/role-staffing.lock</c> (§19.15's first step of a write).</summary>
    public const string Record = "role-staffing";

    /// <summary>The store's version this family's tables arrive at.</summary>
    private const int Step = 8;

    private static readonly JsonWriterOptions Compacted = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>How long the import waits for another writer of the staffing before it holds, nothing written.</summary>
    internal TimeSpan LockWait { get; init; } = RecordLock.Wait;

    /// <summary>Run inside the import's transaction after its rows are written and before its commit: the seam a test kills the import at.</summary>
    internal Action? BeforeCommit { get; init; }

    private Func<StoreSettled> Settled => settled ?? StartConfirmation.Read;

    /// <summary>The store's file in this home.</summary>
    public string Location => DesktopStore.LocationIn(home);

    /// <summary>The home's own <c>driver.json</c>, which the import reads and never writes.</summary>
    public string FileOfHome => DaorisHome.Entry(home, HomeEntry.DriverConfig);

    /// <summary>
    /// Whether this home keeps its staffing in the store: the store is there at the family's version and holds an import of it. A store
    /// that cannot be read says no.
    /// </summary>
    public bool InUse()
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            return store is not null && Imported(store);
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The staffing the store holds, or null before the import; the store's failure is thrown.</summary>
    public StaffingRead? Read()
    {
        using var store = DesktopStoreReader.Open(Location);
        if (store is null || !Imported(store)) return null;

        var actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(StringComparer.Ordinal);
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        string? adapter = null;
        string? intake = null;
        string? helper = null;
        foreach (var (role, state, choices, why, first) in store.Query(
                     "SELECT role, state, choices, why, first_agent FROM role_staffing ORDER BY role",
                     row => (row.GetString(0), row.GetString(1), Text(row, 2), Text(row, 3), Text(row, 4))))
        {
            switch (role)
            {
                case "build": adapter = first; break;
                case "intake": intake = first; break;
                case "ask-daoris": helper = first; break;
            }

            if (state == "set")
            {
                if (Choices(choices) is { } list) actors[role] = list;
                else errors[role] = "is not a list of choices the store can read";
            }
            else if (state == "unreadable")
            {
                errors[role] = why ?? "does not read";
                found[role] = choices ?? "null";
            }
        }

        var written = store.Query(
            "SELECT actors, adapter, intake_adapter, helper_adapter FROM role_staffing_projection",
            row => new StaffingPaths(Text(row, 0), Text(row, 1), Text(row, 2), Text(row, 3))).FirstOrDefault()
            ?? new StaffingPaths(null, null, null, null);
        return new StaffingRead(actors, errors, found, adapter, intake, helper, written);
    }

    /// <summary>
    /// Import the home's <c>driver.json</c> staffing now, once the start's confirmation is settled and under the staffing's record lock, where
    /// the store holds none yet. Nothing is written for a home with no file, a file that does not read, a start not settled, a lock held past
    /// <see cref="LockWait"/> or a store that will not take it. Throws what the store's own failure is not: a kill, here a test's seam.
    /// </summary>
    public StaffingImport ImportFile() => ImportFile(LockWait);

    /// <summary><see cref="ImportFile()"/>, waiting <paramref name="wait"/> for the staffing's lock: a look's import waits for none.</summary>
    internal StaffingImport ImportFile(TimeSpan wait)
    {
        var path = FileOfHome;
        if (!File.Exists(path)) return new StaffingImport(StaffingImported.NoFile);
        if (Settled() is { Is: false } waiting) return Held(waiting.Said ?? "the start's confirmation is not settled");

        try
        {
            using var _ = RecordLock.Take(RecordLock.PathOf(home, Record), wait);
            if (InUse()) return new StaffingImport(StaffingImported.Already);

            byte[] bytes;
            try
            {
                bytes = AtomicFile.ReadBytes(path);
            }
            catch (FileNotFoundException)
            {
                return new StaffingImport(StaffingImported.NoFile);
            }

            if (Parse(bytes, out var staffing) is { } unread) return new StaffingImport(StaffingImported.Unread, unread);

            var receipt = Path.GetRelativePath(home, path).Replace('\\', '/');
            var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            using var store = DesktopStore.Open(Location, StoreProcess.Options(Settled));
            var outcome = store.Import(
                new StoreImport(Family, receipt, bytes.Length, sha, staffing.Rows.Count, ImportClass.Projected),
                write =>
                {
                    var at = Now();
                    foreach (var row in staffing.Rows)
                    {
                        write.Execute(
                            "INSERT INTO role_staffing (role, state, choices, why, first_agent, at) VALUES ($role, $state, $choices, $why, $first, $at)",
                            ("$role", row.Role), ("$state", row.State), ("$choices", row.Choices), ("$why", row.Why), ("$first", row.First), ("$at", at));
                    }

                    write.Execute(
                        """
                        INSERT INTO role_staffing_projection (one, actors, adapter, intake_adapter, helper_adapter, at)
                        VALUES (1, $actors, $adapter, $intake, $helper, $at)
                        """,
                        ("$actors", staffing.Paths.Actors), ("$adapter", staffing.Paths.Adapter), ("$intake", staffing.Paths.IntakeAdapter),
                        ("$helper", staffing.Paths.HelperAdapter), ("$at", at));
                    BeforeCommit?.Invoke();
                });
            return outcome.Done ? new StaffingImport(StaffingImported.Done) : Held(outcome.Said ?? "the store would not take it");
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return Held(error.Message);
        }
    }

    /// <summary>The file step's seams, a test's: where a write's rename is cut or another writer lands.</summary>
    internal KeepSeams? Seams { get; init; }

    /// <summary>How long a write's file step waits for <c>driver.json</c>'s keeping lock; the keeping wait where null.</summary>
    internal TimeSpan? FileWait { get; init; }

    /// <summary>Run by the check after its found row's commit and before the file's replacement: the seam a test kills the repair at.</summary>
    internal Action? AfterFound { get; init; }

    /// <summary>Run by the check after the file's replacement and before <c>written</c>'s update: the second seam of the repair.</summary>
    internal Action? AfterRepair { get; init; }

    /// <summary>The staffing wired for <paramref name="file"/>, or null where none is: a file nobody wired, one the variable names among them.</summary>
    public static RoleStaffing? Of(string file) => RoleStaffingPort.Of(file) as RoleStaffing;

    /// <summary>
    /// A composition root's wiring (§19.15): the home its environment names, wired before its first load, or nothing where
    /// <c>DAORIS_DRIVER_CONFIG</c> names a file, which is read and written as today with no store, or where no home is named.
    /// </summary>
    public static RoleStaffing? WireHome(Func<string, string?> environment) =>
        DriverConfig.Overridden(environment) || DriverConfig.FindHome(environment) is not { } home ? null : Wire(home);

    /// <summary>
    /// The staffing's writers' one door (§19.15, every writer of the census): <see cref="Set(string, IReadOnlyList{ActorChoice}?)"/> where
    /// <paramref name="file"/> is wired, else the file's own write as today. The actors a change of Build's agent made explicit.
    /// </summary>
    /// <exception cref="DriverException">The judge's refusal, a lock held past its wait, or a write not done; said.</exception>
    public static IReadOnlyList<string> SetAt(string file, IReadOnlyList<(string Actor, IReadOnlyList<ActorChoice>? Choices)> changes)
    {
        if (Of(file) is { } staffing) return staffing.Set(changes);
        var kept = new List<string>();
        StandingChoices.Write(file, config => Changed(config, changes, kept));
        return kept;
    }

    /// <summary><see cref="SetAt(string, IReadOnlyList{ValueTuple{string, IReadOnlyList{ActorChoice}}})"/> for one actor.</summary>
    public static IReadOnlyList<string> SetAt(string file, string actor, IReadOnlyList<ActorChoice>? choices) => SetAt(file, [(actor, choices)]);

    /// <summary><see cref="Set(IReadOnlyList{ValueTuple{string, IReadOnlyList{ActorChoice}}})"/> for one actor.</summary>
    public IReadOnlyList<string> Set(string actor, IReadOnlyList<ActorChoice>? choices) => Set([(actor, choices)]);

    /// <summary>
    /// The one write of the staffing (§19.15, steps 1 to 5): the staffing lock, a writer waiting past <see cref="LockWait"/> refused with
    /// nothing written; each change by today's <c>ActorChoices.With</c>; the file before the store, against <c>written</c>; the commit; the
    /// check. Before the import the file is the staffing, and is written as today under the same lock.
    /// </summary>
    /// <exception cref="DriverException">The judge's refusal, a lock held past its wait, or a write not done; said.</exception>
    public IReadOnlyList<string> Set(IReadOnlyList<(string Actor, IReadOnlyList<ActorChoice>? Choices)> changes)
    {
        foreach (var (actor, choices) in changes) ActorChoices.Judged(actor, choices);
        using var held = Lock();
        var kept = new List<string>();
        if (Read() is not { } read)
        {
            StandingChoices.Write(FileOfHome, config => Changed(config, changes, kept));
            return kept;
        }

        Commit(read, Changed(StaffingProjection.Config(read), changes, kept), read.AsFound, also: null);
        return kept;
    }

    /// <summary>
    /// The check (§19.15): at a start, a look of the lease holder's loop and a write's end, the file's four staffing paths compared with
    /// <c>written</c> on <paramref name="source"/>, the text already read where there is one, and only where they differ both locks taken
    /// and the comparison made again. A look takes them with no wait and leaves a held one to the next look. Before the import a start or a
    /// look imports, once the start's confirmation is settled.
    /// </summary>
    public StaffingChecked Check(StaffingCheckAt at, string? source = null)
    {
        StaffingRead? read;
        try
        {
            read = Read();
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return new StaffingChecked(StaffingChecks.Left, Said: error.Message);
        }

        if (read is null)
        {
            if (at == StaffingCheckAt.Write) return new StaffingChecked(StaffingChecks.NotImported);
            return new StaffingChecked(StaffingChecks.NotImported, Said: ImportFile(at == StaffingCheckAt.Look ? TimeSpan.Zero : LockWait).Said);
        }

        if (Shows(source) is not { } shown) return new StaffingChecked(StaffingChecks.Unread);
        if (shown == read.Written) return new StaffingChecked(StaffingChecks.Same);

        RecordLock held;
        try
        {
            held = RecordLock.Take(RecordLock.PathOf(home, Record), at == StaffingCheckAt.Look ? TimeSpan.Zero : LockWait);
        }
        catch (IOException error)
        {
            return new StaffingChecked(StaffingChecks.Left, Said: error.Message);
        }

        using (held) return CheckHolding(at, keepWait: null);
    }

    /// <summary>The found rows not yet taken or dismissed, oldest first, each with what it changed; none before the import.</summary>
    public IReadOnlyList<StaffingFound> Found()
    {
        using var store = DesktopStoreReader.Open(Location);
        if (store is null || !Imported(store)) return [];
        return [.. store.Query(
                "SELECT id, at, actors, adapter, intake_adapter, helper_adapter, written_actors, written_adapter, written_intake_adapter, written_helper_adapter"
                + " FROM role_staffing_found WHERE answered_at IS NULL ORDER BY id",
                row => (Id: row.GetInt64(0), At: row.GetString(1), Paths: StaffingProjection.Paths(row, 2), Written: StaffingProjection.Paths(row, 6)))
            .Select(row => new StaffingFound(row.Id, row.At, row.Paths, row.Written, StaffingProjection.Changes(row.Written, row.Paths)))];
    }

    /// <summary>
    /// <i>Take it</i> (§19.15, C8-1.3, O8-F4): a write that makes the store's staffing what the found file showed, path by path where it
    /// differs from the row's <c>written</c>, read from the row and never from the file, with no <c>With</c> and no <c>Keep</c>; a turn-on
    /// among them by this press. Steps 1 and 3 to 5 as any write's.
    /// </summary>
    /// <exception cref="DriverException">No such row waits, a lock held past its wait, or the write not done; said.</exception>
    public void Take(long id)
    {
        using var held = Lock();
        var read = Read() ?? throw NotKept();
        var row = Found().FirstOrDefault(each => each.Id == id) ?? throw NoSuchFound(id);
        var (after, asFound) = StaffingProjection.Taken(read, row.Paths, row.Written);
        Commit(read, after, asFound, also: write => Answer(write, id, "taken"));
    }

    /// <summary><i>Dismiss</i>: the found row answered, the store's staffing unchanged.</summary>
    /// <exception cref="DriverException">No such row waits, or a lock held past its wait; said.</exception>
    public void Dismiss(long id)
    {
        using var held = Lock();
        if (Read() is null) throw NotKept();
        if (Found().All(each => each.Id != id)) throw NoSuchFound(id);
        using var store = DesktopStore.Open(Location, StoreProcess.Options(Settled));
        store.Write(Family, write => Answer(write, id, "dismissed"));
    }

    private static DriverException NotKept() => new("this home's staffing is not kept in the store yet, so there is nothing found to take or dismiss.");

    private static DriverException NoSuchFound(long id) =>
        new($"no change found numbered {id} waits to be taken or dismissed; `daoris driver actor found` lists the ones that do.");

    private static void Answer(DesktopWrite write, long id, string answer) => write.Execute(
        "UPDATE role_staffing_found SET answer = $answer, answered_at = $at WHERE id = $id AND answered_at IS NULL",
        ("$answer", answer), ("$at", Now()), ("$id", id));

    private static DriverConfig Changed(
        DriverConfig config, IReadOnlyList<(string Actor, IReadOnlyList<ActorChoice>? Choices)> changes, List<string> kept)
    {
        foreach (var (actor, choices) in changes)
        {
            config = ActorChoices.With(config, actor, choices, out var each);
            kept.AddRange(each.Where(name => !kept.Contains(name)));
        }

        return config;
    }

    /// <summary>The staffing's record lock, held to the write's end; a writer that waits past <see cref="LockWait"/> is refused.</summary>
    private RecordLock Lock()
    {
        try
        {
            return RecordLock.Take(RecordLock.PathOf(home, Record), LockWait);
        }
        catch (IOException error)
        {
            throw new DriverException($"another writer of the roles' staffing held its lock for the whole of the wait, so nothing was written; try again ({error.Message}).");
        }
    }

    /// <summary>
    /// Steps 3 to 5 under the staffing lock: the projection onto the file against <c>written</c>, then one store transaction that writes the
    /// staffing and sets <c>written</c>, then the check. A file whose paths moved, or whose lock was taken, is not written and the press is
    /// not done; the check runs then too.
    /// </summary>
    private void Commit(StaffingRead read, DriverConfig after, IReadOnlyDictionary<string, string> asFound, Action<DesktopWrite>? also)
    {
        var projection = StaffingProjection.Project(after, asFound, read.Written.Actors);
        var filed = StaffingFile.Project(FileOfHome, read.Written, projection, FileWait, Seams);
        if (filed.How == StaffingFiled.Unread) throw new DriverException($"{filed.Said} Nothing was written.");
        if (filed.How is StaffingFiled.Moved or StaffingFiled.Refused)
        {
            // A lock held past the file step's wait is still held: the check leaves the comparison to the next one rather than wait again.
            CheckHolding(StaffingCheckAt.Write, filed.How == StaffingFiled.Refused ? TimeSpan.Zero : null);
            throw new DriverException($"the roles' staffing was not written: {filed.Said}");
        }

        try
        {
            using var store = DesktopStore.Open(Location, StoreProcess.Options(Settled));
            store.Write(Family, write =>
            {
                var at = Now();
                StaffingProjection.Rows(write, after, asFound, at);
                StaffingProjection.Written(write, projection, at);
                also?.Invoke(write);
                BeforeCommit?.Invoke();
            });
        }
        catch (Exception error) when (Stored(error))
        {
            // The file holds a projection the store does not: this write's own check keeps it as found, so the next write proceeds.
            CheckHolding(StaffingCheckAt.Write, keepWait: null);
            throw new DriverException(
                $"the roles' staffing was written to {Path.GetFileName(FileOfHome)} and the store would not take it ({error.Message}), so it is "
                + "not done; what the file showed is kept as found.");
        }

        CheckHolding(StaffingCheckAt.Write, keepWait: null);
    }

    private static bool Stored(Exception error) => error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException;

    /// <summary>
    /// The check's comparison under the staffing lock, <c>driver.json</c>'s keeping lock taken (no wait at a look): at <c>written</c>
    /// nothing; at the store's projection <c>written</c> set; otherwise a found row kept whole, every turn-off it shows applied, the projection
    /// written over the file against the paths observed, and <c>written</c> set in a transaction of its own. A repair whose baseline no longer
    /// stands writes nothing and runs no check again.
    /// </summary>
    private StaffingChecked CheckHolding(StaffingCheckAt at, TimeSpan? keepWait)
    {
        KeepLock keep;
        try
        {
            keep = KeepLock.Take(FileOfHome, keepWait ?? (at == StaffingCheckAt.Look ? TimeSpan.Zero : FileWait));
        }
        catch (IOException error)
        {
            return new StaffingChecked(StaffingChecks.Left, Said: error.Message);
        }

        using (keep)
        {
            try
            {
                return Compared(keep);
            }
            catch (Exception error) when (Stored(error))
            {
                return new StaffingChecked(StaffingChecks.Left, Said: error.Message);
            }
        }
    }

    private StaffingChecked Compared(KeepLock keep)
    {
        if (Read() is not { } read) return new StaffingChecked(StaffingChecks.NotImported);
        if (Shows(null) is not { } shown) return new StaffingChecked(StaffingChecks.Unread);
        if (shown == read.Written) return new StaffingChecked(StaffingChecks.Same);

        var staffing = StaffingProjection.Config(read);
        using var store = DesktopStore.Open(Location, StoreProcess.Options(Settled));
        if (shown == StaffingProjection.Project(staffing, read.AsFound, read.Written.Actors))
        {
            store.Write(Family, write => StaffingProjection.Written(write, shown, Now()));
            return new StaffingChecked(StaffingChecks.Written);
        }

        var off = StaffingProjection.TurnedOff(read.Written, shown);
        var after = off.Aggregate(staffing, (config, actor) => ActorChoices.With(config, actor, []));
        var id = store.Write(Family, write =>
        {
            var now = Now();
            var found = StaffingProjection.Recorded(write, shown, read.Written, now);
            if (off.Count > 0) StaffingProjection.Rows(write, after, read.AsFound, now);
            return found;
        });

        AfterFound?.Invoke();
        var repaired = StaffingProjection.Project(after, read.AsFound, read.Written.Actors);
        var filed = StaffingFile.ProjectHolding(FileOfHome, keep, shown, repaired);
        if (filed.How is not (StaffingFiled.Written or StaffingFiled.Unchanged)) return new StaffingChecked(StaffingChecks.Found, id, filed.Said);

        AfterRepair?.Invoke();
        store.Write(Family, write => StaffingProjection.Written(write, repaired, Now()));
        return new StaffingChecked(StaffingChecks.Found, id);
    }

    /// <summary>The four paths <paramref name="source"/> shows, or the file now where none is given; a file that is gone shows none, null where it does not read.</summary>
    private StaffingPaths? Shows(string? source)
    {
        try
        {
            var text = source ?? (File.Exists(FileOfHome) ? DecodedFile() : "{}");
            return StaffingFile.PathsOf(text);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or DecoderFallbackException or IOException)
        {
            return null;
        }
    }

    private string DecodedFile()
    {
        using var reader = new StreamReader(new MemoryStream(AtomicFile.ReadBytes(FileOfHome)), new UTF8Encoding(false));
        return reader.ReadToEnd();
    }

    /// <summary>
    /// A JSON element as <c>driver.json</c>'s staffing paths are kept here, compact and with the relaxed escaping, so two spellings of one
    /// value (whitespace, an escaped letter) are one text. The projection's and a found row's paths are this text.
    /// </summary>
    public static string Compact(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Compacted)) element.WriteTo(writer);
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    private sealed record Row(string Role, string State, string? Choices, string? Why, string? First);

    private sealed record Staffing(IReadOnlyList<Row> Rows, StaffingPaths Paths);

    /// <summary>
    /// The file's staffing as today's build reads it (<see cref="JsonKeeping.Once"/>, <see cref="ActorChoices.Read"/> and the three keys as
    /// <see cref="DriverConfig.Parse"/> reads them), or why the file does not read.
    /// </summary>
    private static string? Parse(byte[] bytes, out Staffing staffing)
    {
        staffing = new Staffing([], new StaffingPaths(null, null, null, null));
        try
        {
            using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false));
            using var document = JsonDocument.Parse(JsonKeeping.Once(reader.ReadToEnd()));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return "it holds no JSON object";

            var (actors, errors) = ActorChoices.Read(root);
            var keys = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["build"] = Json.NonEmpty(root, "adapter"),
                ["intake"] = Json.NonEmpty(root, "intakeAdapter")?.Trim() is { Length: > 0 } intake ? intake : null,
                ["ask-daoris"] = Json.NonEmpty(root, "helperAdapter")?.Trim() is { Length: > 0 } helper ? helper : null,
            };

            var rows = new List<Row>();
            foreach (var id in ActorChoices.Ids)
            {
                var first = keys.GetValueOrDefault(id);
                if (errors.TryGetValue(id, out var why)) rows.Add(new Row(id, "unreadable", Compact(root.GetProperty("actors").GetProperty(id)), why, first));
                else if (actors.TryGetValue(id, out var choices)) rows.Add(new Row(id, "set", Write(choices), null, first));
                else if (first is not null) rows.Add(new Row(id, "before", null, null, first));
            }

            string? Path(string name) => root.TryGetProperty(name, out var value) ? Compact(value) : null;
            staffing = new Staffing(rows, new StaffingPaths(Path("actors"), Path("adapter"), Path("intakeAdapter"), Path("helperAdapter")));
            return null;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or DecoderFallbackException)
        {
            return $"it does not read: {error.Message.TrimEnd('.', ' ')}";
        }
    }

    /// <summary>An entry's choices as the store keeps them: a JSON list of <c>{ agent, model?, effort? }</c>, as <c>driver.json</c> writes them.</summary>
    internal static string Write(IReadOnlyList<ActorChoice> choices)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Compacted))
        {
            writer.WriteStartArray();
            foreach (var choice in choices)
            {
                writer.WriteStartObject();
                writer.WriteString("agent", choice.Agent);
                if (choice.Model is { } model) writer.WriteString("model", model);
                if (choice.Effort is { } effort) writer.WriteString("effort", effort);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// <summary>The choices a stored list holds, or null where the text is not one the store wrote.</summary>
    private static IReadOnlyList<ActorChoice>? Choices(string? text)
    {
        if (text is null) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<ActorChoice>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Json.Text(item, "agent") is not { } agent) return null;
                list.Add(new ActorChoice(agent, Json.Text(item, "model"), Json.Text(item, "effort")));
            }

            return list;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Imported(DesktopStoreReader store) =>
        store.Version >= Step
        && store.Query("SELECT 1 FROM imports WHERE family = $family LIMIT 1", row => row.GetInt32(0), ("$family", Family)).Count > 0;

    private static string? Text(SqliteDataReader row, int column) => row.IsDBNull(column) ? null : row.GetString(column);

    private static string Now() => DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static StaffingImport Held(string why) => new(StaffingImported.Held, why);
}
