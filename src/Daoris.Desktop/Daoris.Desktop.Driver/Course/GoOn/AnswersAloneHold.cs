namespace Daoris.Driver;

/// <summary>
/// The answers-alone bound held where a line would go on (COWORK1e, D188 point 4; the message-urgency design §2.3, §8.3, trap 11): the
/// go-on judged inside its line's admission (COMMS1g, <see cref="ContinuationAdmissions"/>), keyed by the line's one key over this
/// machine's records, and where the answers waiting would take it on a third time in a row with nobody between, each marked
/// <see cref="GoOnMarks.AnswersAlone"/> with the agent that answered and the hold said once in its conversation. Every look then leaves
/// them waiting (<see cref="GoOnMarks.Judged"/>), and the person's wait is listed from the mark.
/// </summary>
/// <remarks>
/// <para><b>Judged where each path goes on</b>, by the session's one dispatcher there: the driver's look before it plans
/// (<see cref="LookAsync"/>), an ended conversation's go-on before its mark is cleared (<see cref="ChatGoOn"/>), and an open
/// conversation's turns as a helper's answer's turn comes (<see cref="ChatTurns"/>). Each reserves the go-on it admits in the line's
/// admission, so two answers queued together, on one path or two, never both pass as the second. The continuation's own files stay as
/// they were (SESSCTX1c): the hold is a mark beside them.</para>
///
/// <para><b>A line is counted by its admissions alone</b> (D232), from its seed on: an event trimmed or lost afterwards takes nothing from
/// it. Where the service's records did not read, or the store holds no admissions (a start before its confirmation settled), nothing is
/// held, and the next look judges again.</para>
/// </remarks>
public sealed class AnswersAloneHold(
    SessionEvents events, GoOnMarks marks, ContinuationAdmissions admissions, SupervisorRuleBook? rules = null, SupervisorRuleActs? acts = null)
{
    /// <summary>The hold of <paramref name="home"/>, judged by the rules its store holds now, each hold kept as its rule's act.</summary>
    public static AnswersAloneHold For(SessionEvents events, GoOnMarks marks, string home) =>
        new(events, marks, new ContinuationAdmissions(home), SupervisorRuleBook.Read(home), new SupervisorRuleActs(home, events));

    /// <summary>
    /// The answers alone in a row <paramref name="session"/>'s line goes on by: its line's effective rule, null where none holds it. A hold
    /// handed no rules reads the seeded ones.
    /// </summary>
    public int? At(string session, IReadOnlyDictionary<string, PriorSession> records)
    {
        var book = rules ?? SupervisorRuleBook.Seed;
        var record = records.GetValueOrDefault(session);
        return SupervisorRuleBook.AnswersAloneAt(book.For(SupervisorRuleBook.RoleOf(record), record?.Repository, record?.Workspace, LineKeys.Of(session, records).Key));
    }

    /// <summary>The number a held line was held at: its rule's.</summary>
    public int HeldAt(string session, IReadOnlyDictionary<string, PriorSession> records) => At(session, records) ?? AnswersAlone.Bound;

    /// <summary>
    /// Whether going on with <paramref name="words"/> on <paramref name="record"/> now would take its line on by answers alone past the
    /// bound; where it would, every answer waiting on it is marked (once, with its note), and true. Where it would not, the go-on is
    /// reserved in the line's admission.
    /// </summary>
    /// <param name="records">The service's records by id, for the line's parts and the agent that answered; none read, the record alone.</param>
    /// <param name="words">The words going on now: the record's waiting words where null, or the one answer an open conversation's turn takes.</param>
    public bool Holds(
        PriorSession record, IReadOnlyDictionary<string, PriorSession> records, DateTimeOffset at, IReadOnlyList<SaidWordView>? words = null)
    {
        words ??= record.Waiting;
        if (record.Teammate || !SessionEventText.IsLocalId(record.Session) || words.Count == 0 || !words.All(AnswersAlone.Solicited)) return false;
        if (Admits(record, records, words)) return false;

        // Every answer waiting on it, those handed under another ask included: going on with any of them alone would be the same third.
        var answers = record.Waiting.Concat(words).Where(AnswersAlone.Solicited).Select(word => word.Id).OfType<string>()
            .Distinct(StringComparer.Ordinal).ToList();
        if (answers.Count == 0) return true;
        if (marks.Read(record.Session) is { Why: GoOnMarks.AnswersAlone } held && answers.All(id => held.Said.Contains(id, StringComparer.Ordinal)))
        {
            return true;
        }

        var agent = AgentOf(words, records);
        marks.Mark(record.Session, answers, GoOnMarks.AnswersAlone, at, agent);
        events.Keep(record.Session, AnswersAloneNotes.Held(agent, answers), say: null);
        Acted(record, records, at);
        return true;
    }

    // The hold is the answers-alone rule's act: said on the line by its rule and kept in the decision record (the context counters design §4.7).
    private void Acted(PriorSession record, IReadOnlyDictionary<string, PriorSession> records, DateTimeOffset at)
    {
        if (acts is null) return;
        var book = rules ?? SupervisorRuleBook.Seed;
        var line = LineKeys.Of(record.Session, records).Key;
        var effective = book.For(SupervisorRuleBook.RoleOf(records.GetValueOrDefault(record.Session) ?? record), record.Repository, record.Workspace, line);
        if (SupervisorRuleBook.Acting(effective, SupervisorRules.AnswersAlone, SupervisorRules.Hold) is not { } rule) return;
        acts.Record(new RuleActed(line, record.Session, rule, book.RevisionOf(rule.Place), rule.At, Known: true, "go-on", at));
    }

    /// <summary>
    /// The driver's look (COWORK1e): each record whose words waiting are answers alone and not already left waiting is judged before the
    /// planner reads the marks, so it leaves a held one waiting. The service's records are read once, and only where such a record waits.
    /// </summary>
    /// <returns>How many records it held this look.</returns>
    public async Task<int> LookAsync(ServiceClient service, IEnumerable<PriorSession> looked, DateTimeOffset at, CancellationToken ct)
    {
        var candidates = looked
            .Where(record => record.WordsWaiting && !record.Teammate && record.Waiting.All(AnswersAlone.Solicited)
                             && !GoOnMarks.Judged(record, marks.Read(record.Session)))
            .DistinctBy(record => record.Session, StringComparer.Ordinal)
            .ToList();
        if (candidates.Count == 0) return 0;

        var records = await RecordsAsync(service, ct).ConfigureAwait(false);
        return candidates.Count(record => Holds(record, records, at));
    }

    /// <summary>
    /// The service's records by id, for a line's parts; unread, none, so each line is its record alone, and a line that went on across a
    /// hand-over is judged again at the next look.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, PriorSession>> RecordsAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return ServiceClient.ReadRecords(await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return new Dictionary<string, PriorSession>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Whether <paramref name="session"/>'s line is at the bound now (SUPERVISOR1b, the supervisor design §4.3): the supervisor's route
    /// asks it at the keeping and its release asks it again, so a supervisor's word is never kept only to be held behind the bound. It
    /// reads the count the hold judges, its admission's; a record not this machine's counts nothing.
    /// </summary>
    public bool AtBound(string session, IReadOnlyDictionary<string, PriorSession> records)
    {
        if (!SessionEventText.IsLocalId(session)) return false;
        return records.ContainsKey(session) && admissions.Count(History(session, records)) is { } count && At(session, records) is { } at && count.N >= at;
    }

    /// <summary>
    /// A waited quest's result counts (the comms design §11): D79's resume of <paramref name="prior"/>'s quest on <paramref name="quest"/>'s
    /// answer is reserved on its line once, beside the words it hands, and held by nothing here. A conversation never waits on a quest
    /// (COWORKVIEW1 R11.2 point 5), so nothing counts on its line, nor does a go-on beside a word of the person's, which resets it.
    /// </summary>
    /// <returns>The line's count with it; null where nothing was reserved or the store holds no admissions.</returns>
    public AloneCount? Waited(PriorSession prior, string quest, IReadOnlyList<SaidWordView> handed, IReadOnlyDictionary<string, PriorSession> records)
    {
        if (prior.Kind != "driven" || prior.Teammate || !SessionEventText.IsLocalId(prior.Session) || !handed.All(AnswersAlone.Solicited)) return null;
        return admissions.Admit(History(prior.Session, records), $"quest:{quest}", Keys(prior, handed), prior.Session, at: null)?.Count;
    }

    /// <summary>The history of the line <paramref name="session"/> is on, its parts' events as this machine keeps them.</summary>
    public LineHistory History(string session, IReadOnlyDictionary<string, PriorSession> records) =>
        ContinuationAdmissions.History(session, records, part => LineCountsRead.EventsOf(events, part));

    /// <summary>
    /// Whether the line admits the go-on now: judged and reserved inside its admission at <see cref="AnswersAlone.Bound"/>, the line read
    /// by its one key. Without the service's records, or without the admissions, it is admitted: nothing counts it.
    /// </summary>
    private bool Admits(PriorSession record, IReadOnlyDictionary<string, PriorSession> records, IReadOnlyList<SaidWordView> words)
    {
        var ids = Keys(record, words);
        return !records.ContainsKey(record.Session)
            || admissions.Admit(History(record.Session, records), "answers:" + string.Join(",", ids.Order(StringComparer.Ordinal)), ids, record.Session, At(record.Session, records)) is not { } admission
            || admission.Admitted;
    }

    /// <summary>Each word's id, and for a word from before ids its record and when it was said.</summary>
    private static IReadOnlyList<string> Keys(PriorSession record, IReadOnlyList<SaidWordView> words) =>
        [.. words.Select(word => word.Id ?? $"{record.Session}@{word.At.ToUniversalTime():O}").Distinct(StringComparer.Ordinal)];


    /// <summary>
    /// The agent whose answer is held, by its adapter: a helper's record's, else the reviewer's whose opinion the findings are; else the
    /// helper's or the opinion's own id, a fact the wait names where no record says the agent.
    /// </summary>
    private static string AgentOf(IReadOnlyList<SaidWordView> words, IReadOnlyDictionary<string, PriorSession> records)
    {
        foreach (var word in words)
        {
            if (word.Helper is { } helper && records.TryGetValue(helper, out var helped) && helped.Adapter is { Length: > 0 } adapter) return adapter;
            if (word.By is { } opinion
                && records.Values.FirstOrDefault(each => string.Equals(each.Opinion, opinion, StringComparison.Ordinal))?.Adapter is { Length: > 0 } reviewer)
            {
                return reviewer;
            }
        }

        return words.Select(word => word.Helper ?? word.By).OfType<string>().FirstOrDefault() ?? "another agent";
    }
}

/// <summary>
/// The person's answer to an answers-alone wait (COWORK1e; the message-urgency design §8.3), shared by the page's route: *Go on with this
/// answer* and *Not now*, each only where the bound holds the record's answers for them.
/// </summary>
public static class AnswersAloneWord
{
    /// <summary>No answer of the session waits under the bound: none was held, the person answered already, or its words went on.</summary>
    public const string Unheld = "unheld";

    /// <summary>
    /// *Go on with this answer*: the press kept in the conversation first, as the person's word the count reads (the steward's choice 1),
    /// then the mark taken away, so the next look, or the conversation's own go-on, takes the answer on. A look between the two still
    /// finds the answer marked.
    /// </summary>
    public static AloneAnswer GoOn(GoOnMarks marks, SessionEvents events, string session)
    {
        if (marks.Read(session) is not { Why: GoOnMarks.AnswersAlone } held) return Refused();
        events.Keep(session, AnswersAloneNotes.Passed(held.Said), say: null);
        marks.Clear(session);
        return new AloneAnswer(true, null, $"Session {session}'s answer goes on: you let it.");
    }

    /// <summary>
    /// *Not now*: the answer is the person's, as an answer nobody takes is handed (§8.3). It stays on the record, marked
    /// <see cref="GoOnMarks.NotNow"/>, so nothing takes it on by itself and the wait is no longer listed; a word of the person's said
    /// later is tried unmarked, and the answer goes beside it. Findings among it go to the person at the opinion's next look.
    /// </summary>
    public static AloneAnswer NotNow(GoOnMarks marks, SessionEvents events, string session, DateTimeOffset at)
    {
        if (marks.Read(session) is not { Why: GoOnMarks.AnswersAlone } held) return Refused();
        marks.Mark(session, held.Said, GoOnMarks.NotNow, at, held.Agent);
        events.Keep(session, AnswersAloneNotes.Kept(held.Said), say: null);
        return new AloneAnswer(true, null, $"Session {session}'s answer is yours: it goes on only with your words.");
    }

    private static AloneAnswer Refused() =>
        new(false, Unheld, "No answer of this session waits for you: none was held, it was answered already, or its words went on.");
}
