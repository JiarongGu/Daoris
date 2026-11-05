using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One count of a line as Daoris hands it (CONTEXTDRIVE1b, CONTEXTDRIVE1i; the context-counters design §3, §5): the course's
/// <c>LineCount</c> as plain values, read below the course by the brief, a go-on's words and the supervisor's print and decision.
/// </summary>
/// <param name="Count">The count's code.</param>
/// <param name="N">What it stands at; none where it is unknown.</param>
/// <param name="Known"><c>exact</c>, <c>at-least</c> or <c>unknown</c>.</param>
/// <param name="Why">What is missing, for at least and unknown.</param>
/// <param name="From">Its one source.</param>
public sealed record CountFact(string Count, int? N, string Known, string? Why, string From)
{
    /// <summary>What Daoris does on this count on this line, and at what number; none where nothing does.</summary>
    public CountWatch? Watch { get; init; }
}

/// <summary>What Daoris does on a count once it stands at <see cref="At"/>: one of the rules' acts (<see cref="SupervisorRules"/>).</summary>
/// <param name="Place">The place of the person's rule that sets it; none where it is Daoris's own number.</param>
public sealed record CountWatch(int At, string Do, string? Place = null);

/// <summary>A part of a line, oldest first, and why it began: its record's <c>from</c> why, none for the line's first.</summary>
public sealed record LinePartFact(string Session, string? Why);

/// <summary>
/// One go-on of a line as its events show it (§5's recent course): when, in which part, what took it on (<see cref="GoOnBy"/>), and who
/// answered where answers did (<see cref="GoOnBy.Helper"/>, <see cref="GoOnBy.Opinion"/>, <see cref="GoOnBy.Supervisor"/>).
/// </summary>
public sealed record GoOnFact(DateTimeOffset At, string Session, string By, IReadOnlyList<string> Answered);

/// <summary>What took a line on, and who answered.</summary>
public static class GoOnBy
{
    public const string Alone = "alone";
    public const string Beside = "beside";
    public const string Person = "person";
    public const string Daoris = "daoris";
    public const string Passed = "passed";

    public const string Helper = "helper";
    public const string Opinion = "opinion";
    public const string Supervisor = "supervisor";
}

/// <summary>A line's counts, its parts and its last go-ons, as the course read them once.</summary>
public sealed record LineFacts(IReadOnlyList<CountFact> Counts, IReadOnlyList<LinePartFact> Parts, IReadOnlyList<GoOnFact> GoOns)
{
    public static LineFacts None { get; } = new([], [], []);

    /// <summary>The acts the person's rules made on the line's parts, oldest first (<see cref="SupervisorRuleActs"/>); none where no rule acted.</summary>
    public IReadOnlyList<RuleActed> Acts { get; init; } = [];
}

/// <summary>
/// Daoris's words for a line's counts (CONTEXTDRIVE1b, CONTEXTDRIVE1i; the context-counters design §3 point 4, §5): composed from codes and
/// numbers alone, each code worded from a closed list, so no agent's text, account or path reaches them. They say what was counted and
/// what Daoris does, never what the session must do.
/// </summary>
public static class CountTexts
{
    /// <summary>The brief's item, as its heading names it.</summary>
    public const string Heading = "The line's course, as Daoris counts it";

    /// <summary>The prefix a reason's source names a count by (§5): <c>count:answers-alone=2 (exact)</c>.</summary>
    public const string Cites = "count:";

    private const string HandOvers = "hand-overs";

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SupervisorRules.AnswersAlone] = "Answers alone in a row",
        [SupervisorRules.Failures] = "Failed sessions on this quest",
        [SupervisorRules.FailuresOnChoice] = "Failures in a row on one agent and model",
        [SupervisorRules.Quiet] = "Its last part ended quiet",
        [SupervisorRules.Limits] = "Its last part ended at a limit",
        [SupervisorRules.Moves] = "Moves to another choice in 24 hours",
        [HandOvers] = "Hand-overs on this line",
    };

    private static readonly IReadOnlyDictionary<string, string> Missing = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["walk-cut"] = "its walk back was cut",
        ["service-unanswered"] = "the service did not answer",
        ["log-trimmed"] = "its oldest events were trimmed",
        ["events-missing"] = "a part's events did not read",
        ["store-before-step"] = "the store is older than the count",
        ["store-unreadable"] = "the store did not read",
        ["no-record"] = "no record of its last part",
    };

    private static readonly IReadOnlyDictionary<string, string> Sources = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["admission"] = "the line's admission",
        ["events"] = "its events",
        ["records"] = "the service's records",
        ["ended.quiet"] = "its last part's end",
        ["role_moves"] = "the moves kept here",
        ["from-why"] = "its parts' links",
    };

    private static readonly IReadOnlyDictionary<string, string> Began = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [HandOverWhy.Press] = "handed over by the person",
        [HandOverWhy.Near] = "handed over near its context's end",
        [HandOverWhy.Limit] = "moved on at a limit",
        [HandOverWhy.Agent] = "handed over at its agent's ask",
        [HandOverWhy.Curve] = "moved on by its account's curve",
        [HandOverWhy.Resume] = "moved on from a lost conversation",
        [HandOverWhy.CarryOn] = "carried on after a cut-off",
        [HandOverWhy.Retry] = "retried",
    };

    /// <summary>
    /// The brief's lines (§3 point 1): one per count the line has that is not zero or that Daoris acts on, an at-least or unknown count
    /// saying so, in the order the counts were read. A count Daoris does not word is left out.
    /// </summary>
    public static IReadOnlyList<string> Lines(IReadOnlyList<CountFact> counts) =>
        [.. counts.Where(count => Labels.ContainsKey(count.Count) && (count.N is not 0 || count.Watch is not null)).Select(Line)];

    private static string Line(CountFact count) => $"{Labels[count.Count]}: {Value(count)}.{Watched(count)}";

    private static string Value(CountFact count) => count switch
    {
        { N: null } => $"cannot count, {WhyOf(count)}",
        { Known: "at-least", N: var n } => $"at least {Number(count, n!.Value)}, {WhyOf(count)}",
        { N: var n } => Number(count, n!.Value),
    };

    // A count of one end reads as whether it did.
    private static string Number(CountFact count, int n) =>
        count.Count is SupervisorRules.Quiet or SupervisorRules.Limits ? n > 0 ? "yes" : "no" : n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string WhyOf(CountFact count) => count.Why is { } why && Missing.TryGetValue(why, out var said) ? said : "part of its history is missing";

    private static string Watched(CountFact count) => count.Watch is { } watch && Does(watch.Do) is { } does
        ? watch.Place is { } place
            ? $" The person's rule for {PlaceOf(place)}: Daoris {does} after {watch.At}."
            : $" Daoris {does} after {watch.At}."
        : "";

    private static string? Does(string act) => act switch
    {
        SupervisorRules.Hold => "waits for the person",
        SupervisorRules.Ask => "asks the person before the next move",
        SupervisorRules.Wait => "waits for its reset",
        SupervisorRules.Next => "moves to the next choice",
        SupervisorRules.Weighted => "moves to the next choice by weight",
        SupervisorRules.Refer => "asks the supervisor",
        _ => null,
    };

    private static string PlaceOf(string place) =>
        place.StartsWith("line:", StringComparison.Ordinal) ? "this line"
        : place.StartsWith("repository:", StringComparison.Ordinal) ? "this repository"
        : place.StartsWith("workspace:", StringComparison.Ordinal) ? "this workspace"
        : "this machine";

    /// <summary>
    /// A go-on's one line (§3 point 2), or none: the go-ons by answers alone in a row, which every go-on moves, and each count Daoris acts on
    /// that stands at its number or one short of it, with what Daoris does next.
    /// </summary>
    public static string? GoOn(IReadOnlyList<CountFact> counts)
    {
        var said = counts.Where(count => Labels.ContainsKey(count.Count) && count.N is > 0
                && (count.Count == SupervisorRules.AnswersAlone || (count.Watch is { } watch && count.N >= watch.At - 1)))
            .Select(GoOnSentence).ToList();
        return said.Count == 0 ? null : "Daoris's count for this line: " + string.Join(" ", said);
    }

    private static string GoOnSentence(CountFact count)
    {
        var n = count.N!.Value;
        var many = n == 1 ? "" : "s";
        var least = count.Known == "at-least" ? "at least " : "";
        var phrase = count.Count switch
        {
            SupervisorRules.AnswersAlone => $"{least}{n} go-on{many} by answers alone in a row",
            SupervisorRules.Failures => $"{least}{n} failed session{many} on this quest",
            SupervisorRules.FailuresOnChoice => $"{least}{n} failure{many} in a row on one agent and model",
            SupervisorRules.Quiet => "its last part ended quiet",
            SupervisorRules.Limits => "its last part ended at a limit",
            SupervisorRules.Moves => $"{least}{n} move{many} to another choice in 24 hours",
            _ => $"{least}{n} hand-over{many}",
        };
        if (count.Watch is not { } watch || Does(watch.Do) is not { } does) return phrase + ".";
        return n >= watch.At
            ? watch.Do == SupervisorRules.Hold ? $"{phrase}; the next waits for the person." : $"{phrase}; at the next, Daoris {does}."
            : $"{phrase}; Daoris {does} after {watch.At}.";
    }

    /// <summary>How a reason's source names <paramref name="count"/> (§5): its code, what it stands at and how it is known.</summary>
    public static string Cite(CountFact count) =>
        count.N is { } n ? $"{Cites}{count.Count}={n} ({count.Known})" : $"{Cites}{count.Count} ({count.Known})";

    private static readonly Regex Cited = new(@"^count:(?<count>[a-z][a-z.-]*)(=(?<n>\d{1,9}))?(\s*\((?<known>[a-z-]+)\))?", RegexOptions.CultureInvariant);

    /// <summary>
    /// Whether a reason's <paramref name="source"/> names a count, and if so whether it names one of <paramref name="handed"/>: its code, and
    /// what it stood at and how it was known where the source says them. A source that does not name a count is none of this check's.
    /// </summary>
    public static bool NamesNone(string source, IReadOnlyList<CountFact>? handed)
    {
        var said = source.Trim();
        if (!said.StartsWith(Cites, StringComparison.Ordinal)) return false;
        if (Cited.Match(said) is not { Success: true } match || handed is null) return true;
        var n = match.Groups["n"] is { Success: true } number ? int.Parse(number.Value, System.Globalization.CultureInfo.InvariantCulture) : (int?)null;
        var known = match.Groups["known"] is { Success: true } state ? state.Value : null;
        return !handed.Any(count => count.Count == match.Groups["count"].Value && (n is null || count.N == n) && (known is null || count.Known == known));
    }

    /// <summary>
    /// The supervisor's part (§5): every count with how it is known and its source, cited as a reason names it, what Daoris does on each
    /// and whose that is, the line's parts with why each began, and its last go-ons with who answered. Daoris's facts, from its records.
    /// </summary>
    public static string Print(LineFacts facts)
    {
        var text = new StringBuilder();
        text.Append("Every count of this line as Daoris read it; a reason names one by its first words here.\n");
        var worded = facts.Counts.Where(count => Labels.ContainsKey(count.Count)).ToList();
        if (worded.Count == 0) text.Append("- none: this line has no record here\n");
        foreach (var count in worded)
        {
            text.Append($"- {Cite(count)}: {Labels[count.Count].ToLowerInvariant()}, from {(Sources.TryGetValue(count.From, out var from) ? from : "its records")}");
            if (count.Known != "exact") text.Append($"; {WhyOf(count)}");
            if (count.Watch is { } watch && Does(watch.Do) is { } does)
            {
                text.Append(watch.Place is { } place ? $"; the person's rule for {PlaceOf(place)}: Daoris {does} after {watch.At}" : $"; Daoris's own number: it {does} after {watch.At}");
            }

            text.Append('\n');
        }

        text.Append(worded.Any(count => count.Watch?.Place is not null)
            ? "The rules in force on this line are the person's, named beside each count.\n"
            : "No rule of the person's is in force on this line: Daoris acts on its own numbers, named beside each count.\n");
        text.Append("Its parts, oldest first:\n");
        foreach (var part in facts.Parts)
        {
            text.Append($"- session {part.Session}: {(part.Why is null ? "opened" : Began.TryGetValue(part.Why, out var began) ? began : "continued another")}\n");
        }

        if (facts.Parts.Count == 0) text.Append("- none read\n");
        text.Append("Its last go-ons, oldest first:\n");
        foreach (var goOn in facts.GoOns)
        {
            text.Append($"- {goOn.At.UtcDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)} UTC, session {goOn.Session}: {Took(goOn)}\n");
        }

        if (facts.GoOns.Count == 0) text.Append("- none read\n");
        if (facts.Acts.Count > 0)
        {
            text.Append("Its rules' acts, oldest first:\n");
            foreach (var act in facts.Acts.OrderBy(each => each.At))
            {
                var label = Labels.TryGetValue(act.Rule.Count, out var counted) ? counted.ToLowerInvariant() : act.Rule.Count;
                text.Append($"- {act.At.UtcDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)} UTC, session {act.Session}: "
                    + $"the person's rule for {PlaceOf(act.Rule.Place)}: {label} stood at {act.N}, and Daoris {Does(act.Rule.Do) ?? "acted"} after {act.Rule.At}\n");
            }
        }

        return text.ToString().TrimEnd('\n');
    }

    private static string Took(GoOnFact goOn) => goOn.By switch
    {
        GoOnBy.Alone => $"by answers alone: {Answered(goOn.Answered)}",
        GoOnBy.Beside => $"{Answered(goOn.Answered)}, beside a word of the person's or Daoris's",
        GoOnBy.Daoris => "Daoris's word",
        GoOnBy.Passed => "the person's Go on with this answer",
        GoOnBy.Person => "the person's word",
        _ => "a word",
    };

    private static string Answered(IReadOnlyList<string> by)
    {
        var said = by.Select(each => each switch
        {
            GoOnBy.Helper => "a helper's answer",
            GoOnBy.Opinion => "a second opinion's findings",
            GoOnBy.Supervisor => "the supervisor's answer",
            _ => null,
        }).OfType<string>().Distinct().ToList();
        return said.Count == 0 ? "answers" : string.Join(" and ", said);
    }
}
