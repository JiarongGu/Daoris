namespace Daoris.Driver;

/// <summary>
/// The supervisor's rules as the executors read them: every place's set read once, and
/// for a line its effective rules and the number or move each executor acts on.
/// </summary>
public sealed record SupervisorRuleBook(IReadOnlyList<SupervisorRuleSet> Sets)
{
    /// <summary>How many go-ons by answers alone a line takes in a row: the seeded rule's number, which the course's bound reads.</summary>
    public const int AnswersAloneBound = 2;

    /// <summary>The failed sessions the seeded failures rule parks a quest at.</summary>
    public const int FailuresByDefault = 3;

    /// <summary>The seeded rules alone, at the machine's place: what decides where the store holds no machine's rules or does not read.</summary>
    public static SupervisorRuleBook Seed { get; } = new([new SupervisorRuleSet(SupervisorRules.Machine, 0, Seeded(FailuresByDefault))]);

    /// <summary>
    /// The rules <paramref name="home"/>'s store holds now. Every home is seeded at its first start or by its migration; where the machine's
    /// place holds nothing, or the store does not read, the seeded rules decide there, so no line goes on with no tries rule.
    /// </summary>
    public static SupervisorRuleBook Read(string home)
    {
        try
        {
            var sets = new SupervisorRuleStore(home).All();
            return sets.Any(set => set.Place == SupervisorRules.Machine) ? new(sets) : new([.. sets, .. Seed.Sets]);
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            return Seed;
        }
    }

    /// <summary>A line's effective rules per count, for its role at its place.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> For(string? role, string? repository, string? workspace) =>
        SupervisorRules.Effective(Sets, SupervisorRules.Chain(repository, workspace), role);

    /// <summary>The rule on <paramref name="count"/> that acts by <paramref name="does"/> first (its lowest <c>at</c>), for the act's record; none where none does.</summary>
    public static SupervisorRule? Acting(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective, string count, string does) =>
        effective.TryGetValue(count, out var rules) ? rules.Where(rule => rule.Do == does).MinBy(rule => rule.At) : null;

    /// <summary>The revision <paramref name="place"/>'s set is read at: 0 for a place holding none, or the seed read where none is written.</summary>
    public long RevisionOf(string place) => Sets.FirstOrDefault(set => set.Place == place)?.Revision ?? 0;

    /// <summary>
    /// The answers alone in a row a line goes on by before it waits: for the person, or for the supervisor a <c>refer</c> asks, the line
    /// held alike behind the count's door; null where no rule holds it.
    /// </summary>
    public static int? AnswersAloneAt(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective) => AnswersAloneActing(effective)?.At;

    /// <summary>The answers-alone rule that holds a line, <c>hold</c> or <c>refer</c>, at its lowest <c>at</c>; none where neither does.</summary>
    public static SupervisorRule? AnswersAloneActing(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective) =>
        effective.TryGetValue(SupervisorRules.AnswersAlone, out var rules)
            ? rules.Where(rule => rule.Do is SupervisorRules.Hold or SupervisorRules.Refer).MinBy(rule => rule.At)
            : null;

    /// <summary>The failed sessions that park a quest, by <c>hold</c> or by <c>refer</c>, which parks it alike; 0 where no rule holds it.</summary>
    public static int FailuresAt(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective) =>
        Lowest(effective, SupervisorRules.Failures, SupervisorRules.Hold, SupervisorRules.Refer) ?? 0;

    /// <summary>The failed sessions that park a quest at the machine's place, for any role: where a line with no narrower rule parks; 0 where none holds.</summary>
    public int MachineFailures => FailuresAt(For(null, null, null));

    /// <summary>The tries a run's transient episode makes: the lowest <c>at</c> of the effective tries rules, which every line holds (the judge refuses a write leaving none).</summary>
    public static int TriesLimit(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective) =>
        effective.TryGetValue(SupervisorRules.Tries, out var rules) && rules.Count > 0 ? Math.Clamp(rules.Min(rule => rule.At), 1, TransientRetry.Bound) : TransientRetry.Bound;

    /// <summary>
    /// The policy the staffing course reads: <paramref name="applied"/>'s rule, weights, shares, budgets and works,
    /// its moves and its bound taken from the effective rules alone. A tries rule that holds is the episode's limit, no move; a role with no policy
    /// and no move rule is null, today's order.
    /// </summary>
    public static StaffPolicy? Course(StaffPolicy? applied, IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective)
    {
        var moves = new Dictionary<string, StaffMove>(StringComparer.Ordinal);
        if (First(effective, SupervisorRules.Tries) is { Do: not SupervisorRules.Hold } tries) moves[StaffConditions.Transient] = new(tries.Do, tries.At);
        // A refer asks as ask does: the quest waits at the part's end, for the person's yes or the supervisor's move, both one approval.
        if (First(effective, SupervisorRules.FailuresOnChoice) is { } failed) moves[StaffConditions.Failed] = new(Asking(failed.Do), failed.At);
        if (First(effective, SupervisorRules.Quiet) is { } quiet) moves[StaffConditions.Quiet] = new(quiet.Do);
        if (First(effective, SupervisorRules.Limits) is { } limit) moves[StaffConditions.Limit] = new(Asking(limit.Do), UpTo: limit.UpTo);
        int? most = First(effective, SupervisorRules.Moves) is { Do: SupervisorRules.Ask or SupervisorRules.Refer } bound ? bound.At : null;
        if (applied is null && moves.Count == 0) return null;
        return applied is null
            ? new StaffPolicy(StaffRules.Order, [], moves, most)
            : applied with { Moves = moves, Most = most };
    }

    /// <summary>The moves a line makes a day before the seeded moves rule asks, and a preset's bound.</summary>
    public const int MovesByDefault = 3;

    /// <summary>
    /// The rules a preset brings: <i>Spread evenly</i> and <i>Spread by weight</i> move a line by weight on a limit, at most
    /// <see cref="MovesByDefault"/> moves a day, as rules naming <paramref name="role"/> at <paramref name="place"/>; <i>First that can run</i> none.
    /// </summary>
    public static IReadOnlyList<SupervisorRule> PresetRules(string preset, string role, string place)
    {
        if (preset is not (StaffPresets.Even or StaffPresets.Weighted)) return [];
        SupervisorRule Rule(string count, int at, string does) => new($"{role}-{count}", count, SupervisorRules.Windows[count], at, does, null, place, [role]);
        return [Rule(SupervisorRules.Limits, 1, StaffActions.Weighted), Rule(SupervisorRules.Moves, MovesByDefault, SupervisorRules.Ask)];
    }

    /// <summary>
    /// The machine's rules every home holds at today's numbers, a fresh home's and a migrated one's
    /// alike: answers alone 2 in a row, failed sessions at <paramref name="failures"/> (none at 0; an older home's migration hands its file's
    /// number), five transient tries, and three moves a day before the next asks, for every role.
    /// </summary>
    public static IReadOnlyList<SupervisorRule> Seeded(int failures = FailuresByDefault) =>
    [
        new("answers-alone", SupervisorRules.AnswersAlone, SupervisorRules.Windows[SupervisorRules.AnswersAlone], AnswersAloneBound, SupervisorRules.Hold, null, SupervisorRules.Machine, null),
        .. failures > 0
            ? [new SupervisorRule("failures", SupervisorRules.Failures, SupervisorRules.Windows[SupervisorRules.Failures], Math.Min(failures, 20), SupervisorRules.Hold, null, SupervisorRules.Machine, null)]
            : Array.Empty<SupervisorRule>(),
        new("tries", SupervisorRules.Tries, SupervisorRules.EpisodeWindow, TransientRetry.Bound, SupervisorRules.Hold, null, SupervisorRules.Machine, null),
        new("moves", SupervisorRules.Moves, SupervisorRules.Windows[SupervisorRules.Moves], MovesByDefault, SupervisorRules.Ask, null, SupervisorRules.Machine, null),
    ];

    /// <summary>
    /// A fresh home's first start, beside its workflow's seed: the seeded rules written at the machine's place, so the person reads and
    /// edits them where every line reads them. A store that does not take them now is seeded at a later start; until then <see cref="Read"/>
    /// decides by the same rules.
    /// </summary>
    /// <returns>Whether the rules were written.</returns>
    public static bool SeedFresh(string home, Func<StoreSettled>? settled = null)
    {
        var store = new SupervisorRuleStore(home, settled);
        if (store.Seeded()) return false;
        return store.Write(SupervisorRules.Machine, 0, Seeded(), SeedDoor, SupervisorRuleDoor.World(home)).Stored;
    }

    /// <summary>The door a seed's revision is kept as written by.</summary>
    public const string SeedDoor = "seed";

    // The rule a count's executor acts on: the lowest `at` of its ladder, where a later step can only be reached past it.
    private static SupervisorRule? First(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective, string count) =>
        effective.TryGetValue(count, out var rules) ? rules.Where(rule => rule.Do != SupervisorRules.Off).MinBy(rule => rule.At) : null;

    private static string Asking(string does) => does == SupervisorRules.Refer ? StaffActions.Ask : does;

    private static int? Lowest(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective, string count, params string[] does) =>
        effective.TryGetValue(count, out var rules) && rules.Where(rule => does.Contains(rule.Do)).Select(rule => (int?)rule.At).Min() is { } at ? at : null;
}
