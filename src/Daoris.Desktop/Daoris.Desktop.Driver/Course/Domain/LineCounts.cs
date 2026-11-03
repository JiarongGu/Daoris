namespace Daoris.Driver;

/// <summary>One count of a line with the state it is known in (the context-counters design §2.1, §2.4).</summary>
/// <param name="Count">The count's code (<see cref="LineCounts"/>'s constants).</param>
/// <param name="Of">Whose it is: <c>line</c> or <c>quest</c>.</param>
/// <param name="Line">The line's key (<see cref="LineKeys.Of"/>).</param>
/// <param name="Window">The window the count offers.</param>
/// <param name="N">The count; null where <see cref="Known"/> is <see cref="LineCounts.Unknown"/>.</param>
/// <param name="Known"><see cref="LineCounts.Exact"/>, <see cref="LineCounts.AtLeast"/> or <see cref="LineCounts.Unknown"/>.</param>
/// <param name="Why">What is missing, for at least and unknown; null for an exact count.</param>
/// <param name="From">Its one source.</param>
public sealed record LineCount(string Count, string Of, string Line, string Window, int? N, string Known, string? Why, string From);

/// <summary>A session's events as one log read them, and whether the log's start is gone (D195's retention).</summary>
public sealed record PartEvents(IReadOnlyList<SessionEvent> Events, bool Trimmed);

/// <summary>The line's moves in 24 hours as the store answered them: <see cref="Read"/>, <see cref="BeforeStep"/> or <see cref="Unreadable"/>.</summary>
public sealed record MovesAnswer(int N, string State)
{
    /// <summary>The store holds the table and answered.</summary>
    public const string Read = "read";

    /// <summary>No store, or one before the step that keeps <c>role_moves</c>: it counts nothing, so the count is a lower bound.</summary>
    public const string BeforeStep = "before-step";

    /// <summary>A store that does not read.</summary>
    public const string Unreadable = "unreadable";
}

/// <summary>Everything a line's counts are read from, read once (<c>LineCountsRead</c>).</summary>
/// <param name="Records">The service's records by id; null where it did not answer.</param>
/// <param name="Events">The events of the line's parts by session; a part absent or null here is one whose log did not read.</param>
/// <param name="Strikes"><see cref="ServiceClient"/>'s strikes by quest: its one count of failures.</param>
/// <param name="FailedRuns">Its failures in a row on one choice by quest.</param>
/// <param name="Forgiven">The failures each quest's <i>Try again</i> marked forgiven.</param>
/// <param name="Moves">The line's moves, as the store answered for its key.</param>
/// <param name="Alone">The line's answers-alone count as its admission keeps it (COMMS1g); null where none is kept, so the events count it.</param>
public sealed record LineEvidence(
    IReadOnlyDictionary<string, PriorSession>? Records,
    IReadOnlyDictionary<string, PartEvents?> Events,
    IReadOnlyDictionary<string, int> Strikes,
    IReadOnlyDictionary<string, int> FailedRuns,
    IReadOnlyDictionary<string, int> Forgiven,
    MovesAnswer Moves,
    AloneCount? Alone = null);

/// <summary>
/// Every count of a line, from what was read once (CONTEXTDRIVE1a, the context-counters design §2.2): pure, and nothing stored beside the
/// record each count comes from. A count is <i>exact</i>, <i>at least</i> where history before a point is missing (a trimmed log, a walk
/// cut by a cycle or the bound, a store before its step) and <i>unknown</i> where its source does not answer. A count a line does not have is
/// absent: a conversation has no quest to fail. A teammate's record is on no line here, so it has no counts.
/// </summary>
public static class LineCounts
{
    public const string AnswersAlone = "answers-alone";
    public const string Failures = "failures";
    public const string FailuresOnChoice = "failures-on-choice";
    public const string Quiet = "quiet";
    public const string Limits = "limits";
    public const string Moves = "moves";
    public const string HandOvers = "hand-overs";

    public const string Exact = "exact";
    public const string AtLeast = "at-least";
    public const string Unknown = "unknown";

    public const string WalkCut = "walk-cut";
    public const string ServiceUnanswered = "service-unanswered";
    public const string LogTrimmed = "log-trimmed";
    public const string EventsMissing = "events-missing";
    public const string StoreBeforeStep = "store-before-step";
    public const string StoreUnreadable = "store-unreadable";
    public const string NoRecord = "no-record";

    /// <summary>The reasons a part was handed on by another agent's name; a carry-on, a resume, a retry and a policy's move are not hand-overs.</summary>
    private static readonly string[] HandedWhy = [HandOverWhy.Press, HandOverWhy.Near, HandOverWhy.Limit, HandOverWhy.Agent, HandOverWhy.Curve];

    /// <summary>Every count of the line <paramref name="session"/> is on.</summary>
    public static IReadOnlyList<LineCount> Of(string session, LineEvidence evidence)
    {
        var records = evidence.Records ?? new Dictionary<string, PriorSession>(StringComparer.Ordinal);
        var key = LineKeys.Of(session, records);
        if (key.Parts.Count == 0) return [];

        var members = LineKeys.Members(key, records);
        var cut = key.Cut is not null;
        var driven = evidence.Records is not null && members.Any(member => member.Kind == "driven" && member.Quest == key.Key);
        var counts = new List<LineCount> { AnswersAloneOf(key, evidence.Records is null ? key.Parts : [.. members.Select(member => member.Session)], evidence) };

        // Without the records a line's kind is not known, so each count that comes from them is unknown, a conversation's included.
        if (evidence.Records is null || driven)
        {
            counts.Add(FromRecords(Failures, "quest", key, "since-try-again", "records", evidence, () =>
                Math.Max(0, evidence.Strikes.GetValueOrDefault(key.Key) - evidence.Forgiven.GetValueOrDefault(key.Key)), atLeast: null));
            counts.Add(FromRecords(FailuresOnChoice, "line", key, "in-a-row-one-choice", "records", evidence, () =>
                evidence.FailedRuns.GetValueOrDefault(key.Key), atLeast: null));
        }

        var newest = members.LastOrDefault();
        var atLeast = cut ? WalkCut : newest is null ? NoRecord : null;
        counts.Add(FromRecords(Quiet, "line", key, "this-end", "ended.quiet", evidence, () =>
            newest?.NoteParts?.Any(part => part.Code == NoteCodes.EndedQuiet.Code) == true ? 1 : 0, atLeast));
        counts.Add(FromRecords(Limits, "line", key, "this-end", "records", evidence, () => newest?.Limit == true ? 1 : 0, atLeast));
        counts.Add(MovesOf(key, evidence.Moves));
        counts.Add(FromRecords(HandOvers, "line", key, "the-line", "from-why", evidence, () =>
            members.Count(member => member.FromWhy is { } why && HandedWhy.Contains(why)), cut ? WalkCut : null));
        return counts;
    }

    private static LineCount AnswersAloneOf(LineKey key, IReadOnlyList<string> parts, LineEvidence evidence)
    {
        if (evidence.Alone is { } kept) return new LineCount(AnswersAlone, "line", key.Key, "in-a-row", kept.N, kept.Known, kept.Why, kept.From);

        // Until the line's admission is kept: oldest part first; a part whose log does not read ends the line before it, as the bound's own reader leaves it.
        var line = new List<SessionEvent>();
        var trimmed = false;
        var missing = false;
        foreach (var id in parts)
        {
            if (evidence.Events.GetValueOrDefault(id) is not { } part)
            {
                line.Clear();
                missing = true;
                continue;
            }

            trimmed |= part.Trimmed;
            line.AddRange(part.Events);
        }

        var why = key.Cut is not null ? WalkCut
            : evidence.Records is null ? ServiceUnanswered
            : trimmed ? LogTrimmed
            : missing ? EventsMissing
            : null;
        return new LineCount(AnswersAlone, "line", key.Key, "in-a-row", global::Daoris.Driver.AnswersAlone.Count(line), why is null ? Exact : AtLeast, why, "events");
    }

    private static LineCount FromRecords(
        string count, string of, LineKey key, string window, string from, LineEvidence evidence, Func<int> read, string? atLeast)
    {
        if (evidence.Records is null) return new LineCount(count, of, key.Key, window, null, Unknown, ServiceUnanswered, from);
        return new LineCount(count, of, key.Key, window, read(), atLeast is null ? Exact : AtLeast, atLeast, from);
    }

    private static LineCount MovesOf(LineKey key, MovesAnswer moves) => moves.State switch
    {
        MovesAnswer.Read => new LineCount(Moves, "line", key.Key, "24-hours", moves.N, Exact, null, "role_moves"),
        MovesAnswer.BeforeStep => new LineCount(Moves, "line", key.Key, "24-hours", moves.N, AtLeast, StoreBeforeStep, "role_moves"),
        _ => new LineCount(Moves, "line", key.Key, "24-hours", null, Unknown, StoreUnreadable, "role_moves"),
    };

    /// <summary>The most go-ons the supervisor's print names (the context-counters design §5): the line's last ten.</summary>
    public const int GoOnsShown = 10;

    /// <summary>
    /// The counts as facts below the course (CONTEXTDRIVE1b, CONTEXTDRIVE1i), each with the number Daoris acts on it at today: the
    /// answers-alone bound, and the home's failed sessions, none where it is 0.
    /// </summary>
    public static IReadOnlyList<CountFact> Facts(IReadOnlyList<LineCount> counts, int strikes) =>
    [
        .. counts.Select(count => new CountFact(count.Count, count.N, count.Known, count.Why, count.From)
        {
            Watch = count.Count switch
            {
                AnswersAlone => new CountWatch(global::Daoris.Driver.AnswersAlone.Bound, SupervisorRules.Hold),
                Failures when strikes > 0 => new CountWatch(strikes, SupervisorRules.Hold),
                _ => null,
            },
        }),
    ];

    /// <summary>The line <paramref name="session"/> is on, oldest part first, each with why it began; the part alone where the records did not read.</summary>
    public static IReadOnlyList<LinePartFact> Parts(string session, LineEvidence evidence)
    {
        if (evidence.Records is not { } records) return SessionEventText.IsLocalId(session) ? [new LinePartFact(session, null)] : [];
        var key = LineKeys.Of(session, records);
        return [.. LineKeys.Members(key, records).Select(member => new LinePartFact(member.Session, member.From is { Length: > 0 } ? member.FromWhy : null))];
    }

    /// <summary>
    /// The line's last <see cref="GoOnsShown"/> go-ons, oldest first, as its events show them (the context-counters design §5): each prompt
    /// its agent acted on by answers alone, or beside a word, and each word of the person's or Daoris's, with who answered.
    /// </summary>
    public static IReadOnlyList<GoOnFact> GoOns(string session, LineEvidence evidence)
    {
        IEnumerable<string> parts = evidence.Records is { } records
            ? LineKeys.Members(LineKeys.Of(session, records), records).Select(member => member.Session)
            : [session];
        var line = parts.SelectMany(part => (evidence.Events.GetValueOrDefault(part)?.Events ?? []).Select(e => (Part: part, Event: e))).ToList();
        var steps = global::Daoris.Driver.AnswersAlone.Read(AloneScan.Start, line).Steps.TakeLast(GoOnsShown);
        return [.. steps.Select(step => new GoOnFact(step.At, step.Part, By(step, line), [.. Answered(step, line)]))];
    }

    private static string By(AloneStep step, IReadOnlyList<(string Part, SessionEvent Event)> line)
    {
        if (step.Kind == AloneStep.GoOn) return GoOnBy.Alone;
        if (step.Kind == AloneStep.Beside) return GoOnBy.Beside;
        var word = line.FirstOrDefault(each => each.Part == step.Part && each.Event.Seq == step.Seq).Event;
        return word?.Kind == SessionEventKind.Note ? GoOnBy.Passed : word?.From == DaorisWords.From ? GoOnBy.Daoris : GoOnBy.Person;
    }

    private static IEnumerable<string> Answered(AloneStep step, IReadOnlyList<(string Part, SessionEvent Event)> line) =>
        line.Where(each => each.Event.Id is { } id && step.Words.Contains(id, StringComparer.Ordinal))
            .Select(each => each.Event.Helper is not null ? GoOnBy.Helper
                : each.Event.Opinion is not null ? GoOnBy.Opinion
                : each.Event.From == SupervisorHanded.From ? GoOnBy.Supervisor
                : null)
            .OfType<string>().Distinct(StringComparer.Ordinal);
}
