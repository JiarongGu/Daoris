using System.Globalization;

namespace Daoris.Driver;

/// <summary>A choice's reported tokens today (§2.2): what was reported, and whether a turn on it has not reported yet, which is never zero.</summary>
public sealed record StaffTokens(long Reported, bool Unreported);

/// <summary>
/// What <see cref="StaffJudge"/> arranges by (the staff policy design §3.1), all values, read by the ledger and the line before it asks.
/// </summary>
public sealed record StaffFacts
{
    /// <summary>The choices of the role's last new starts since its policy's <c>since</c>, oldest first; at most <see cref="StaffJudge.Window"/> are read.</summary>
    public IReadOnlyList<StaffKey> Window { get; init; } = [];

    /// <summary>This look's reservations by choice: a pending start counts in the window and against a budget.</summary>
    public IReadOnlyDictionary<StaffKey, int> Reserved { get; init; } = new Dictionary<StaffKey, int>();

    /// <summary>The role's new starts by choice in the machine's local day.</summary>
    public IReadOnlyDictionary<StaffKey, int> Today { get; init; } = new Dictionary<StaffKey, int>();

    /// <summary>The role's reported tokens by choice in the machine's local day.</summary>
    public IReadOnlyDictionary<StaffKey, StaffTokens> Tokens { get; init; } = new Dictionary<StaffKey, StaffTokens>();

    /// <summary>Each agent marked not answering, until when (§4.2).</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> NotAnswering { get; init; } = new Dictionary<string, DateTimeOffset>();

    /// <summary>Each choice this line moved off, when.</summary>
    public IReadOnlyDictionary<StaffKey, DateTimeOffset> MovedOff { get; init; } = new Dictionary<StaffKey, DateTimeOffset>();

    /// <summary>The moment the marks and the hold are read at.</summary>
    public DateTimeOffset Now { get; init; }
}

/// <summary>One entry the walk takes in turn (§3.1): why it stands where it does, by a code of <see cref="StaffBy"/>, and that said.</summary>
public sealed record StaffArranged(ActorEffective Entry, string By, string Said);

/// <summary>
/// <see cref="StaffJudge.Arrange"/>'s answer: the rule it arranged by; the entries to walk, in order; those passed before any account is
/// asked, with their code of <see cref="StaffPasses"/>; and those that take another work, which leave the walk unsaid.
/// </summary>
public sealed record StaffArrangement(string Rule, IReadOnlyList<StaffArranged> Walk, IReadOnlyList<BuildPass> Passed, IReadOnlyList<ActorEffective> Left);

/// <summary>A line's facts for <see cref="StaffJudge.OnCondition"/>: whether its role lists another choice or agent, and its moves in 24 hours.</summary>
public sealed record StaffLine(bool Elsewhere, int Moves);

/// <summary>
/// A met condition's action (§4): what to do, the tries or strikes before it, how long a wait holds, whether it is today's for want of a
/// move set, and whether the line's bound turned a move into an ask (<c>held.moves-bound</c>).
/// </summary>
public sealed record StaffAction(string Condition, string Do, int? After, int? UpTo, bool AsToday, bool MovesBound);

/// <summary>Why an arranged entry stands where it does (§3.1), recorded as a start's <c>chosen.by</c>.</summary>
public static class StaffBy
{
    /// <summary>The stored order: <i>first that can run</i>, or an order walked as given.</summary>
    public const string First = "first";

    /// <summary>The weighted pick.</summary>
    public const string Weight = "weight";

    /// <summary>An entry ahead of it in the rule's order is over its share.</summary>
    public const string Share = "share";

    /// <summary>An entry ahead of it in the rule's order is over its daily budget.</summary>
    public const string Budget = "budget";

    /// <summary>It is the only one of the role's choices taking the start's work.</summary>
    public const string Work = "work";

    public static IReadOnlyList<string> Codes { get; } = [First, Weight, Share, Budget, Work];
}

/// <summary>The codes a policy passes an entry by before any account is asked (§3.1), beside the walk's own (<see cref="StartPasses"/>).</summary>
public static class StaffPasses
{
    /// <summary>Over today's budget of starts or reported tokens.</summary>
    public const string OverBudget = "over-budget";

    /// <summary>Its agent is marked not answering, where the role moves on <c>transient</c>.</summary>
    public const string NotAnswering = "not-answering";

    /// <summary>This line moved off it within <see cref="StaffJudge.Hold"/>, where the role moves on <c>transient</c>.</summary>
    public const string MovedOff = "moved-off";

    public static IReadOnlyList<string> Codes { get; } = [OverBudget, NotAnswering, MovedOff];
}

/// <summary>
/// A role's policy's two judges (STAFFPOLICY1a, D226; the staff policy design §3, §4), pure: the policy and its facts come in as values and
/// nothing is read inside. <see cref="Arrange"/> orders a start's walk over the role's own entries; <see cref="OnCondition"/> answers what a
/// met condition does. With no policy, or a stale one the caller hands as null, each answers today's.
/// </summary>
/// <remarks>
/// <para><b>The arrangement</b> (§3.1), in order: an entry whose works leave out the start's work leaves the walk; an entry over today's
/// budget is passed <c>over-budget</c>; where the role moves on <c>transient</c>, an entry on an agent marked not answering is passed
/// <c>not-answering</c> and one this line moved off within <see cref="Hold"/> <c>moved-off</c>, each in the stored order and only while
/// another entry remains; then the rule orders the rest. A design start whose role names design for no choice is served by the choices
/// taking build, as a design step falls to build's role.</para>
/// <para><b>The weighted pick</b> (§3.2): over the window's <c>N</c> starts and this look's reservations, an entry of weight <c>w</c> above 0
/// with <c>n</c> of them scores <c>w × (N + 1) − n × W</c>, <c>W</c> the sum of the role's weights, which is <c>W</c> times the design's
/// <c>w / W × (N + 1) − n</c> in whole numbers: the highest first, ties by the stored order; weight-0 entries last, in order. Under either
/// rule an entry with a share is within it where one more start keeps it at most that share, <c>(n + 1) × 100 ≤ share × (N + 1)</c>, and
/// one over it walks after every entry within.</para>
/// </remarks>
public static class StaffJudge
{
    /// <summary>How many of the role's last new starts the weighted pick and a share read (§3.2).</summary>
    public const int Window = 20;

    /// <summary>How long a choice a line moved off is passed (§3.1): the curve's hold, <see cref="SwitchDecision.Hold"/>.</summary>
    public static TimeSpan Hold => SwitchDecision.Hold;

    /// <param name="policy">The role's policy; null for none or a stale one, which walks the order as given.</param>
    /// <param name="work">The start's work.</param>
    /// <param name="order">
    /// The effective order the walk reads; only an order of the role's own entries (<c>From</c> 1 to 4) is arranged, and one holding a pick,
    /// a handed agent, <i>as before</i> or <c>rule-default</c> walks as given.
    /// </param>
    /// <param name="facts">The window, the reservations, today's counts, the marks and the line's moves off.</param>
    public static StaffArrangement Arrange(StaffPolicy? policy, string work, IReadOnlyList<ActorEffective> order, StaffFacts facts)
    {
        if (policy is null || order.Count == 0 || !order.All(entry => Own(entry.From)))
        {
            return new StaffArrangement(
                policy?.Rule ?? StaffRules.Order,
                [.. order.Select(entry => new StaffArranged(entry, StaffBy.First, Own(entry.From) ? $"choice {entry.From} in the stored order" : "walked as given"))],
                [],
                []);
        }

        var entries = order.Select((entry, index) => new Entry(entry, index, policy.Of(StaffKey.Of(entry)) ?? new StaffChoice(StaffKey.Of(entry)))).ToList();
        var designNamed = entries.Any(each => each.Choice.Takes?.Contains(RoleWork.Design, StringComparer.Ordinal) == true);
        var served = entries.Where(each => Serves(each.Choice, work, designNamed)).ToList();
        var left = entries.Where(each => !served.Contains(each)).ToList();

        var window = facts.Window.Count > Window ? facts.Window.Skip(facts.Window.Count - Window).ToList() : facts.Window;
        var counts = new Dictionary<StaffKey, long>();
        foreach (var key in window) counts[key] = counts.GetValueOrDefault(key) + 1;
        foreach (var (key, reserved) in facts.Reserved) counts[key] = counts.GetValueOrDefault(key) + reserved;
        var spread = new Spread(policy.Rule, counts, window.Count + facts.Reserved.Values.Sum(), entries.Sum(each => (long)Math.Max(0, each.Choice.Weight)));

        var passed = new List<(Entry Entry, string Code, string Why)>();
        foreach (var each in served)
        {
            if (OverBudget(each, facts) is { } why) passed.Add((each, StaffPasses.OverBudget, why));
        }

        var remaining = served.Where(each => passed.All(pass => pass.Entry != each)).ToList();
        if (policy.Moves.TryGetValue(StaffConditions.Transient, out var transient) && StaffActions.Moves(transient.Do))
        {
            foreach (var each in remaining.ToList())
            {
                if (remaining.Count > 1 && facts.NotAnswering.Any(mark => string.Equals(mark.Key, each.Effective.Agent, StringComparison.OrdinalIgnoreCase) && mark.Value > facts.Now))
                {
                    remaining.Remove(each);
                    passed.Add((each, StaffPasses.NotAnswering, "its agent's service did not answer: it is passed until its mark ends or a prompt on it answers"));
                }
            }

            foreach (var each in remaining.ToList())
            {
                if (remaining.Count > 1 && facts.MovedOff.TryGetValue(each.Key, out var at) && at <= facts.Now && facts.Now - at < Hold)
                {
                    remaining.Remove(each);
                    var minutes = (int)(facts.Now - at).TotalMinutes;
                    passed.Add((each, StaffPasses.MovedOff, $"this line moved off it {minutes} minute{(minutes == 1 ? "" : "s")} ago, within the {(int)Hold.TotalMinutes}-minute hold"));
                }
            }
        }

        var ranked = spread.Rank(served, withShare: true);
        var walk = new List<StaffArranged>();
        foreach (var each in ranked.Where(remaining.Contains))
        {
            var (by, said) = Reason(each);
            walk.Add(new StaffArranged(each.Effective, by, said));
        }

        return new StaffArrangement(
            policy.Rule,
            walk,
            [.. passed.OrderBy(pass => pass.Entry.Index).Select(pass => new BuildPass(pass.Entry.Effective, pass.Code, pass.Why))],
            [.. left.Select(each => each.Effective)]);

        (string By, string Said) Reason(Entry each)
        {
            if (served.Count == 1 && left.Count > 0) return (StaffBy.Work, $"the only choice taking {work}");
            var ahead = ranked.TakeWhile(other => other != each).ToList();
            if (ahead.FirstOrDefault(other => passed.Any(pass => pass.Entry == other && pass.Code == StaffPasses.OverBudget)) is { } over)
            {
                return (StaffBy.Budget, $"choice {over.Effective.From} is over its daily budget");
            }

            if (spread.Within(each)
                && spread.Rank(served, withShare: false).TakeWhile(other => other != each).FirstOrDefault(other => !spread.Within(other)) is { } full)
            {
                return (StaffBy.Share, $"choice {full.Effective.From} is over its share: {spread.Count(full)} of the last {spread.Total} starts, at most {full.Choice.Share}%");
            }

            return policy.Rule == StaffRules.Weight ? (StaffBy.Weight, spread.Said(each)) : (StaffBy.First, $"choice {each.Effective.From} in the stored order");
        }
    }

    /// <summary>
    /// What a met <paramref name="condition"/> does (§4): the policy's move where it set one, else today's; a move to another choice made the
    /// person's ask where the line already made the policy's bound of moves in 24 hours (§4.2).
    /// </summary>
    /// <exception cref="ArgumentException">A condition this build does not observe.</exception>
    public static StaffAction OnCondition(StaffPolicy? policy, string condition, StaffLine line)
    {
        if (!StaffConditions.Codes.Contains(condition, StringComparer.Ordinal))
        {
            throw new ArgumentException($"`{condition}` is not a condition — one of {string.Join(", ", StaffConditions.Codes)}.", nameof(condition));
        }

        // A move this build does not know, kept as a newer build wrote it, reads as today's.
        StaffMove? move = null;
        var set = policy is not null && policy.Moves.TryGetValue(condition, out move) && StaffActions.Codes.Contains(move.Do, StringComparer.Ordinal);
        var action = set ? new StaffAction(condition, move!.Do, After(condition, move), move.UpTo, false, false) : Today(condition, line);
        return policy?.Bound is { } bound && line.Moves >= bound && StaffActions.Moves(action.Do)
            ? action with { Do = StaffActions.Ask, After = null, UpTo = null, MovesBound = true }
            : action;
    }

    /// <summary>
    /// Today's action for <paramref name="condition"/> (§4's last column): a limit walks on where the role lists another choice or agent, else
    /// waits; a refused sign-in or model and a budget walk on; a service that does not answer holds after the episode's tries; a fatal error
    /// and a quiet stop are tried again.
    /// </summary>
    private static StaffAction Today(string condition, StaffLine line) => condition switch
    {
        StaffConditions.Limit => new(condition, line.Elsewhere ? StaffActions.Next : StaffActions.Wait, null, null, true, false),
        StaffConditions.Transient => new(condition, StaffActions.Hold, TransientRetry.Bound, null, true, false),
        StaffConditions.Failed or StaffConditions.Quiet => new(condition, StaffActions.Retry, null, null, true, false),
        _ => new(condition, StaffActions.Next, null, null, true, false),
    };

    /// <summary>The tries a transient move waits for, its own or the episode's bound; the strikes a fatal move waits for, its own or the first.</summary>
    private static int? After(string condition, StaffMove move) => condition switch
    {
        StaffConditions.Transient => move.After ?? TransientRetry.Bound,
        StaffConditions.Failed when move.Do != StaffActions.Retry => move.After ?? 1,
        _ => null,
    };

    /// <summary>Whether <paramref name="from"/> is the role's own entry, choice 1 to <see cref="ActorChoices.Most"/>.</summary>
    private static bool Own(string from) =>
        int.TryParse(from, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= ActorChoices.Most;

    private static bool Serves(StaffChoice choice, string work, bool designNamed) =>
        choice.Takes is not { } takes
        || takes.Contains(work, StringComparer.Ordinal)
        || (work == RoleWork.Design && !designNamed && takes.Contains(RoleWork.Build, StringComparer.Ordinal));

    private static string? OverBudget(Entry each, StaffFacts facts)
    {
        if (each.Choice.Budget is not { } budget) return null;
        if (budget.Starts is { } starts)
        {
            var used = facts.Today.GetValueOrDefault(each.Key) + facts.Reserved.GetValueOrDefault(each.Key);
            return used >= starts ? $"over its daily budget: {used} of {starts} starts today" : null;
        }

        if (budget.Tokens is not { } tokens || facts.Tokens.GetValueOrDefault(each.Key) is not { } reported) return null;
        if (reported.Unreported) return $"a turn on it has not reported its tokens yet, so its daily budget of {tokens} reported tokens holds it";
        return reported.Reported >= tokens ? $"over its daily budget: {reported.Reported} of {tokens} reported tokens today" : null;
    }

    private sealed record Entry(ActorEffective Effective, int Index, StaffChoice Choice)
    {
        public StaffKey Key => Choice.Key;
    }

    /// <summary>The window's counts, by which the rule ranks and a share is weighed.</summary>
    private sealed class Spread(string rule, IReadOnlyDictionary<StaffKey, long> counts, long total, long weights)
    {
        public long Total => total;

        public long Count(Entry each) => counts.GetValueOrDefault(each.Key);

        public bool Within(Entry each) => each.Choice.Share is not { } share || (Count(each) + 1) * 100 <= share * (total + 1);

        private long Score(Entry each) => each.Choice.Weight * (total + 1) - Count(each) * weights;

        /// <summary>The rule's order of <paramref name="entries"/>; with the shares, every entry within its share before every one over it.</summary>
        public IReadOnlyList<Entry> Rank(IReadOnlyList<Entry> entries, bool withShare)
        {
            if (rule != StaffRules.Weight || weights == 0)
            {
                return withShare ? [.. entries.Where(Within), .. entries.Where(each => !Within(each))] : entries;
            }

            IEnumerable<Entry> Weighed(IEnumerable<Entry> some) => some.OrderByDescending(Score).ThenBy(each => each.Index);
            var weighted = entries.Where(each => each.Choice.Weight > 0).ToList();
            var zero = entries.Where(each => each.Choice.Weight <= 0);
            return withShare
                ? [.. Weighed(weighted.Where(Within)), .. Weighed(weighted.Where(each => !Within(each))), .. zero]
                : [.. Weighed(weighted), .. zero];
        }

        /// <summary>The weighted pick's sentence: the entry's starts among the window's, and its aim as a fraction.</summary>
        public string Said(Entry each)
        {
            if (each.Choice.Weight <= 0) return $"weight 0: choice {each.Effective.From} runs only when no other can";
            var common = Common(each.Choice.Weight, weights);
            var aim = $"aim {each.Choice.Weight / common} in {weights / common}";
            return total == 0
                ? $"by weight: no new start yet in its window, {aim}"
                : $"by weight: {Count(each)} of the last {total} were choice {each.Effective.From}, {aim}";
        }

        private static long Common(long a, long b) => b == 0 ? a : Common(b, a % b);
    }
}
