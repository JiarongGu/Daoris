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

/// <summary>
/// The roles' staffing in the desktop store (FOUNDATION1 w-s part 1, D212; the design §19.15, §13.3, §13.5, §13.6): <c>role_staffing</c>
/// holding what <c>driver.json</c>'s staffing holds today, its projection row <c>written</c> and its found rows, imported once from the
/// file. Roles' own, read by nothing yet.
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
    public StaffingImport ImportFile()
    {
        var path = FileOfHome;
        if (!File.Exists(path)) return new StaffingImport(StaffingImported.NoFile);
        if (Settled() is { Is: false } waiting) return Held(waiting.Said ?? "the start's confirmation is not settled");

        try
        {
            using var _ = RecordLock.Take(RecordLock.PathOf(home, Record), LockWait);
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

    /// <summary>
    /// Set a role's staffing: the one write of §19.15 (the staffing lock, the change by <c>ActorChoices.With</c>, the file against its
    /// baseline, the commit, the check). Part 1 of FOUNDATION1w-s builds the family and nothing calls this; the write is part 3's.
    /// </summary>
    /// <exception cref="DriverException">The judge's refusal; nothing changed.</exception>
    /// <exception cref="NotSupportedException">Always, once judged: the write is not built yet.</exception>
    public void Set(string actor, IReadOnlyList<ActorChoice>? choices)
    {
        ActorChoices.Judged(actor, choices);
        throw new NotSupportedException("the staffing's write is built by FOUNDATION1w-s part 3.");
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
    private static string Write(IReadOnlyList<ActorChoice> choices)
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
