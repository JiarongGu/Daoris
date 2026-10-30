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
/// and by nothing yet (row x's start plan adopts <see cref="RoleFor"/>). Its capabilities are the work codes (<see cref="ParticipantWork"/>);
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
        ["build"] = ("开发", ParticipantWork.Build),
        ["intake"] = ("受理", ParticipantWork.Intake),
        ["opinion"] = ("第二意见", ParticipantWork.Review),
        ["helper"] = ("助手", ParticipantWork.Help),
        ["partner"] = ("搭档", ParticipantWork.Partner),
        ["brainstorm"] = ("头脑风暴", ParticipantWork.Brainstorm),
        ["ask-daoris"] = ("问道衍", ParticipantWork.AskDaoris),
        ["chat"] = ("聊天", ParticipantWork.Chat),
    };

    /// <summary>The ids no added role may take, which stay the shell's: the supervisor and the steward.</summary>
    public static IReadOnlyList<string> Reserved { get; } = ["supervisor", "steward"];

    /// <summary>The seeded roles, one per id of <see cref="ActorChoices.Ids"/>, in the screen's order.</summary>
    public static IReadOnlyList<RoleRecord> Seed { get; } =
        [.. ActorChoices.Ids.Select(id => new RoleRecord(id, ActorWords.Name(id), Seeded[id].Zh, RoleRecord.Always, [Seeded[id].Work], RoleScope.Everywhere))];

    private RoleCatalog(IReadOnlyList<RoleSlot> slots) => Slots = slots;

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
    /// takes there answers neither, and starts nothing.
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

        return work == ParticipantWork.Design ? RoleFor(ParticipantWork.Build, place) : new RoleFound(null, null);
    }

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
