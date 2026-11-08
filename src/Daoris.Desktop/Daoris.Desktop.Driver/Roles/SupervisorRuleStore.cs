using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>What a write of a place's rules came to: the revision it wrote, or why it is refused, or neither where the store would not take it.</summary>
public sealed record RuleWritten(long? Revision, RuleRefusal? Refusal)
{
    public bool Stored => Revision is not null;
}

/// <summary>
/// A place's rules kept in the desktop store: <c>supervisor_rules</c>, one row per rule,
/// and <c>supervisor_rule_revisions</c>, each place's set as written. A set is written whole, judged by <see cref="SupervisorRules.Judge"/>
/// in the same transaction, so a refused write leaves nothing. A store before the step, or none, holds no rule and takes none; a write the
/// store refuses does not raise, it is said so (<see cref="RuleWritten.Stored"/> false). The executors read them through <see cref="SupervisorRuleBook"/>.
/// </summary>
public sealed class SupervisorRuleStore(string home, Func<StoreSettled>? settled = null, TimeProvider? clock = null)
{
    /// <summary>The family a write moves.</summary>
    public const string Family = "supervisor_rules";

    /// <summary>The store's version the tables arrive at.</summary>
    public const int Step = 19;

    private static readonly Lock _gate = new();

    private string Location => DesktopStore.LocationIn(home);

    /// <summary>Every place's rules as stored; none before the tables.</summary>
    public IReadOnlyList<SupervisorRuleSet> All()
    {
        using var store = DesktopStoreReader.Open(Location);
        return store is null || store.Version < Step ? [] : Read(store);
    }

    /// <summary>One place's rules and their revision; revision 0 where it holds none.</summary>
    public SupervisorRuleSet Of(string place) => All().FirstOrDefault(set => set.Place == place) ?? new SupervisorRuleSet(place, 0, []);

    /// <summary>
    /// Write <paramref name="place"/>'s rules whole at <paramref name="revision"/> (the one read), through <paramref name="door"/>: judged
    /// against every place's set under the write lock, then kept with the place's next revision.
    /// </summary>
    public RuleWritten Write(string place, long revision, IReadOnlyList<SupervisorRule> rules, string door, SupervisorRuleWorld world)
    {
        lock (_gate)
        {
            try
            {
                using var store = DesktopStore.Open(Location, StoreProcess.Options(settled ?? StartConfirmation.Read));
                if (store.Stamp.Version < Step) return new RuleWritten(null, null);
                return store.Write(Family, write => new RuleWritten(WriteIn(write, place, revision, rules, door, world, clock), null));
            }
            catch (RuleRefusedException refused)
            {
                return new RuleWritten(null, refused.Refusal);
            }
            catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
            {
                return new RuleWritten(null, null);
            }
        }
    }

    /// <summary>
    /// <see cref="Write"/> inside a transaction the caller holds, so the role door publishes a role, its policy and a place's
    /// rules in one. Judged against every place's set as the transaction reads it. The place's new revision.
    /// </summary>
    /// <exception cref="RuleRefusedException">The judge's refusal; the caller's transaction rolls back.</exception>
    /// <exception cref="DriverException">The store is before the step that keeps rules.</exception>
    public static long WriteIn(
        DesktopWrite write, string place, long revision, IReadOnlyList<SupervisorRule> rules, string door, SupervisorRuleWorld world, TimeProvider? clock = null)
    {
        if (!Has(write)) throw new DriverException("this home's store is not yet at the version that keeps the supervisor's rules, so nothing was saved; try again after the next start.");
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        var held = SupervisorRuleBook.WithSeed(Sets(write.Query(RulesSql, RuleOf), write.Query(RevisionsSql, row => (row.GetString(0), row.GetInt64(1)))));
        if (SupervisorRules.Judge(place, revision, rules, held, world) is { } refusal) throw new RuleRefusedException(refusal);
        var next = revision + 1;
        write.Execute("DELETE FROM supervisor_rules WHERE place = $place", ("$place", place));
        var ord = 0;
        foreach (var rule in rules)
        {
            write.Execute(
                """
                INSERT INTO supervisor_rules (place, ord, id, roles, count, window, at, do, up_to, revision)
                VALUES ($place, $ord, $id, $roles, $count, $window, $at, $do, $upTo, $revision)
                """,
                ("$place", place), ("$ord", ord++), ("$id", rule.Id), ("$roles", rule.Roles is null ? null : JsonSerializer.Serialize(rule.Roles)),
                ("$count", rule.Count), ("$window", rule.Window), ("$at", rule.At), ("$do", rule.Do), ("$upTo", rule.UpTo), ("$revision", next));
        }

        write.Execute(
            "INSERT INTO supervisor_rule_revisions (place, revision, rules, at, door) VALUES ($place, $revision, $rules, $at, $door)",
            ("$place", place), ("$revision", next), ("$rules", JsonSerializer.Serialize(rules)), ("$at", now), ("$door", door));
        write.Moved(Family);
        return next;
    }

    /// <summary>
    /// <paramref name="rules"/> laid over <paramref name="place"/>'s set as this transaction reads it: each rule there on the same count naming the
    /// same roles replaced, the rest kept, and the set written at the revision it stands at. What a preset's rules are written by.
    /// </summary>
    /// <exception cref="RuleRefusedException">The judge's refusal of the set as it would stand.</exception>
    public static long MergeIn(DesktopWrite write, string place, IReadOnlyList<SupervisorRule> rules, string door, SupervisorRuleWorld world)
    {
        var held = Sets(write.Query(RulesSql, RuleOf), write.Query(RevisionsSql, row => (row.GetString(0), row.GetInt64(1))));
        var set = held.FirstOrDefault(each => each.Place == place) ?? new SupervisorRuleSet(place, 0, []);
        static string Roles(SupervisorRule rule) => rule.Roles is null ? "" : string.Join(",", rule.Roles.Order(StringComparer.Ordinal));
        var kept = set.Rules.Where(old => !rules.Any(rule => rule.Count == old.Count && Roles(rule) == Roles(old)));
        return WriteIn(write, place, set.Revision, [.. kept, .. rules], door, world);
    }

    /// <summary>Whether the machine's rules were ever written here (a revision row at the machine's place): a home's seed or its migration.</summary>
    public bool Seeded() => All().Any(set => set.Place == SupervisorRules.Machine);

    /// <summary>Whether the store this write is in has the rules' tables.</summary>
    public static bool Has(DesktopWrite write) =>
        write.Scalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'supervisor_rules'") > 0;

    private const string RulesSql = "SELECT id, roles, count, window, at, do, up_to, place FROM supervisor_rules ORDER BY place, ord";

    private const string RevisionsSql = "SELECT place, MAX(revision) FROM supervisor_rule_revisions GROUP BY place";

    private static SupervisorRule RuleOf(SqliteDataReader row) =>
        new(row.GetString(0), row.GetString(2), row.GetString(3), row.GetInt32(4), row.GetString(5),
            row.IsDBNull(6) ? null : row.GetInt32(6), row.GetString(7),
            row.IsDBNull(1) ? null : JsonSerializer.Deserialize<string[]>(row.GetString(1)));

    // A place emptied by a write keeps its revision, so the next write is read against it.
    private static IReadOnlyList<SupervisorRuleSet> Sets(IReadOnlyList<SupervisorRule> rules, IReadOnlyList<(string Place, long Revision)> revisions) =>
        [.. revisions.Select(each => new SupervisorRuleSet(each.Place, each.Revision, [.. rules.Where(rule => rule.Place == each.Place)]))];

    private static IReadOnlyList<SupervisorRuleSet> Read(DesktopStoreReader store) =>
        Sets(store.Query(RulesSql, RuleOf), store.Query(RevisionsSql, row => (row.GetString(0), row.GetInt64(1))));
}

/// <summary>A write of a place's rules the judge refused (<see cref="SupervisorRules.Judge"/>): its closed code, sentence and place; nothing written.</summary>
public sealed class RuleRefusedException(RuleRefusal refusal) : DriverException(refusal.Message)
{
    public RuleRefusal Refusal { get; } = refusal;

    /// <summary>One of <see cref="SupervisorRules.Codes"/>.</summary>
    public string Code => Refusal.Code;
}
