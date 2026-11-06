using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The staffing the store holds (§19.15): the actors of today's loaded record, each as <c>driver.json</c> holds them.
/// </summary>
/// <param name="Actors">Each readable entry, its choices (<c>[]</c>, off, for Intake and Ask Daoris alone).</param>
/// <param name="Errors">Each entry that does not read, with why: it holds the starts that read it (<see cref="ActorUnreadableException"/>).</param>
/// <param name="AsFound">Each entry that does not read as the file showed it, compact JSON, which the store keeps whole.</param>
public sealed record StaffingRead(
    IReadOnlyDictionary<string, IReadOnlyList<ActorChoice>> Actors,
    IReadOnlyDictionary<string, string> Errors,
    IReadOnlyDictionary<string, string> AsFound);

/// <summary>One role's row of <c>role_staffing</c> as the store keeps it: <c>set</c>, <c>before</c> or <c>unreadable</c>, its choices and first-agent key.</summary>
internal sealed record StaffingRow(string Role, string State, string? Choices, string? Why, string? First);

/// <summary>How an import of the file's staffing came out.</summary>
public enum StaffingImported
{
    /// <summary>The store holds the file's staffing now, or a fresh home's seeds.</summary>
    Done,

    /// <summary>The store held it already: it is imported once.</summary>
    Already,

    /// <summary>The file does not read as a configuration: nothing was imported, and it is left as it is.</summary>
    Unread,

    /// <summary>Not made now: the start's confirmation is not settled, the staffing's lock stayed held or the store would not take it.</summary>
    Held,
}

/// <summary>An import's outcome and, where it was held or unread, why.</summary>
public sealed record StaffingImport(StaffingImported How, string? Said = null);

/// <summary>Where the check runs (§19.15): a look imports with no wait for the staffing's lock; a start waits as a writer does.</summary>
public enum StaffingCheckAt
{
    Start,
    Look,
    Write,
}

/// <summary>How a check came out.</summary>
public enum StaffingChecks
{
    /// <summary>The store holds the staffing: nothing to do.</summary>
    Same,

    /// <summary>The store would not read: the check is left to the next one.</summary>
    Left,

    /// <summary>The store holds no import yet: a start or a look imports it.</summary>
    NotImported,
}

/// <summary>A check's outcome, and what a held import or a store that would not read said.</summary>
public sealed record StaffingChecked(StaffingChecks How, string? Said = null);

/// <summary>
/// The roles' staffing in the desktop store (FOUNDATION1 w-s part 1, D212; the design §19.15, §13.3, §13.5, §13.6): <c>role_staffing</c>,
/// imported once from the file. Read through Settings' port by every load of a wired home's file (part 3); written by
/// <see cref="Set(string, IReadOnlyList{ActorChoice}?)"/> alone, every writer reaching it through
/// <see cref="SetAt(string, string, IReadOnlyList{ActorChoice}?)"/> (part 4), in the store alone (D232).
/// </summary>
/// <remarks>
/// <para><b>Imported once, once the start's confirmation is settled</b> (§13.5, §19.7), under the staffing's record lock
/// (<c>locks/records/role-staffing.lock</c>), where nothing was imported yet: one store transaction reads the file's staffing through
/// today's reader (<see cref="ActorChoices.Read"/>) into <c>role_staffing</c>, an entry that does not read kept as found with why.
/// <b>The file is never written</b>: it already is the projection of what was imported (<see cref="ImportClass.Projected"/>). A kill before the commit leaves the store as it was, and the next start imports again.</para>
/// <para><b>The home's own <c>driver.json</c></b>, the only one a home has (ONEWAY1c-i). One that does not read imports nothing and is
/// asked again at the next start. A fresh home, with no file or one holding none of the four keys, is seeded instead (ONEWAY1b): each of
/// the five roles with an order of its own written at its seed (<see cref="ActorChoices.Seed"/>), recorded <see cref="ImportClass.Seeded"/>.</para>
/// <para><b>Opened for one use and closed</b>, as every family's store is, a read never creating or writing the store.</para>
/// </remarks>
/// <param name="home">The home whose store and <c>driver.json</c> these are.</param>
/// <param name="settled">The reading of the start's confirmation; null reads this install's swap journal.</param>
public sealed class RoleStaffing(string home, Func<StoreSettled>? settled = null) : IRoleStaffing
{
    /// <summary>
    /// Wire the staffing of <paramref name="home"/>'s own <c>driver.json</c> to Settings' port (FOUNDATION1w-s part 3), once before the root's
    /// first load: from then on a load of that file fills its five staffing fields from the store where it holds an import.
    /// </summary>
    public static RoleStaffing Wire(string home, Func<StoreSettled>? settled = null)
    {
        var staffing = new RoleStaffing(home, settled);
        RoleStaffingPort.Wire(staffing.FileOfHome, staffing);
        return staffing;
    }

    /// <summary>The port's read: the store's staffing as a loaded record holds it, or null before the import.</summary>
    StaffingHeld? IRoleStaffing.Held() => Read() is { } read ? new StaffingHeld(read.Actors, read.Errors) : null;

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

    /// <summary>The reading of the start's confirmation this staffing writes under, which the catalogue's write beside it takes too.</summary>
    internal Func<StoreSettled> Confirmation => Settled;

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
        foreach (var (role, state, choices, why) in store.Query(
                     "SELECT role, state, choices, why FROM role_staffing ORDER BY role",
                     row => (row.GetString(0), row.GetString(1), Text(row, 2), Text(row, 3))))
        {
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

        return new StaffingRead(actors, errors, found);
    }

    /// <summary>The seeded roles' rows as the store keeps them, by role; none before the import. The staffing part's before and after.</summary>
    internal IReadOnlyList<StaffingRow> Rows()
    {
        using var store = DesktopStoreReader.Open(Location);
        if (store is null || !Imported(store)) return [];
        return [.. store.Query(
                "SELECT role, state, choices, why, first_agent FROM role_staffing ORDER BY role",
                row => new StaffingRow(row.GetString(0), row.GetString(1), Text(row, 2), Text(row, 3), Text(row, 4)))
            .Where(row => ActorChoices.Ids.Contains(row.Role, StringComparer.Ordinal))];
    }

    /// <summary>Whether this home's staffing was seeded fresh (ONEWAY1b): its file never held it, so no older build reads it there.</summary>
    public bool Seeded()
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            return store is not null && store.Version >= Step && store.Query(
                "SELECT 1 FROM imports WHERE family = $family AND class = $class LIMIT 1", row => row.GetInt32(0), ("$family", Family), ("$class", ImportClass.Seeded)).Count > 0;
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException or DesktopStoreException)
        {
            return false;
        }
    }

    /// <summary>
    /// The migration's write (<see cref="StaffingStep"/>): each named role's row replaced by its own, none where it is null, in one
    /// transaction. The caller holds the staffing's lock.
    /// </summary>
    internal void Replace(IReadOnlyList<(string Role, StaffingRow? Row)> rows)
    {
        using var store = DesktopStore.Open(Location, StoreProcess.Options(Settled));
        store.Write(Family, write =>
        {
            var at = Now();
            foreach (var (role, row) in rows)
            {
                write.Execute("DELETE FROM role_staffing WHERE role = $role", ("$role", role));
                if (row is null) continue;
                write.Execute(
                    "INSERT INTO role_staffing (role, state, choices, why, first_agent, at) VALUES ($role, $state, $choices, $why, $first, $at)",
                    ("$role", role), ("$state", row.State), ("$choices", row.Choices), ("$why", row.Why), ("$first", row.First), ("$at", at));
            }
        });
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
        if (Settled() is { Is: false } waiting) return Held(waiting.Said ?? "the start's confirmation is not settled");

        try
        {
            using var _ = RecordLock.Take(RecordLock.PathOf(home, Record), wait);
            if (InUse()) return new StaffingImport(StaffingImported.Already);

            byte[]? bytes;
            try
            {
                bytes = AtomicFile.ReadBytes(path);
            }
            catch (Exception gone) when (gone is FileNotFoundException or DirectoryNotFoundException)
            {
                bytes = null;
            }

            Staffing staffing;
            if (bytes is null) staffing = Seeds();
            else if (Parse(bytes, out var parsed) is { } unread) return new StaffingImport(StaffingImported.Unread, unread);
            else staffing = parsed;

            bytes ??= [];

            // ONEWAY1b: a fresh home, whose file holds none of the four keys, starts on its seeds, written; nothing of it is copied.
            var fresh = !staffing.Holds;
            if (fresh) staffing = Seeds();
            var receipt = Path.GetRelativePath(home, path).Replace('\\', '/');
            var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            using var store = DesktopStore.Open(Location, StoreProcess.Options(Settled));
            var outcome = store.Import(
                new StoreImport(Family, receipt, bytes.Length, sha, staffing.Rows.Count, fresh ? ImportClass.Seeded : ImportClass.Projected),
                write =>
                {
                    var at = Now();
                    foreach (var row in staffing.Rows)
                    {
                        write.Execute(
                            "INSERT INTO role_staffing (role, state, choices, why, first_agent, at) VALUES ($role, $state, $choices, $why, $first, $at)",
                            ("$role", row.Role), ("$state", row.State), ("$choices", row.Choices), ("$why", row.Why), ("$first", row.First), ("$at", at));
                    }

                    BeforeCommit?.Invoke();
                });
            return outcome.Done ? new StaffingImport(StaffingImported.Done) : Held(outcome.Said ?? "the store would not take it");
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return Held(error.Message);
        }
    }

    /// <summary>The staffing wired for <paramref name="file"/>, or null where a composition root wired none.</summary>
    public static RoleStaffing? Of(string file) => RoleStaffingPort.Of(file) as RoleStaffing;

    /// <summary>
    /// A composition root's wiring (§19.15): the home its environment names, wired before its first load, or nothing where no home is
    /// named. A variable that named another file is read nowhere (ONEWAY1c-i): every home's staffing is its store's.
    /// </summary>
    public static RoleStaffing? WireHome(Func<string, string?> environment) =>
        DriverConfig.FindHome(environment) is { } home ? Wire(home) : null;

    /// <summary>
    /// The staffing's writers' one door (§19.15, every writer of the census): <see cref="Set(string, IReadOnlyList{ActorChoice}?)"/> where
    /// <paramref name="file"/> is wired, else the file's own write as today.
    /// </summary>
    /// <exception cref="DriverException">The judge's refusal, a lock held past its wait, or a write not done; said.</exception>
    public static void SetAt(string file, IReadOnlyList<(string Actor, IReadOnlyList<ActorChoice>? Choices)> changes)
    {
        if (Of(file) is { } staffing) staffing.Set(changes);
        else StandingChoices.Write(file, config => Changed(config, changes));
    }

    /// <summary><see cref="SetAt(string, IReadOnlyList{ValueTuple{string, IReadOnlyList{ActorChoice}}})"/> for one actor.</summary>
    public static void SetAt(string file, string actor, IReadOnlyList<ActorChoice>? choices) => SetAt(file, [(actor, choices)]);

    /// <summary><see cref="Set(IReadOnlyList{ValueTuple{string, IReadOnlyList{ActorChoice}}})"/> for one actor.</summary>
    public void Set(string actor, IReadOnlyList<ActorChoice>? choices) => Set([(actor, choices)]);

    /// <summary>
    /// The one write of the staffing (§19.15, steps 1 to 5): the staffing lock, a writer waiting past <see cref="LockWait"/> refused with
    /// nothing written; each change by today's <c>ActorChoices.With</c>; the commit. Before the import the file is the staffing, and is
    /// written under the same lock.
    /// </summary>
    /// <exception cref="DriverException">The judge's refusal, a lock held past its wait, or a write not done; said.</exception>
    public void Set(IReadOnlyList<(string Actor, IReadOnlyList<ActorChoice>? Choices)> changes) => Set(changes, also: null);

    /// <summary>
    /// <see cref="Set(IReadOnlyList{ValueTuple{string, IReadOnlyList{ActorChoice}}})"/> with <paramref name="also"/> run in the commit's
    /// transaction (STAFFPOLICY1b): the role's record and policy are published with the staffing or not at all. Before the import, where the
    /// file is the staffing, it runs in a transaction of its own after the file's write. A policy bound to a changed role's choices is
    /// re-keyed in the same transaction, or deleted where the role is left with none (§2.1).
    /// </summary>
    internal void Set(IReadOnlyList<(string Actor, IReadOnlyList<ActorChoice>? Choices)> changes, Action<DesktopWrite>? also)
    {
        foreach (var (actor, choices) in changes) ActorChoices.Judged(actor, choices);
        using var held = Lock();
        if (Read() is not { } read)
        {
            StandingChoices.Write(FileOfHome, config => Changed(config, changes));
            // The file is the staffing before the import: the record and the policy follow it in a transaction of their own.
            if (also is not null) Publishing(also);
            return;
        }

        Commit(Changed(StaffingProjection.Config(read), changes), read.AsFound, also, [.. changes.Select(change => change.Actor)]);
    }

    /// <summary>
    /// A transaction that writes <paramref name="rows"/> (the added roles' staffing) and <paramref name="also"/> together, under the staffing's
    /// lock, with no seeded role's staffing among them (STAFFPOLICY1b): the catalogue, the policy and the staffing of added roles published as one.
    /// </summary>
    /// <exception cref="DriverException">The staffing is not in the store yet and <paramref name="rows"/> names a role; a lock held past its wait; a write not done.</exception>
    internal void Publish(IReadOnlyList<(string Role, IReadOnlyList<ActorChoice>? Choices)> rows, Action<DesktopWrite> also)
    {
        foreach (var (role, _) in rows)
        {
            if (ActorChoices.Ids.Contains(role, StringComparer.Ordinal)) throw new ArgumentException($"`{role}` is a seeded role, whose staffing is Set's.", nameof(rows));
        }

        using var held = Lock();
        if (rows.Count > 0 && Read() is null) throw NotInStore();
        Publishing(write =>
        {
            AddedRows(write, rows);
            also(write);
        });
    }

    private void Publishing(Action<DesktopWrite> work)
    {
        using var store = DesktopStore.Open(Location, StoreProcess.Options(Settled));
        store.Write(Family, write =>
        {
            work(write);
            BeforeCommit?.Invoke();
        });
    }

    internal static DriverException NotInStore() =>
        new("the roles' staffing is not in this home's store yet (its first start imports it), so an added role's choices are not kept; nothing was written.");

    /// <summary>This home's ledger of role starts, which reads the policies and the day's counts a preview arranges by.</summary>
    public StaffLedger Ledger => new(home, settled);

    /// <summary><paramref name="role"/>'s policy as the store holds it: the default where there is none, or no store.</summary>
    public StaffPolicyRead Policy(string role)
    {
        using var store = DesktopStoreReader.Open(Location);
        return store is null ? new StaffPolicyRead(StaffPolicyRead.Default, null, 0, null) : StaffPolicies.Read(store, role);
    }

    /// <summary>
    /// The staffing of an added role (FOUNDATION1y-i): its own row of <c>role_staffing</c> set, or deleted where <paramref name="choices"/> is
    /// null, under the staffing's lock in one transaction. The file holds no added role, so nothing of it is projected; the caller judged the
    /// choices by the role's work (<see cref="RoleCatalog.Judge(RoleRecord, IReadOnlyList{ActorChoice}?)"/>) and runs the check once the
    /// catalogue is written, since an added role taking Intake or Ask Daoris moves the projection's narrowing.
    /// </summary>
    /// <exception cref="DriverException">The staffing is not in the store yet, a lock held past its wait, or a write not done; said.</exception>
    public void SetAdded(string role, IReadOnlyList<ActorChoice>? choices) => SetAdded([(role, choices)]);

    /// <summary><see cref="SetAdded(string, IReadOnlyList{ActorChoice})"/> for several added roles, all in the one transaction (AGENTSPLACE1g2).</summary>
    public void SetAdded(IReadOnlyList<(string Role, IReadOnlyList<ActorChoice>? Choices)> changes)
    {
        foreach (var (role, _) in changes)
        {
            if (ActorChoices.Ids.Contains(role, StringComparer.Ordinal)) throw new ArgumentException($"`{role}` is a seeded role, whose staffing is Set's.", nameof(changes));
        }

        using var held = Lock();
        if (Read() is null) throw NotInStore();
        Publishing(write => AddedRows(write, changes));
    }

    /// <summary>The added roles' rows written inside a transaction; a policy bound to one is re-keyed to its new choices, or deleted where it has none.</summary>
    internal static void AddedRows(DesktopWrite write, IReadOnlyList<(string Role, IReadOnlyList<ActorChoice>? Choices)> changes)
    {
        var bound = StaffPolicies.Has(write) ? BoundPolicies(write, changes.Select(change => change.Role)) : [];
        foreach (var (role, choices) in changes)
        {
            write.Execute("DELETE FROM role_staffing WHERE role = $role", ("$role", role));
            if (choices is null) continue;
            write.Execute(
                "INSERT INTO role_staffing (role, state, choices, why, first_agent, at) VALUES ($role, 'set', $choices, NULL, NULL, $at)",
                ("$role", role), ("$choices", Write(choices)), ("$at", Now()));
        }

        Rekeyed(write, bound);
    }

    /// <summary>The policies of <paramref name="roles"/> that stand set against the staffing as it is, to be re-keyed once their staffing is rewritten.</summary>
    private static IReadOnlyList<(string Role, StaffPolicy Policy)> BoundPolicies(DesktopWrite write, IEnumerable<string> roles) =>
        [.. roles.Distinct(StringComparer.Ordinal).Select(role => (Role: role, Read: StaffPolicies.Read(write, role)))
            .Where(each => each.Read is { State: StaffPolicyRead.Set, Policy: not null })
            .Select(each => (each.Role, each.Read.Policy!))];

    /// <summary>The policies read before a write, bound to the choices it left: re-keyed to them, or deleted where the role has none now.</summary>
    private static void Rekeyed(DesktopWrite write, IReadOnlyList<(string Role, StaffPolicy Policy)> bound)
    {
        foreach (var (role, policy) in bound)
        {
            var row = write.Query(
                "SELECT state, choices FROM role_staffing WHERE role = $role", read => (State: read.GetString(0), Choices: Text(read, 1)), ("$role", role)).FirstOrDefault();
            var choices = row.State == "set" ? Choices(row.Choices) : null;
            if (choices is not { Count: > 0 })
            {
                if (StaffPolicies.Delete(write, role, Now())) write.Moved(StaffPolicies.Family);
                continue;
            }

            StaffPolicies.Rekey(write, role, policy, choices);
            write.Moved(StaffPolicies.Family);
        }
    }

    /// <summary>
    /// The check (§19.15): at a start, a look of the lease holder's loop and a write's end, whether the store holds the staffing; before the
    /// import a start or a look imports, once the start's confirmation is settled, a look waiting for no lock.
    /// </summary>
    public StaffingChecked Check(StaffingCheckAt at)
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

        return new StaffingChecked(StaffingChecks.Same);
    }

    private static DriverConfig Changed(
        DriverConfig config, IReadOnlyList<(string Actor, IReadOnlyList<ActorChoice>? Choices)> changes)
    {
        foreach (var (actor, choices) in changes) config = ActorChoices.With(config, actor, choices);
        return config;
    }

    /// <summary>The staffing's record lock, held to the write's end; a writer that waits past <see cref="LockWait"/> is refused.</summary>
    internal RecordLock Lock()
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

    /// <summary>Steps 3 to 5 under the staffing lock: one store transaction that writes the staffing, the re-keyed policies and <paramref name="also"/>.</summary>
    private void Commit(DriverConfig after, IReadOnlyDictionary<string, string> asFound, Action<DesktopWrite>? also, IReadOnlyList<string>? rekey = null)
    {
        try
        {
            using var store = DesktopStore.Open(Location, StoreProcess.Options(Settled));
            store.Write(Family, write =>
            {
                var bound = rekey is { Count: > 0 } && StaffPolicies.Has(write) ? BoundPolicies(write, rekey) : [];
                StaffingProjection.Rows(write, after, asFound, Now());
                Rekeyed(write, bound);
                also?.Invoke(write);
                BeforeCommit?.Invoke();
            });
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            throw new DriverException($"the roles' staffing was not written: the store would not take it ({error.Message}).");
        }
    }

    /// <summary>
    /// A JSON element as <c>driver.json</c>'s staffing paths are kept here, compact and with the relaxed escaping, so two spellings of one
    /// value (whitespace, an escaped letter) are one text. An unreadable entry is kept as this text.
    /// </summary>
    public static string Compact(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Compacted)) element.WriteTo(writer);
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// <summary>The file's staffing rows, and whether the file holds any of its four staffing keys.</summary>
    private sealed record Staffing(IReadOnlyList<StaffingRow> Rows, bool Holds);

    /// <summary>
    /// <c>driver.json</c>'s staffing keys: <c>actors</c>, and the three first-agent keys only nocompat1's <see cref="StaffingStep"/> meets,
    /// since it takes them out once it has imported them.
    /// </summary>
    private static readonly string[] Keys = ["actors", "adapter", "intakeAdapter", "helperAdapter"];

    /// <summary>A fresh home's staffing (ONEWAY1b): each of the five roles with an order of its own at its seed, nothing in the file.</summary>
    private static Staffing Seeds() => new(
        [.. ActorChoices.Ordered.Select(role => new StaffingRow(role, "set", Write(ActorChoices.Seed(role)!), null, null))],
        false);

    /// <summary>
    /// The file's staffing as today's build reads it (<see cref="JsonKeeping.Once"/>, <see cref="ActorChoices.Read"/> and the three keys as
    /// <see cref="DriverConfig.Parse"/> reads them), or why the file does not read.
    /// </summary>
    private static string? Parse(byte[] bytes, out Staffing staffing)
    {
        staffing = new Staffing([], false);
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

            var rows = new List<StaffingRow>();
            foreach (var id in ActorChoices.Ids)
            {
                var first = keys.GetValueOrDefault(id);
                if (errors.TryGetValue(id, out var why)) rows.Add(new StaffingRow(id, "unreadable", Compact(root.GetProperty("actors").GetProperty(id)), why, first));
                else if (actors.TryGetValue(id, out var choices)) rows.Add(new StaffingRow(id, "set", Write(choices), null, first));
                else if (first is not null) rows.Add(new StaffingRow(id, "before", null, null, first));
            }

            staffing = new Staffing(rows, Keys.Any(key => root.TryGetProperty(key, out _)));
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

    /// <summary>The choices a stored list holds, or null where the text is not one the store wrote; a policy's digest reads them too.</summary>
    internal static IReadOnlyList<ActorChoice>? Choices(string? text)
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
