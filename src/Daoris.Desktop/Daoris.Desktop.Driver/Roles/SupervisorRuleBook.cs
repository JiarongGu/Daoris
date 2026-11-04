namespace Daoris.Driver;

/// <summary>
/// The supervisor's rules as the executors read them (CONTEXTDRIVE1e; the context counters design §4.5): every place's set read once, whether
/// this home is reached, and for a line its effective rules and the number or move each executor acts on. On a home not reached every answer
/// is null and the executor reads its old number, kept under its register entry.
/// </summary>
public sealed record SupervisorRuleBook(bool Reached, IReadOnlyList<SupervisorRuleSet> Sets)
{
    /// <summary>How many go-ons by answers alone a line takes in a row (§2.3): the seeded rule's number, which the course's bound reads.</summary>
    public const int AnswersAloneBound = 2;

    /// <summary>A home whose rules are not reached: nothing here decides.</summary>
    public static SupervisorRuleBook NotReached { get; } = new(false, []);

    /// <summary>The rules <paramref name="home"/>'s store holds now; a store that does not read is not reached.</summary>
    public static SupervisorRuleBook Read(string home)
    {
        try
        {
            var sets = new SupervisorRuleStore(home).All();
            return new(sets.Any(set => set.Place == SupervisorRules.Machine), sets);
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            return NotReached;
        }
    }

    /// <summary>A line's effective rules per count, for its role at its place (§4.4).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> For(string? role, string? repository, string? workspace, string? line) =>
        SupervisorRules.Effective(Sets, SupervisorRules.Chain(line, repository, workspace), role);

    /// <summary>The role a record's line goes on under, as a rule names it: its actor where it says one, else its kind's.</summary>
    public static string RoleOf(PriorSession? record) =>
        record?.Actor is { Length: > 0 } actor ? actor
        : record?.HelpMode is not null ? RoleWork.AskDaoris
        : record?.Kind == SessionKinds.Chat ? RoleWork.Chat
        : RoleWork.Build;

    /// <summary>The answers alone in a row a line goes on by before it waits for the person; null where no rule holds it (the reached home's answer).</summary>
    public static int? AnswersAloneAt(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective) => Lowest(effective, SupervisorRules.AnswersAlone, SupervisorRules.Hold);

    /// <summary>The failed sessions that park a quest; 0 where no rule holds it.</summary>
    public static int FailuresAt(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective) => Lowest(effective, SupervisorRules.Failures, SupervisorRules.Hold) ?? 0;

    /// <summary>The tries a run's transient episode makes: the lowest <c>at</c> of the effective tries rules, which a reached home always holds.</summary>
    public static int TriesLimit(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective) =>
        effective.TryGetValue(SupervisorRules.Tries, out var rules) && rules.Count > 0 ? Math.Clamp(rules.Min(rule => rule.At), 1, TransientRetry.Bound) : TransientRetry.Bound;

    /// <summary>
    /// The policy the staffing course reads on a reached home (§4.8 read back): <paramref name="applied"/>'s rule, weights, shares, budgets and works,
    /// its moves and its bound taken from the effective rules alone. A tries rule that holds is the episode's limit, no move; a role with no policy
    /// and no move rule is null, today's order.
    /// </summary>
    public static StaffPolicy? Course(StaffPolicy? applied, IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective)
    {
        var moves = new Dictionary<string, StaffMove>(StringComparer.Ordinal);
        if (First(effective, SupervisorRules.Tries) is { Do: not SupervisorRules.Hold } tries) moves[StaffConditions.Transient] = new(tries.Do, tries.At);
        if (First(effective, SupervisorRules.FailuresOnChoice) is { } failed) moves[StaffConditions.Failed] = new(failed.Do, failed.At);
        if (First(effective, SupervisorRules.Quiet) is { } quiet) moves[StaffConditions.Quiet] = new(quiet.Do);
        if (First(effective, SupervisorRules.Limits) is { } limit) moves[StaffConditions.Limit] = new(limit.Do, UpTo: limit.UpTo);
        int? most = First(effective, SupervisorRules.Moves) is { Do: SupervisorRules.Ask } bound ? bound.At : null;
        if (applied is null && moves.Count == 0) return null;
        return applied is null
            ? new StaffPolicy(StaffRules.Order, [], moves, most)
            : applied with { Moves = moves, Most = most };
    }

    /// <summary>
    /// A policy's moves and its bound as rules naming <paramref name="role"/> at <paramref name="place"/> (§4.8): <c>transient after 2: weighted</c>
    /// is <c>tries at 2: weighted</c>, <c>failed after 2: next</c> <c>failures-on-choice at 2: next</c>, <c>limit: wait upTo 60</c>
    /// <c>limits at 1: wait</c> up to 60, <c>quiet: next</c> <c>quiet at 1: next</c>, and the bound <c>moves at n: ask</c>. A <c>retry</c> is the
    /// default course and a refused sign-in's, a refused model's and a budget's moves are the walk's, so none is a rule.
    /// </summary>
    public static IReadOnlyList<SupervisorRule> FromPolicy(StaffPolicy policy, string role, string place)
    {
        var rules = new List<SupervisorRule>();
        SupervisorRule Rule(string count, int at, string does, int? upTo = null) =>
            new($"{role}-{count}", count, SupervisorRules.Windows[count], at, does, upTo, place, [role]);
        foreach (var (condition, move) in policy.Moves.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (move.Do == StaffActions.Retry) continue;
            switch (condition)
            {
                case StaffConditions.Transient: rules.Add(Rule(SupervisorRules.Tries, Math.Clamp(move.After ?? TransientRetry.Bound, 1, TransientRetry.Bound), move.Do)); break;
                case StaffConditions.Failed: rules.Add(Rule(SupervisorRules.FailuresOnChoice, move.After ?? 1, move.Do)); break;
                case StaffConditions.Quiet: rules.Add(Rule(SupervisorRules.Quiet, 1, move.Do)); break;
                case StaffConditions.Limit: rules.Add(Rule(SupervisorRules.Limits, 1, move.Do, move.Do == StaffActions.Wait ? move.UpTo ?? 60 : null)); break;
            }
        }

        if (policy.Bound is { } bound && rules.Count > 0) rules.Add(Rule(SupervisorRules.Moves, bound, SupervisorRules.Ask));
        return rules;
    }

    /// <summary>
    /// The machine's rules every home holds at today's numbers (§6.1, the owner's answer of 2026-11-03): answers alone 2 in a row, failed
    /// sessions at <paramref name="strikes"/> (none at 0), and five transient tries, for every role.
    /// </summary>
    public static IReadOnlyList<SupervisorRule> Seeded(int strikes) =>
    [
        new("answers-alone", SupervisorRules.AnswersAlone, SupervisorRules.Windows[SupervisorRules.AnswersAlone], AnswersAloneBound, SupervisorRules.Hold, null, SupervisorRules.Machine, null),
        .. strikes > 0
            ? [new SupervisorRule("failures", SupervisorRules.Failures, SupervisorRules.Windows[SupervisorRules.Failures], Math.Min(strikes, 20), SupervisorRules.Hold, null, SupervisorRules.Machine, null)]
            : Array.Empty<SupervisorRule>(),
        new("tries", SupervisorRules.Tries, SupervisorRules.EpisodeWindow, TransientRetry.Bound, SupervisorRules.Hold, null, SupervisorRules.Machine, null),
    ];

    /// <summary>
    /// A fresh home's first start (§6.1), beside its workflow's seed: the seeded rules written at the machine's place, which reaches the home, so
    /// it reads its rules from one place as a converted home does. A store that does not take them leaves the home not reached, for the
    /// conversion's <c>rules</c> part to seed on the person's press.
    /// </summary>
    /// <returns>Whether the rules were written.</returns>
    public static bool SeedFresh(string home, DriverConfig config, Func<StoreSettled>? settled = null)
    {
        var store = new SupervisorRuleStore(home, settled);
        if (store.Reached()) return false;
        var world = SupervisorRuleDoor.World(home) with { ReadReached = false, Reached = true };
        return store.Write(SupervisorRules.Machine, 0, Seeded(config.Strikes), SeedDoor, world).Stored;
    }

    /// <summary>The door a seed's revision is kept as written by.</summary>
    public const string SeedDoor = "seed";

    // The rule a count's executor acts on: the lowest `at` of its ladder, where a later step can only be reached past it.
    private static SupervisorRule? First(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective, string count) =>
        effective.TryGetValue(count, out var rules) ? rules.Where(rule => rule.Do != SupervisorRules.Off).MinBy(rule => rule.At) : null;

    private static int? Lowest(IReadOnlyDictionary<string, IReadOnlyList<SupervisorRule>> effective, string count, string does) =>
        effective.TryGetValue(count, out var rules) && rules.Where(rule => rule.Do == does).Select(rule => (int?)rule.At).Min() is { } at ? at : null;
}
