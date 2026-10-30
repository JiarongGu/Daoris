namespace Daoris.Driver;

/// <summary>A row of <c>roles</c>: the role id, its <c>state</c> (<c>set</c> or <c>removed</c>), its record's JSON and its revision.</summary>
public sealed record StoredRole(string Id, string State, string Record, long Revision);

/// <summary>How the catalogue reads one id (§2.7, X2-2.3): the seed's, a stored record, a removal, or a record that does not read.</summary>
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
public sealed record RoleSlot(string Id, RoleState State, RoleRecord? Role, string? Error, long Revision, RoleScope? Scope = null);

/// <summary>The role a work has at a place, or why its start holds; neither where no role takes the work there (it is off).</summary>
public sealed record RoleFound(RoleRecord? Role, string? Held);

/// <summary>
/// The catalogue of working roles (FOUNDATION1 w, the foundation design §2.7): the store's <c>roles</c> and the seed per id, read beside
/// and by nothing yet (row x's start plan adopts <see cref="RoleFor"/>). Its capabilities are the work codes (<see cref="RoleWork"/>);
/// no act vocabulary and no appointed seed: the supervisor and the steward stay the shell's.
/// </summary>
/// <remarks>
/// <b>The seed</b> reproduces today's start per id: the eight always-on roles, each machine-wide and taking the work of its name, none taking
/// design, so design falls back to what takes build. <b>An id reads</b> absent (the seed's), set, removed or unreadable; an unreadable seeded
/// id holds the starts of its work, an unreadable added record every start in its scope, each said with the record's error (O3-16). Its
/// staffing is not in the record: it is read from <c>role_staffing</c> by <see cref="Order"/> through the loaded configuration.
/// </remarks>
public sealed class RoleCatalog
{
    /// <summary>The store's version this family's tables arrive at.</summary>
    private const int Step = 10;

    /// <summary>The roles' English and Chinese names, as the AI screen says them (<c>settings.ai.actors.name.*</c>).</summary>
    private static readonly Dictionary<string, (string Zh, string Work)> Seeded = new(StringComparer.Ordinal)
    {
        ["build"] = ("开发", RoleWork.Build),
        ["intake"] = ("受理", RoleWork.Intake),
        ["opinion"] = ("第二意见", RoleWork.Review),
        ["helper"] = ("助手", RoleWork.Help),
        ["partner"] = ("搭档", RoleWork.Partner),
        ["brainstorm"] = ("头脑风暴", RoleWork.Brainstorm),
        ["ask-daoris"] = ("问道衍", RoleWork.AskDaoris),
        ["chat"] = ("聊天", RoleWork.Chat),
    };

    /// <summary>The ids no added role may take, which stay the shell's: the supervisor and the steward.</summary>
    public static IReadOnlyList<string> Reserved { get; } = ["supervisor", "steward"];

    /// <summary>The seeded roles, one per id of <see cref="ActorChoices.Ids"/>, in the screen's order.</summary>
    public static IReadOnlyList<RoleRecord> Seed { get; } =
        [.. ActorChoices.Ids.Select(id => new RoleRecord(id, ActorWords.Name(id), Seeded[id].Zh, RoleRecord.Always, [Seeded[id].Work], RoleScope.Everywhere))];

    private RoleCatalog(IReadOnlyList<RoleSlot> slots) => Slots = slots;

    /// <summary>The revision of <paramref name="id"/>'s record as the catalogue reads it; 0 for the seed and for an id it does not hold.</summary>
    public long RevisionOf(string id) => Slots.FirstOrDefault(slot => slot.Id == id)?.Revision ?? 0;

    /// <summary>
    /// The catalogue a start reads (FOUNDATION1x): the store in <paramref name="home"/>, the seed where it has none. A store that cannot be
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

        slots.AddRange(rows.Values.Where(row => !ActorChoices.Ids.Contains(row.Id, StringComparer.Ordinal)).OrderBy(row => row.Id, StringComparer.Ordinal).Select(Slot));
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
    /// The record of <paramref name="id"/> as it stood at <paramref name="revision"/> (census C): the seed at 0 for a seeded id, else the revision
    /// the store kept when its state was <c>set</c>; null where none is kept or it does not read. Never writes the store.
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
                    ? new RoleSlot(row.Id, RoleState.Unreadable, null, why, row.Revision, RoleRecord.ScopeOf(row.Record))
                    : new RoleSlot(row.Id, RoleState.Set, role, null, row.Revision);
            default:
                return new RoleSlot(row.Id, RoleState.Unreadable, null, "has a state this build does not know", row.Revision);
        }
    }

    /// <summary>
    /// The role that takes <paramref name="work"/> at <paramref name="place"/>: the most specific scope's, design falling back to what takes
    /// build; its <c>Held</c> where a record that does not read holds the start, or two roles take the work in one scope. A work no role
    /// takes there, its role removed or scoped elsewhere, holds its starts until the person gives it one (FOUNDATION1y-i, §2.7), said.
    /// </summary>
    public RoleFound RoleFor(string work, RolePlace place)
    {
        var seeded = StartPlan.RoleOf(work);
        foreach (var slot in Slots.Where(slot => slot.State == RoleState.Unreadable))
        {
            var seededId = ActorChoices.Ids.Contains(slot.Id, StringComparer.Ordinal);
            if (seededId ? slot.Id == seeded : slot.Scope is null || slot.Scope.Covers(place))
            {
                var reach = seededId ? $"the starts of {work}" : "every start in its scope";
                return new RoleFound(null, $"The role `{slot.Id}` {slot.Error}, so {reach} hold until it reads or is deleted.");
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

        return work == RoleWork.Design ? RoleFor(RoleWork.Build, place) : new RoleFound(null, NoRole(work));
    }

    /// <summary>
    /// What a start of <paramref name="work"/> says where no role takes it (FOUNDATION1y-i): its role was removed or scoped elsewhere, so it
    /// holds until the person gives the work a role. A work that is off has its role, with no choices, and is not this.
    /// </summary>
    public static string NoRole(string work) =>
        $"No role takes `{work}` here, so its starts hold until one is given it: Settings → AI features → Roles, or `daoris driver role`.";

    /// <summary>
    /// The roles whose entry in <c>driver.json</c> the projection narrows for an older build (§19.15, <i>C6-1.7</i>): Intake and Ask Daoris
    /// wherever their work is off, removed or unreadable in any scope, or an added record's work does not read, which counts as taking both.
    /// <paramref name="staffing"/> is the store's staffing; its own keys are never narrowed, so the newer build's machine role still starts.
    /// </summary>
    public IReadOnlySet<string> Narrowed(DriverConfig staffing)
    {
        var narrowed = new SortedSet<string>(StringComparer.Ordinal);
        var unreadableAdded = Slots.Any(slot => slot.State == RoleState.Unreadable && !ActorChoices.Ids.Contains(slot.Id, StringComparer.Ordinal));
        foreach (var work in (string[])[RoleWork.Intake, RoleWork.AskDaoris])
        {
            var seeded = StartPlan.RoleOf(work);
            var holder = RoleFor(work, new RolePlace()).Role;
            var off = Slots.Any(slot => slot.Role is { } role && role.Id != seeded && role.Takes.Contains(work, StringComparer.Ordinal)
                && !(staffing.Actors.TryGetValue(role.Id, out var choices) && choices.Count > 0));
            if (unreadableAdded || off || holder?.Id != seeded || Slots.Any(slot => slot.Id == seeded && slot.State == RoleState.Unreadable)) narrowed.Add(seeded);
        }

        return narrowed;
    }

    /// <summary>The judge's refusal codes, the closed set every door answers.</summary>
    public static IReadOnlyList<string> Codes { get; } =
        ["appointed-not-configurable", "mixed-shape", "unknown-work", "reserved-id", "bad-record", "may-not-be-off", "two-roles-one-work", "choices"];

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
    /// Whether <paramref name="change"/> may be made to this catalogue, for every door alike: an appointed record is refused before anything
    /// else of it, then a mixed shape, an unknown work, an id the shell keeps, a record that is not one, two roles taking one work in one
    /// scope, and the removal or scoping away of the role of a work that may not be off from the machine. Null where it may.
    /// </summary>
    public RoleRefusal? Judge(RoleChange change)
    {
        if (change is RoleChange.SetRole { Role: var role })
        {
            if (role.Filled == RoleRecord.Appointed) return Refused("appointed-not-configurable", $"`{role.Id}` is an appointed role: this build configures working roles alone.");
            if (role.Authority is not null) return Refused("mixed-shape", $"`{role.Id}` mixes the working and the appointed shape: a role takes work or holds acts, never both.");
            if (role.Filled != RoleRecord.Always) return Refused("bad-record", $"`{role.Id}` is not `always` or `appointed`.");
            if (Reserved.Contains(role.Id, StringComparer.Ordinal)) return Refused("reserved-id", $"`{role.Id}` is the shell's: the supervisor and the steward are not configured here.");
            if (!IdShape.IsMatch(role.Id)) return Refused("bad-record", $"`{role.Id}` is not an id: lowercase letters, digits and hyphens, from a letter, at most 40.");
            if (string.IsNullOrWhiteSpace(role.NameEn) || string.IsNullOrWhiteSpace(role.NameZh)) return Refused("bad-record", $"`{role.Id}` needs a name in English and in Chinese.");
            if (!RoleScope.Kinds.Contains(role.Scope.Kind, StringComparer.Ordinal) || (role.Scope.Kind == RoleScope.Machine) != string.IsNullOrWhiteSpace(role.Scope.Id))
            {
                return Refused("bad-record", $"`{role.Id}`'s scope is the machine alone, or a workspace or a repository with its id.");
            }

            if (role.Takes.Count == 0) return Refused("bad-record", $"`{role.Id}` takes no work.");
            if (role.Takes.FirstOrDefault(work => !RoleWork.Codes.Contains(work, StringComparer.Ordinal)) is { } unknown)
            {
                return Refused("unknown-work", $"`{unknown}` is not a work — one of {string.Join(", ", RoleWork.Codes)}.");
            }
        }
        else if (Reserved.Contains(change.Id, StringComparer.Ordinal)) return Refused("reserved-id", $"`{change.Id}` is the shell's: the supervisor and the steward are not configured here.");
        else if (Slots.All(slot => slot.Id != change.Id)) return Refused("bad-record", $"no role `{change.Id}` is in the catalogue.");

        var after = After(change);
        if (change is RoleChange.SetRole { Role: var set })
        {
            var clash = after.Where(slot => slot.Role is { } other && other.Id != set.Id && other.Scope == set.Scope && other.Takes.Intersect(set.Takes).Any()).Select(slot => slot.Role!).FirstOrDefault();
            if (clash is not null)
            {
                return Refused("two-roles-one-work", $"`{clash.Id}` already takes `{clash.Takes.Intersect(set.Takes).First()}` in that scope: at most one role takes a work in one scope.");
            }
        }

        // An unreadable seeded id is left to the person's next edit of it, so it is not counted as uncovered here.
        foreach (var work in RoleWork.Codes.Where(work => work != RoleWork.Design && !ActorChoices.MayBeOff(StartPlan.RoleOf(work))))
        {
            if (after.Any(slot => slot.State == RoleState.Unreadable && slot.Id == StartPlan.RoleOf(work))) continue;
            if (!after.Any(slot => slot.Role is { } role && role.Scope.Rank == 0 && role.Takes.Contains(work, StringComparer.Ordinal)))
            {
                return Refused("may-not-be-off", $"`{work}` may not be off: a role must take it on the machine, so `{change.Id}` cannot be removed or scoped away from it.");
            }
        }

        return null;
    }

    private IReadOnlyList<RoleSlot> After(RoleChange change)
    {
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
    public static long Write(string location, RoleChange change, Func<StoreSettled>? settled = null)
    {
        using var store = DesktopStore.Open(location, StoreProcess.Options(settled));
        return store.Write("roles", write =>
        {
            var current = Of(write.Query("SELECT id, state, record, revision FROM roles", row => new StoredRole(row.GetString(0), row.GetString(1), row.GetString(2), row.GetInt64(3))));
            if (current.Judge(change) is { } refusal) throw new DriverException(refusal.Said);

            var revision = write.Scalar<long>("SELECT MAX(revision) FROM role_revisions WHERE id = $id", ("$id", change.Id)) + 1;
            var at = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            var seeded = ActorChoices.Ids.Contains(change.Id, StringComparer.Ordinal);
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

            return revision;
        });
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
}
