using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris bus stalls</c>: the Stalls view, read from the home's store and changing nothing: the armed timers, and each obliged entry the
/// watchdog marked stalled, newest first: what (its type and subject), since when, who is obliged, which retry, and what it would do next
/// (try again, or the give-up it would apply and what it would hand to whom). <c>--compare</c> sets the look's reading beside each stall:
/// agreements, disagreements and unknowns by type, and the newest disagreements with both sides.
/// </summary>
/// <remarks>
/// In shadow: nothing the watchdog records changes a run, a session or a notice yet. The window has no diagnostics place, so this
/// terminal view is the only one until the window reads the bus's views through its pushed slices. Exit codes: 0 read, or the home keeps no log
/// yet, said · 2 a word it does not answer. The <c>bus</c> group's runner (<see cref="BusStatsCommand"/>) hands it its words.
/// </remarks>
public static class BusStallsCommand
{
    public const string Usage =
        "usage: daoris bus stalls [--compare]   the watchdog's stalls, in shadow: each obliged entry past due, since when, who is obliged,\n"
        + "                                       which retry and the give-up it would apply; --compare sets the look's reading beside each;\n"
        + "                                       changes nothing";

    /// <summary>The stalled entries listed, newest first.</summary>
    public const int Listed = 50;

    /// <summary>The disagreements listed, newest first.</summary>
    public const int Disagreements = 20;

    /// <summary>The newest stall and give-up facts read.</summary>
    private const int Read = 5000;

    /// <param name="args">The words after <c>bus</c>, <c>stalls</c> first.</param>
    /// <param name="home">The Daoris home.</param>
    /// <param name="clock">What time it is, which says which timers are past due.</param>
    /// <param name="said">What the verb said, for the caller to print.</param>
    public static int Run(string[] args, string home, Func<DateTimeOffset> clock, out string said)
    {
        var output = new StringBuilder();
        var code = Run(args, home, clock, output);
        said = output.ToString();
        return code;
    }

    private static int Run(string[] args, string home, Func<DateTimeOffset> clock, StringBuilder output)
    {
        bool compare;
        switch (args)
        {
            case ["stalls"]: compare = false; break;
            case ["stalls", "--compare"]: compare = true; break;
            default:
                output.AppendLine("bus stalls: read the stalls, and at most --compare.");
                output.AppendLine(Usage);
                return 2;
        }

        using var store = DesktopBusLogStore.Reading(home);
        if (store is null)
        {
            output.AppendLine("bus stalls: this home keeps no bus log yet: its store has not been opened by a build that keeps one, and nothing was read.");
            return 0;
        }

        var now = clock();
        var armed = store.Timers(null, int.MaxValue);
        var facts = store.Newest([BusTypes.Stalled.Kind, BusTypes.GiveUp.Kind], Read);
        output.AppendLine("bus stalls: the bus's watchdog on this home, in shadow: nothing it records changes a run, a session or a notice yet.");
        var overdue = armed.Count(timer => timer.Due <= now);
        output.AppendLine(armed.Count == 0
            ? "armed: no timer."
            : Invariant($"armed: {armed.Count} timer{(armed.Count == 1 ? "" : "s")}, {overdue} past due now; the next due {When(armed.Min(timer => timer.Due))}."));

        var stalls = Stalls(facts);
        if (stalls.Count == 0)
        {
            output.AppendLine("stalled: none.");
            return 0;
        }

        output.AppendLine(Invariant($"stalled: {stalls.Count} entr{(stalls.Count == 1 ? "y" : "ies")}, newest first{(stalls.Count > Listed ? Invariant($", the first {Listed}") : "")}:"));
        foreach (var stall in stalls.Take(Listed)) output.AppendLine("  " + Line(stall));
        if (compare) Compare(stalls, output);
        return 0;
    }

    /// <summary>Each stalled entry's newest stall, and its give-up where it reached one, newest first.</summary>
    private static List<Stall> Stalls(IReadOnlyList<BusEnvelope> facts)
    {
        var gaveUp = new Dictionary<string, BusEnvelope>(StringComparer.Ordinal);
        foreach (var fact in facts.Where(fact => fact.Kind == BusTypes.GiveUp.Kind))
        {
            if (Text(fact, "entry") is { } entry) gaveUp.TryAdd(entry, fact);
        }

        var stalls = new List<Stall>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fact in facts.Where(fact => fact.Kind == BusTypes.Stalled.Kind))
        {
            if (Text(fact, "entry") is not { } entry || !seen.Add(entry)) continue;
            stalls.Add(new Stall(fact, gaveUp.GetValueOrDefault(entry)));
        }

        return stalls;
    }

    private static string Line(Stall stall)
    {
        var fact = stall.Newest;
        var obliged = Text(fact, "obliged") ?? "unknown";
        var said = Invariant($"{Text(fact, "kind")} {fact.Subject} since {When(Moment(fact, "since"))}: obliged {obliged}, ")
                   + Invariant($"{Missed(fact)}, past due since {When(Moment(fact, "due"))}; ");
        if (stall.GaveUp is { } gave)
        {
            var to = gave.Payload.FirstOrDefault(pair => pair.Key == "to").Value as IReadOnlyList<string> ?? [];
            var applied = gave.Payload.FirstOrDefault(pair => pair.Key == "applied").Value is true;
            return said + $"gave up{(applied ? "" : " (would)")}: {Text(gave, "consequence")}, hand the {Text(gave, "hands")} to {string.Join(", then ", to)}";
        }

        return said + Invariant($"would try {obliged} again, next due {When(Moment(fact, "next"))}");
    }

    private static void Compare(IReadOnlyList<Stall> stalls, StringBuilder output)
    {
        output.AppendLine("compare: the look's reading beside each stalled entry's newest stall (agree: the look holds it open too; disagree: the look says handled; unknown: no reading):");
        foreach (var kind in stalls.GroupBy(stall => Text(stall.Newest, "kind") ?? "unknown", StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            int Count(string verdict) => kind.Count(stall => BusCompare.Verdict(Text(stall.Newest, "look")) == verdict);
            output.AppendLine(Invariant($"  {kind.Key} agree {Count(BusCompare.Agree)}, disagree {Count(BusCompare.Disagree)}, unknown {Count(BusCompare.Unknown)}"));
        }

        var disagreed = stalls.Where(stall => BusCompare.Verdict(Text(stall.Newest, "look")) == BusCompare.Disagree).ToList();
        if (disagreed.Count == 0)
        {
            output.AppendLine("disagreements: none.");
            return;
        }

        output.AppendLine(Invariant($"disagreements, newest first ({Math.Min(disagreed.Count, Disagreements)} of {disagreed.Count}):"));
        foreach (var stall in disagreed.Take(Disagreements))
        {
            var fact = stall.Newest;
            output.AppendLine(Invariant(
                $"  {Text(fact, "kind")} {fact.Subject}: the bus: past due since {When(Moment(fact, "due"))}, {Missed(fact)}; the look: {Text(fact, "look")} ({Text(fact, "says") ?? "unsaid"})"));
        }
    }

    /// <summary>Which stall this is of the most its type allows: its retries, and the last, past which it gives up.</summary>
    private static string Missed(BusEnvelope fact) => Invariant($"stall {Number(fact, "retry")} of {Number(fact, "retries") + 1}");

    private sealed record Stall(BusEnvelope Newest, BusEnvelope? GaveUp);

    private static object? Value(BusEnvelope fact, string key) => fact.Payload.FirstOrDefault(pair => pair.Key == key).Value;

    private static string? Text(BusEnvelope fact, string key) => Value(fact, key) as string;

    private static long? Number(BusEnvelope fact, string key) => Value(fact, key) as long?;

    private static DateTimeOffset? Moment(BusEnvelope fact, string key) =>
        Value(fact, key) is long ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : Value(fact, key) as DateTimeOffset?;

    private static string When(DateTimeOffset? at) => at is { } moment ? moment.UtcDateTime.ToString("yyyy-MM-dd HH:mm'Z'", CultureInfo.InvariantCulture) : "unknown";

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
