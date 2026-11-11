using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A row of <c>roles</c>: the role id, its <c>state</c> (<c>set</c> or <c>removed</c>), its record's JSON and its revision.</summary>
public sealed record StoredRole(string Id, string State, string Record, long Revision);

/// <summary>How the catalogue reads one id: the seed's, a stored record, a removal, or a record that does not read.</summary>
public enum RoleState
{
    /// <summary>No record: the seed's role (an added id never reads so).</summary>
    Seed,

    /// <summary>A record that reads.</summary>
    Set,

    /// <summary>A record saying the seeded role is taken out.</summary>
    Removed,

    /// <summary>A record that does not read, kept as its error: it holds the starts that read it, and is never read as the seed.</summary>
    Unreadable,
}

/// <summary>One id as the catalogue reads it: its role where it has one, its revision (0 for the seed) and, where it does not read, why.</summary>
/// <param name="Scope">What an unreadable record still shows of its scope; none where it shows none, which holds every start.</param>
/// <param name="Appointed">An unreadable record whose <c>filled</c> is <c>appointed</c>, which holds no work's starts.</param>
public sealed record RoleSlot(string Id, RoleState State, RoleRecord? Role, string? Error, long Revision, RoleScope? Scope = null, bool Appointed = false);

/// <summary>The role a work has at a place, or why its start holds; neither where no role takes the work there (it is off).</summary>
public sealed record RoleFound(RoleRecord? Role, string? Held);

/// <summary>
/// The catalogue of roles: the store's <c>roles</c> and the seed per id. A
/// working role's capabilities are the work codes (<see cref="RoleWork"/>), an appointed role's its acts (<see cref="RoleHold"/>).
/// </summary>
/// <remarks>
/// <b>The seed</b> reproduces today's start per id: the eight always-on roles, each machine-wide and taking the work of its name, none taking
/// design, so design falls back to what takes build; then the supervisor, held by the person's rules, and the steward, held by no one, both
/// machine-wide. <b>An id reads</b> absent (the seed's), set, removed or unreadable; an unreadable seeded working id holds the starts of its
/// work, an unreadable added working record every start in its scope, each said with the record's error; an appointed record holds
/// no start. A working role's staffing is not in the record: it is read from <c>role_staffing</c> by <see cref="Order"/>.
/// </remarks>
public sealed class RoleCatalog
{
    /// <summary>The store's version this family's tables arrive at.</summary>
    internal const int Step = 10;

    public const string Family = "roles";

    /// <summary>The seeded roles in the screen's order, one per id of <see cref="ActorChoices.Ids"/> and the two appointed: Defaults/roles.json, carried in the assembly.</summary>
    public static IReadOnlyList<RoleRecord> Seed { get; } = ReadSeed();

    /// <summary>Whether <paramref name="id"/> is one the seed holds.</summary>
    public static bool IsSeeded(string id) => Seed.Any(role => role.Id == id);

    private static IReadOnlyList<RoleRecord> ReadSeed()
    {
        var assembly = typeof(RoleCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("Daoris.Driver.Defaults/roles.json")
            ?? throw new InvalidOperationException("the roles this build ships are not carried in it (Defaults/roles.json).");
        using var document = JsonDocument.Parse(stream);
        return [.. document.RootElement.GetProperty("roles").EnumerateArray().Select(each =>
        {
            var hold = each.TryGetProperty("authority", out _) ? RoleHold.Read(each).Hold ?? throw new InvalidOperationException("a shipped appointed role does not read.") : null;
            return new RoleRecord(
                each.GetProperty("id").GetString()!, AuthoredText.Read(each.GetProperty("name")) ?? throw new InvalidOperationException("a shipped role has no name."),
                each.GetProperty("filled").GetString()!, hold is null ? [each.GetProperty("work").GetString()!] : [],
                new RoleScope(each.GetProperty("place").GetString()!), hold);
        })];
    }

    private RoleCatalog(IReadOnlyList<RoleSlot> slots) => Slots = slots;

    /// <summary>The refusal of a press made against a revision its role has moved past.</summary>
    public static DriverException Moved(string id, long seen, long now) =>
        new($"the role `{id}` changed since it was read (revision {seen}, now {now}), so nothing was written; read it again.");

    /// <summary>The revision of <paramref name="id"/>'s record as the catalogue reads it; 0 for the seed and for an id it does not hold.</summary>
    public long RevisionOf(string id) => Slots.FirstOrDefault(slot => slot.Id == id)?.Revision ?? 0;

    /// <summary>
    /// The catalogue a start reads: the store in <paramref name="home"/>, the seed where it has none. A store that cannot be
    /// read holds the start, never reads as the seed, since a role it holds could be one that takes the work.
    /// </summary>
    /// <exception cref="DriverException">The store cannot be read.</exception>
    public static RoleCatalog ReadHome(string home)
    {
        try
        {
            return Read(DesktopStore.LocationIn(home));
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            throw new DriverException($"the role catalogue cannot be read, so starts wait: {error.Message}");
        }
    }

    /// <summary>Every seeded id in the screen's order, then each added id by name.</summary>
    public IReadOnlyList<RoleSlot> Slots { get; }

    /// <summary>The catalogue <paramref name="stored"/> makes beside the seed: each id with no row is the seed's.</summary>
    public static RoleCatalog Of(IEnumerable<StoredRole> stored)
    {
        var rows = stored.GroupBy(row => row.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        var slots = new List<RoleSlot>();
        foreach (var seed in Seed)
        {
            slots.Add(rows.TryGetValue(seed.Id, out var row) ? Slot(row) : new RoleSlot(seed.Id, RoleState.Seed, seed, null, 0));
        }

        slots.AddRange(rows.Values.Where(row => !IsSeeded(row.Id)).OrderBy(row => row.Id, StringComparer.Ordinal).Select(Slot));
        return new RoleCatalog(slots);
    }

    /// <summary>
    /// The catalogue the store at <paramref name="location"/> holds beside the seed: the seed whole where there is no store or one from
    /// before the family's step. A read never creates or writes the store; the store's failure is thrown.
    /// </summary>
    public static RoleCatalog Read(string location)
    {
        using var store = DesktopStoreReader.Open(location);
        if (store is null || store.Version < Step) return Of([]);
        return Of(store.Query("SELECT id, state, record, revision FROM roles", row => new StoredRole(row.GetString(0), row.GetString(1), row.GetString(2), row.GetInt64(3))));
    }

    /// <summary>
    /// The record of <paramref name="id"/> as it stood at <paramref name="revision"/>: the seed at 0 for a seeded id, else the revision
    /// the store kept when its state is <c>set</c>; null where none is kept or it does not read. Never writes the store.
    /// </summary>
    public static RoleRecord? RecordAt(string location, string id, long revision)
    {
        if (revision == 0) return Seed.FirstOrDefault(role => role.Id == id);
        using var store = DesktopStoreReader.Open(location);
        if (store is null || store.Version < Step) return null;
        var json = store.Query(
            "SELECT record FROM role_revisions WHERE id = $id AND revision = $revision AND state = 'set'",
            row => row.GetString(0), ("$id", id), ("$revision", revision)).FirstOrDefault();
        return json is null ? null : RoleRecord.Parse(id, json).Record;
    }

    private static RoleSlot Slot(StoredRole row)
    {
        switch (row.State)
        {
            case "removed":
                return new RoleSlot(row.Id, RoleState.Removed, null, null, row.Revision);
            case "set":
                var (role, why) = RoleRecord.Parse(row.Id, row.Record);
                return role is null
                    ? new RoleSlot(row.Id, RoleState.Unreadable, null, why, row.Revision, RoleRecord.ScopeOf(row.Record), FilledAppointed(row.Record))
                    : new RoleSlot(row.Id, RoleState.Set, role, null, row.Revision);
            default:
                return new RoleSlot(row.Id, RoleState.Unreadable, null, "has a state this build does not know", row.Revision);
        }
    }

    /// <summary>Whether <paramref name="json"/> says <c>filled: appointed</c>, read before anything else of it, as the judge reads it.</summary>
    private static bool FilledAppointed(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("filled", out var filled)
                && filled.ValueKind == JsonValueKind.String && filled.GetString() == RoleRecord.Appointed;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// The role that takes <paramref name="work"/> at <paramref name="place"/>: the most specific scope's, design falling back to what takes
    /// build; its <c>Held</c> where a record that does not read holds the start, or two roles take the work in one scope. A work no role
    /// takes there, its role removed or scoped elsewhere, holds its starts until the person gives it one, said.
    /// </summary>
    public RoleFound RoleFor(string work, RolePlace place)
    {
        var seeded = StartPlan.RoleOf(work);
        foreach (var slot in Slots.Where(slot => slot.State == RoleState.Unreadable && !slot.Appointed && !AppointedRoles.Kinds.Contains(slot.Id, StringComparer.Ordinal)))
        {
            var seededId = ActorChoices.Ids.Contains(slot.Id, StringComparer.Ordinal);
            if (seededId ? slot.Id == seeded : slot.Scope is null || slot.Scope.Covers(place))
            {
                return Unreadable(slot, seededId ? $"the starts of {work}" : "every start in its scope");
            }
        }

        var taking = Slots.Where(slot => slot.Role is { } role && role.Takes.Contains(work, StringComparer.Ordinal) && role.Scope.Covers(place))
            .Select(slot => slot.Role!).ToList();
        if (taking.Count > 0)
        {
            var rank = taking.Max(role => role.Scope.Rank);
            var best = taking.Where(role => role.Scope.Rank == rank).ToList();
            return best.Count == 1
                ? new RoleFound(best[0], null)
                : new RoleFound(null, $"Two roles take `{work}` in one scope ({string.Join(", ", best.Select(role => $"`{role.Id}`"))}), so its starts hold until one is removed.");
        }

        return work is RoleWork.Design or RoleWork.Exact ? RoleFor(RoleWork.Build, place) : new RoleFound(null, NoRole(work));
    }

    /// <summary>
    /// The role <paramref name="role"/> names, for a start of <paramref name="work"/> at <paramref name="place"/>: found by its id, never by
    /// the work. A role that takes build takes a design step, as <see cref="RoleFor"/>'s fallback does. Its <c>Held</c>, said,
    /// where the id is not in the catalogue, appointed or unreadable, or the role does not work here or take the work; no other role is
    /// taken for it.
    /// </summary>
    public RoleFound Named(string role, string work, RolePlace place)
    {
        const string Hold = "so the starts naming it hold";
        var slot = Slots.FirstOrDefault(slot => slot.Id == role);
        if (slot is null or { State: RoleState.Removed }) return new RoleFound(null, $"No role `{role}` is in the catalogue, {Hold} until it is added: {Configured}.");
        if (work == ParticipantWork.Supervise)
        {
            return slot.Role is { } supervisor && AppointedStaffing.Ordered(supervisor.Hold) && supervisor.Scope.Covers(place)
                ? new RoleFound(supervisor, null)
                : new RoleFound(null, $"`{role}` is no supervisor an agent holds here, {Hold}.");
        }

        if (slot.Appointed || slot.Role?.Hold is not null) return new RoleFound(null, $"`{role}` is an appointed role, which holds acts and takes no work, {Hold} until they name a working role.");
        if (slot.Role is not { } found) return Unreadable(slot, "the starts naming it");
        if (!found.Scope.Covers(place))
        {
            return new RoleFound(null, $"The role `{role}` does not work here: its scope is the {found.Scope.Kind} `{found.Scope.Id}`, {Hold} until it is scoped here: {Configured}.");
        }

        var takes = found.Takes.Contains(work, StringComparer.Ordinal) || (work is RoleWork.Design or RoleWork.Exact && found.Takes.Contains(RoleWork.Build, StringComparer.Ordinal));
        return takes ? new RoleFound(found, null) : new RoleFound(null, $"The role `{role}` does not take `{work}`, {Hold} until it does: {Configured}.");
    }

    private static RoleFound Unreadable(RoleSlot slot, string reach) =>
        new(null, $"The role `{slot.Id}` {slot.Error}, so {reach} hold until it reads or is deleted.");

    /// <summary>Every appointed role that reads, the supervisors and the steward, in the catalogue's order.</summary>
    public IReadOnlyList<RoleRecord> HoldingActs => [.. Slots.Select(slot => slot.Role).OfType<RoleRecord>().Where(role => role.Hold is not null)];

    /// <summary>The supervisor that answers a decision of <paramref name="kind"/> at <paramref name="place"/>: <see cref="SupervisorResolution.Find"/>.</summary>
    public SupervisorFound Supervising(string kind, RolePlace place, RuleWiderPlaces? wider = null) => SupervisorResolution.Find(Slots, kind, place, wider);

    /// <summary>Where the person gives a work its role, named in every hold that waits for one.</summary>
    private const string Configured = "Agents → Roles, or `daoris role`";

    /// <summary>
    /// What a start of <paramref name="work"/> says where no role takes it: its role is removed or scoped elsewhere, so it
    /// holds until the person gives the work a role. A work that is off has its role, with no choices, and is not this.
    /// </summary>
    public static string NoRole(string work) =>
        $"No role takes `{work}` here, so its starts hold until one is given it: {Configured}.";

    /// <summary>The judge's refusal codes, the closed set every door answers; <c>policy</c> is a staffing policy's (<see cref="StaffPolicy.Judge"/>).</summary>
    public static IReadOnlyList<string> Codes { get; } =
        ["appointed-not-configurable", "mixed-shape", "unknown-work", "bad-record", "may-not-be-off", "two-roles-one-work", "choices", StaffPolicy.Code, SupervisorConversion.NotConverted];

    /// <summary>
    /// Whether <paramref name="choices"/> may be the staffing of a role taking <paramref name="work"/>: today's <see cref="ActorChoices.Judge"/>
    /// keyed by the work (design judged as Build), its sentence unchanged; null where it may.
    /// </summary>
    public static RoleRefusal? Judge(string work, IReadOnlyList<ActorChoice>? choices)
    {
        if (!RoleWork.Codes.Contains(work, StringComparer.Ordinal)) return Refused("unknown-work", $"`{work}` is not a work — one of {string.Join(", ", RoleWork.Codes)}.");
        return ActorChoices.Judge(StartPlan.RoleOf(work), choices) is { } said ? Refused(choices is { Count: 0 } ? "may-not-be-off" : "choices", said) : null;
    }

    /// <summary><see cref="Judge(string, IReadOnlyList{ActorChoice}?)"/> for each work <paramref name="role"/> takes, the first refusal.</summary>
    public static RoleRefusal? Judge(RoleRecord role, IReadOnlyList<ActorChoice>? choices) =>
        role.Takes.Select(work => Judge(work, choices)).FirstOrDefault(refusal => refusal is not null);

    /// <summary>
    /// Whether <paramref name="change"/> may be made to this catalogue, for every door alike: a mixed shape, an appointed role's acts,
    /// holder, scope and window (<see cref="RoleHold.Judge"/>), an unknown work, a record that is not one, a seeded id given the other
    /// shape or kind, two roles taking one work (or two of one appointed kind) in one scope, and the removal or scoping away from the
    /// machine of the role of a work that may not be off, or of the supervisor. Null where it may.
    /// </summary>
    public RoleRefusal? Judge(RoleChange change)
    {
        if (change is RoleChange.SetRole { Role: var role })
        {
            var appointed = role.Filled == RoleRecord.Appointed;
            if ((appointed && role.Takes.Count > 0) || (!appointed && role.Hold is not null))
            {
                return Refused("mixed-shape", $"`{role.Id}` mixes the working and the appointed shape: a role takes work or holds acts, never both.");
            }

            if (role.Filled is not (RoleRecord.Always or RoleRecord.Appointed)) return Refused("bad-record", $"`{role.Id}` is not `always` or `appointed`.");
            if (!IdShape.IsMatch(role.Id)) return Refused("bad-record", $"`{role.Id}` is not an id: lowercase letters, digits and hyphens, from a letter, at most 40.");
            if (string.IsNullOrWhiteSpace(role.Name.AsWritten)) return Refused("bad-record", $"`{role.Id}` needs a name.");
            if (!RoleScope.Kinds.Contains(role.Scope.Kind, StringComparer.Ordinal) || (role.Scope.Kind == RoleScope.Machine) != string.IsNullOrWhiteSpace(role.Scope.Id))
            {
                return Refused("bad-record", $"`{role.Id}`'s scope is the machine alone, or a workspace or a repository with its id.");
            }

            if (Seed.FirstOrDefault(seed => seed.Id == role.Id) is { } seeded && (seeded.Filled != role.Filled || seeded.Hold?.Kind != role.Hold?.Kind))
            {
                return Refused("mixed-shape", $"`{role.Id}` is seeded as {(seeded.Hold?.Kind is { } kind ? $"the {kind}" : "a working role")}, a shape it keeps.");
            }

            if (appointed)
            {
                if (RoleHold.Judge(role) is { } refusedHold) return refusedHold;
            }
            else
            {
                if (role.Takes.Count == 0) return Refused("bad-record", $"`{role.Id}` takes no work.");
                if (role.Takes.FirstOrDefault(work => !RoleWork.Codes.Contains(work, StringComparer.Ordinal)) is { } unknown)
                {
                    return Refused("unknown-work", $"`{unknown}` is not a work — one of {string.Join(", ", RoleWork.Codes)}.");
                }
            }
        }
        else if (change is RoleChange.DefaultAllRoles)
        {
            // Names no one id: the seed answers for every seeded one.
        }
        else if (Slots.All(slot => slot.Id != change.Id)) return Refused("bad-record", $"no role `{change.Id}` is in the catalogue.");

        var after = After(change);
        if (change is RoleChange.SetRole { Role: var set })
        {
            var clash = after.Where(slot => slot.Role is { } other && other.Id != set.Id && other.Scope == set.Scope && other.Takes.Intersect(set.Takes).Any()).Select(slot => slot.Role!).FirstOrDefault();
            if (clash is not null)
            {
                return Refused("two-roles-one-work", $"`{clash.Id}` already takes `{clash.Takes.Intersect(set.Takes).First()}` in that scope: at most one role takes a work in one scope.");
            }

            if (set.Hold is { } hold)
            {
                foreach (var other in after.Select(slot => slot.Role).OfType<RoleRecord>().Where(other => other.Id != set.Id && other.Scope == set.Scope && other.Hold is not null))
                {
                    if (SupervisorResolution.Shared(hold, other.Hold!) is { } shared) return Refused("two-roles-one-work", $"`{other.Id}` {shared}.");
                }
            }
        }

        // The supervisor's rules always act, so a generic supervisor stays on the machine to hold them and answer what no kind takes.
        if (!after.Any(slot => slot.Role is { Hold.Generic: true, Scope.Rank: 0 }) && !after.Any(slot => slot is { State: RoleState.Unreadable, Id: AppointedRoles.Supervisor }))
        {
            return Refused("may-not-be-off", $"the supervisor for every decision may not be off: your rules hold it on the machine, so `{change.Id}` cannot be removed, scoped away from it or given kinds.");
        }

        // An unreadable seeded id is left to the person's next edit of it, so it is not counted as uncovered here.
        foreach (var work in RoleWork.Codes.Where(work => work is not (RoleWork.Design or RoleWork.Exact) && !ActorChoices.MayBeOff(StartPlan.RoleOf(work))))
        {
            if (after.Any(slot => slot.State == RoleState.Unreadable && slot.Id == StartPlan.RoleOf(work))) continue;
            if (!after.Any(slot => slot.Role is { } role && role.Scope.Rank == 0 && role.Takes.Contains(work, StringComparer.Ordinal)))
            {
                return Refused("may-not-be-off", $"`{work}` may not be off: a role must take it on the machine, so `{change.Id}` cannot be removed or scoped away from it.");
            }
        }

        return null;
    }

    /// <summary>The catalogue once <paramref name="change"/> is made, for judging the next change of a batch against it; revisions are not carried.</summary>
    public RoleCatalog With(RoleChange change) => new(After(change));

    private IReadOnlyList<RoleSlot> After(RoleChange change)
    {
        if (change is RoleChange.DefaultAllRoles)
        {
            return [.. Seed.Select(role => new RoleSlot(role.Id, RoleState.Seed, role, null, 0)), .. Slots.Where(slot => !IsSeeded(slot.Id))];
        }

        var slots = Slots.Where(slot => slot.Id != change.Id).ToList();
        var seeded = Seed.FirstOrDefault(role => role.Id == change.Id);
        switch (change)
        {
            case RoleChange.SetRole set: slots.Add(new RoleSlot(set.Id, RoleState.Set, set.Role, null, 0)); break;
            case RoleChange.RemoveRole when seeded is not null: slots.Add(new RoleSlot(change.Id, RoleState.Removed, null, null, 0)); break;
            case RoleChange.DefaultRole when seeded is not null: slots.Add(new RoleSlot(change.Id, RoleState.Seed, seeded, null, 0)); break;
        }

        return slots;
    }

    /// <summary>
    /// The catalogue's one write: <paramref name="change"/> judged against the store's roles inside its transaction, then one row of
    /// <c>roles</c> written or deleted and one row of <c>role_revisions</c> kept, the id's next revision, which a removal never resets.
    /// Nothing is written for a refusal. Read by nothing yet: no door is wired to it.
    /// </summary>
    /// <returns>The id's new revision.</returns>
    /// <exception cref="DriverException">The judge's refusal; nothing changed.</exception>
    public static long Write(string location, RoleChange change, Func<StoreSettled>? settled = null) => WriteAll(location, [change], settled)[0];

    /// <summary>
    /// <see cref="Write"/> for a batch: each change judged in order against the roles the changes before it leave, all in one
    /// transaction, so the first refusal rolls every one back and nothing is written.
    /// </summary>
    /// <returns>Each id's new revision, in the batch's order.</returns>
    /// <exception cref="DriverException">The first change the judge refused; nothing changed.</exception>
    public static IReadOnlyList<long> WriteAll(string location, IReadOnlyList<RoleChange> changes, Func<StoreSettled>? settled = null)
    {
        using var store = DesktopStore.Open(location, StoreProcess.Options(settled));
        return store.Write(Family, write => WriteIn(write, changes));
    }

    /// <summary><see cref="WriteAll"/> inside a transaction the caller holds, so a role, its staffing and its policy are published in one; <paramref name="reads"/> holds each change's read revision, compared with the transaction's own.</summary>
    /// <exception cref="DriverException">The first change the judge refused or whose role moved since read; the caller's transaction rolls back.</exception>
    public static IReadOnlyList<long> WriteIn(DesktopWrite write, IReadOnlyList<RoleChange> changes, IReadOnlyList<long?>? reads = null)
    {
        {
            var revisions = new List<long>();
            RoleCatalog Current() => Of(write.Query("SELECT id, state, record, revision FROM roles", row => new StoredRole(row.GetString(0), row.GetString(1), row.GetString(2), row.GetInt64(3))));
            for (var index = 0; index < changes.Count; index++)
            {
                var asked = changes[index];
                var current = Current();
                if (reads?[index] is { } seen && current.RevisionOf(asked.Id) != seen) throw Moved(asked.Id, seen, current.RevisionOf(asked.Id));
                if (current.Judge(asked) is { } refusal) throw new RoleRefusedException(refusal);

                // The whole structure's default is each seeded role not already the seed's, one revision apiece; 0 where none moved.
                var each = asked is RoleChange.DefaultAllRoles
                    ? current.Slots.Where(slot => slot.State != RoleState.Seed && IsSeeded(slot.Id)).Select(slot => RoleChange.Default(slot.Id)).ToList()
                    : [asked];
                var last = 0L;
                foreach (var change in each)
                {
                    var revision = write.Scalar<long>("SELECT MAX(revision) FROM role_revisions WHERE id = $id", ("$id", change.Id)) + 1;
                    var at = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                    var seeded = IsSeeded(change.Id);
                    (string State, string? Record) kept = change switch
                    {
                        RoleChange.SetRole set => ("set", set.Role.Json()),
                        RoleChange.RemoveRole when seeded => ("removed", "{}"),
                        RoleChange.RemoveRole => ("deleted", null),
                        _ => (seeded ? "default" : "deleted", null),
                    };
                    write.Execute(
                        "INSERT INTO role_revisions (id, revision, state, record, at) VALUES ($id, $revision, $state, $record, $at)",
                        ("$id", change.Id), ("$revision", revision), ("$state", kept.State), ("$record", kept.Record), ("$at", at));
                    if (kept.State is "set" or "removed")
                    {
                        write.Execute(
                            """
                            INSERT INTO roles (id, state, record, revision, at) VALUES ($id, $state, $record, $revision, $at)
                            ON CONFLICT (id) DO UPDATE SET state = $state, record = $record, revision = $revision, at = $at
                            """,
                            ("$id", change.Id), ("$state", kept.State), ("$record", kept.Record), ("$revision", revision), ("$at", at));
                    }
                    else write.Execute("DELETE FROM roles WHERE id = $id", ("$id", change.Id));

                    last = revision;
                }

                revisions.Add(last);
            }

            return revisions;
        }
    }

    private static RoleRefusal Refused(string code, string said) => new(code, said);

    private static readonly System.Text.RegularExpressions.Regex IdShape = new("^[a-z][a-z0-9-]{0,39}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// The order a start of <paramref name="work"/> at <paramref name="place"/> walks: its role's <see cref="ActorChoices.Effective"/> over
    /// <paramref name="config"/>, the staffing <c>role_staffing</c> fills; empty where no role takes the work there.
    /// </summary>
    /// <exception cref="DriverException">A record that does not read holds the start, or two roles take the work in one scope.</exception>
    /// <exception cref="ActorUnreadableException">The role's stored staffing does not read.</exception>
    public IReadOnlyList<ActorEffective> Order(
        string work, RolePlace place, DriverConfig config, IReadOnlyList<string>? fence = null, string? pick = null, string? handed = null)
    {
        var found = RoleFor(work, place);
        if (found.Held is { } held) throw new DriverException(held);
        return found.Role is { } role ? ActorChoices.Effective(role.Id, config, fence, pick, handed) : [];
    }

    /// <summary>The order a start of <paramref name="work"/> walks when its step names <paramref name="role"/> (<see cref="Named"/>).</summary>
    /// <exception cref="DriverException">The named role holds the start (<see cref="Named"/>'s sentences).</exception>
    /// <exception cref="ActorUnreadableException">The role's stored staffing does not read.</exception>
    public IReadOnlyList<ActorEffective> Order(
        string role, string work, RolePlace place, DriverConfig config, IReadOnlyList<string>? fence = null, string? pick = null, string? handed = null)
    {
        var found = Named(role, work, place);
        return found.Role is { } named ? ActorChoices.Effective(named.Id, config, fence, pick, handed) : throw new DriverException(found.Held!);
    }
}
