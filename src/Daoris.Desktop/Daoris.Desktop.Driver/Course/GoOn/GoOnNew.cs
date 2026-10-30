namespace Daoris.Driver;

/// <summary>
/// The choices the person made to go on in a new session rather than wait for a cooling account (MSG1g, D137 §2.2):
/// <c>&lt;home&gt;/sessions/&lt;session&gt;.new-session.json</c>, beside the session's marks and conversation id. The person's
/// choice, kept with when it was given (D143 point 4), which the driver's next look reads.
/// </summary>
/// <remarks>
/// <para><b>By the words' ids</b>, as <see cref="GoOnMarks"/> keeps its marks: a choice covers its record while any word it
/// named still waits, and a word said since goes with them, since a carry-on is handed every word waiting. Once they are
/// taken it covers nothing, and a word said later waits for the account again.</para>
///
/// <para><b>This machine's, never the record's.</b> A file that does not read is no choice: the words wait for the account,
/// which loses nothing.</para>
///
/// <para>What is kept is this store's; the file and its format are <see cref="GoOnFiles"/>' (STRUCT3h, D166's note).</para>
/// </remarks>
public sealed class NewSessionChoices(string home)
{
    public const string Suffix = SessionHomeFiles.NewSessionChoiceSuffix;

    /// <summary>Where a session's choice is kept.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>
    /// Keep the choice for these words, replacing any before. An id that is not a session's keeps nothing.
    /// </summary>
    /// <exception cref="IOException">The choice could not be written: the person is told, rather than told it was kept.</exception>
    public void Choose(string session, IEnumerable<string> said, DateTimeOffset at)
    {
        if (!SessionEventText.IsLocalId(session)) return;
        GoOnFiles.WriteChoice(PathOf(session), [.. said.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)], at);
    }

    /// <summary>The choice kept for this session, or null for none, a file that does not read, or an id that is not one.</summary>
    public NewSessionChoice? Read(string session) => SessionEventText.IsLocalId(session) ? GoOnFiles.ReadChoice(PathOf(session)) : null;

    /// <summary>Take a session's choice away: its words went on, or were taken. Nothing to take is no failure.</summary>
    public void Clear(string session)
    {
        // A choice left behind covers only words it names, and those are taken.
        if (SessionEventText.IsLocalId(session)) GoOnFiles.Delete(PathOf(session));
    }

    /// <summary>
    /// Whether the person chose a new session for the words waiting on this record: any word the choice named still waits. A
    /// choice that named none, words kept by a host from before ids, covers whatever waits.
    /// </summary>
    public bool Covers(PriorSession record) =>
        Read(record.Session) is { } choice
        && record.Waiting is { Count: > 0 } waiting
        && (choice.Said.Count == 0 || waiting.Any(word => word.Id is { } id && choice.Said.Contains(id, StringComparer.Ordinal)));
}

/// <summary>
/// *Go on in a new session* (MSG1g, D137 §2.2's account paragraph): words a resume holds while the account their session ran on
/// cools go on now in a new session, handed them, without that conversation. One door for the screen's route and the terminal's
/// <c>daoris-driver sessions go-on-new</c> (D50): it keeps the person's choice (<see cref="NewSessionChoices"/>), and the
/// driver's next look carries the words on as it carries on words whose account cannot run there.
/// </summary>
/// <remarks>
/// <para><b>Refused before anything is kept</b>: D137 §2.2's nevers by <see cref="WordsNever"/>, a session still running, one
/// with nothing waiting, and what nothing carries on by itself, a conversation and a closed quest's session, which are pointed
/// at *Start a conversation with these words*; and words whose account is not cooling, which the same session goes on with at
/// the next look, better than any new one.</para>
/// </remarks>
public static class GoOnNew
{
    /// <summary>Nothing waits on the record to go on with.</summary>
    public const string NoWords = "no-words";

    /// <summary>The session runs: words reach it at its door (D136), and it needs no new session.</summary>
    public const string Running = "running";

    /// <summary>A conversation, which nothing carries on by itself (D137 §2.2): *Start a conversation with these words* is the door.</summary>
    public const string Conversation = "conversation";

    /// <summary>A closed quest's session, which nothing carries on by itself either.</summary>
    public const string Closed = "closed";

    /// <summary>Its account is not cooling, so the same session goes on with the words at the next look.</summary>
    public const string NotCooling = "not-cooling";

    /// <summary>Judge and keep the choice for a session of this machine's.</summary>
    /// <param name="coolingOf">The cool-off a start on an adapter would read for an account, or null where it is ready.</param>
    public static Task<GoOnNewAnswer> AskAsync(
        ServiceClient service, string home, Func<string?, string?, CoolingEntry?> coolingOf, string id, DateTimeOffset now,
        CancellationToken ct = default) =>
        AskAsync(service, home, coolingOf, id, now, null, ct);

    /// <summary>
    /// FOUNDATION1h branch 2 (the foundation design §3.1): the course's one decision for <see cref="CourseTrigger.GoOnNew"/> says whether the
    /// words may go on in a new session; this reads the facts it decides over and says its answer in the door's own codes and words, so
    /// nothing a person reads changes. <paramref name="decided"/> hears what the course answered: a test's, no person's.
    /// </summary>
    internal static async Task<GoOnNewAnswer> AskAsync(
        ServiceClient service, string home, Func<string?, string?, CoolingEntry?> coolingOf, string id, DateTimeOffset now,
        Action<LineCourse>? decided, CancellationToken ct = default)
    {
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) return Refused(WordsNever.NotFound, $"no session here is {id}.");

        var last = WordsNever.LastHere(json, record.Quest);
        var next = WordsNever.ContinuedIn(json, id);
        var prior = ServiceClient.ReadRecord(json, id);
        var judged = WordsNever.Judge(record, last, next);

        // The facts, read lazily as the door's refusals were: a driven record's quest is read only where nothing before it refused, and the
        // account only where its quest is open or taken. The first pass assumes an open quest and no cooling, so its answer says whether a
        // later fact could change it (the not-cooling refusal is the one that reached them).
        var facts = FactsOf(record, prior, judged, last, next, record.Quest is null || record.Kind == "chat" ? null : "Open", null);
        var course = CourseDecision.Decide(facts, CourseTrigger.GoOnNew);
        if (course is LineCourse.Hold { Code: NotCooling } && facts.Head.QuestStatus is not null)
        {
            // Words the course would hold only for an account's cool-off: whether the quest has closed, and whether the account cools.
            var quest = await service.FindQuestAsync(record.Quest!, ct).ConfigureAwait(false);
            var status = quest?.Status ?? "missing";
            facts = FactsOf(record, prior, judged, last, next, status, status is "Open" or "Taken" ? CoolingFact(coolingOf, prior!) : null);
            course = CourseDecision.Decide(facts, CourseTrigger.GoOnNew);
        }

        decided?.Invoke(course);

        // Only a refusal says why; whatever else the course answers (a wait for the cooling account, or a move to another) keeps the choice.
        switch (course)
        {
            case LineCourse.Never never:
                return never.Why switch
                {
                    CourseNeverWhy.Teammate => Refused(WordsNever.Teammate, $"{id} ran on {id[..id.IndexOf('/')]}, where its conversation is; nothing said here reaches it."),
                    CourseNeverWhy.Intake => Refused(WordsNever.Intake, $"{id} is an intake; answer its ask #{record.Ask} instead: publish it or close it."),
                    CourseNeverWhy.StoodDown => Refused(WordsNever.StoodDown, $"it stood down: #{record.Quest} is someone else's, so it has nothing to go on with."),
                    // A later session of its quest is a quest's; none is a conversation that went on in another (HANDWORDS1).
                    _ when judged == WordsNever.Superseded =>
                        Refused(WordsNever.Superseded, $"#{record.Quest} went on in a later session here, {last}, and its words go there."),
                    _ => Refused(WordsNever.Continued, WordsNever.WentOn(id, next!)),
                };
            case LineCourse.Hold { Why: CourseHoldWhy.Refused, Code: { } code }:
                return RefusedBy(code, id, record);
        }

        try
        {
            new NewSessionChoices(home).Choose(id, prior!.Waiting.Select(word => word.Id).OfType<string>(), now);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new GoOnNewAnswer(false, null, $"your choice could not be kept: {error.Message}");
        }

        return new GoOnNewAnswer(
            true, null,
            "it goes on in a new session at the driver's next look, handed your words; that session starts without this one's conversation.");
    }

    // The door's sentences, by the codes its refusals carry (the course's holds by `Refused`, and the nevers apart): what the person read
    // before the course decided is what they read still.
    private static GoOnNewAnswer RefusedBy(string code, string id, SessionRecord record) => code switch
    {
        WordsNever.Help => Refused(WordsNever.Help,
            $"{id} is Ask Daoris's own conversation, which has no quest a new session would serve: its words go on in it, or "
            + "Ask Daoris's history starts a new conversation from it."),
        Running => Refused(Running, $"{id} is {record.State}: words reach it there, so it needs no new session."),
        NoWords => Refused(NoWords, $"no words wait on {id} to go on with: `daoris-driver sessions say {id} \"…\"` says some."),
        // MSG1f3: the terminal's *Start a conversation with these words*, which takes them off this session.
        Conversation => Refused(Conversation,
            $"{id} is a conversation, which nothing carries on by itself: start a conversation with these words instead: "
            + $"`daoris-driver sessions start-from {id}`."),
        Closed => Refused(Closed,
            $"#{record.Quest} has closed, so nothing carries its session's words on by itself: start a conversation with them "
            + $"instead: `daoris-driver sessions start-from {id}`."),
        NotCooling => Refused(NotCooling, $"the account {id} ran on is not cooling, so the same session goes on with your words at the driver's next look."),
        _ => Refused(code, $"{id} cannot go on in a new session."),
    };

    // What the course reads of the record: the nevers, whether it runs, the words waiting and the quest's status. The nevers are
    // WordsNever's one table, read by its code (a later session of its quest, or the conversation it went on in, is the head's later
    // part either way). Ask Daoris's own conversation has no quest a new session would serve (ASKHIST1). A park is live, but the
    // course reads it as no running turn.
    private static CourseFacts FactsOf(
        SessionRecord record, PriorSession? prior, string? never, string? last, string? next, string? questStatus, CourseAccount? account)
    {
        var words = prior is { WordsWaiting: true } ? [.. prior.Waiting.Select(word => new CourseWord(word.Id ?? "", CourseAuthor.Person))] : new List<CourseWord>();
        var head = new CourseHead(
            record.State, Adapter: prior?.Adapter, Profile: prior?.Profile, Conversation: record.Kind == "chat",
            Teammate: never == WordsNever.Teammate, Intake: never == WordsNever.Intake, StoodDown: never == WordsNever.StoodDown,
            SupersededBy: never == WordsNever.Superseded ? last : never == WordsNever.Continued ? next : null, Waiting: words,
            Session: record.Id, QuestStatus: questStatus, AskDaoris: WordsNever.IsHelp(record));
        return new CourseFacts(head, record.Live ? CourseBoundary.TurnRunning : CourseBoundary.ProcessExited, account);
    }

    // The cool-off a start on the record's adapter would read for its account: its wait, where there is one. An adapter this build no
    // longer has: nothing cools on it, and its own judgement says why it cannot go on.
    private static CourseAccount? CoolingFact(Func<string?, string?, CoolingEntry?> coolingOf, PriorSession prior)
    {
        try
        {
            return coolingOf(prior.Adapter, prior.Profile) is { } cooling
                ? new CourseAccount(new LineAnswer.Wait(cooling, [cooling], ResetSet.Switch, NextHold.Cooling, OwnFirst: true))
                : null;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    private static GoOnNewAnswer Refused(string why, string message) => new(false, why, message);
}
