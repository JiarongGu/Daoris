namespace Daoris.Driver;

/// <summary>
/// The conversations waiting to be handed over (MIXAGENT1a-i, D163 §2): <c>&lt;home&gt;/sessions/&lt;session&gt;.hand-over.json</c>,
/// beside the session's marks. A box is the ask, kept with when it was given (D143 point 4), until the shell's chat runner takes
/// it: at once where the conversation is not talking, else at its turn's end, else as the shell comes up.
/// </summary>
/// <remarks>
/// <para><b>This machine's, never the record's.</b> A file that does not read is no ask: nothing is handed over, which loses nothing.</para>
///
/// <para>What is kept is this store's; the file and its format are <see cref="HandOverFiles"/>' (STRUCT3h, D166's note).</para>
/// </remarks>
public sealed class HandOverBox(string home)
{
    public const string Suffix = SessionHomeFiles.HandOverBoxSuffix;

    /// <summary>Where a session's box is kept.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>Keep the ask, replacing any before. An id that is not a session's keeps nothing.</summary>
    /// <exception cref="IOException">The ask could not be written: the asker is told, rather than told it was kept.</exception>
    public void Write(string session, HandOverRequest request)
    {
        if (!SessionEventText.IsLocalId(session)) return;
        HandOverFiles.WriteBox(PathOf(session), request);
    }

    /// <summary>The ask kept for this session, or null for none, a file that does not read, or an id that is not one.</summary>
    public HandOverRequest? Read(string session) => SessionEventText.IsLocalId(session) ? HandOverFiles.ReadBox(PathOf(session)) : null;

    /// <summary>The sessions with a box, in the order of their ids: what a shell coming up takes.</summary>
    public IReadOnlyList<string> Pending() =>
        [.. HandOverFiles.Leaves(DaorisHome.Entry(home, HomeEntry.Sessions),"*" + Suffix)
            .Select(leaf => leaf[..^Suffix.Length])
            .Where(SessionEventText.IsLocalId)
            .Order(StringComparer.Ordinal)];

    /// <summary>Take a session's box away: it was taken, or could not be. Nothing to take is no failure.</summary>
    public void Clear(string session)
    {
        // A box left behind is judged again when it is taken, and refused if it no longer holds.
        if (SessionEventText.IsLocalId(session)) HandOverFiles.Delete(PathOf(session));
    }
}

/// <summary>
/// *Hand a conversation over* (MIXAGENT1a-i, D163, the mixed-agents design §2 and §5): a conversation goes on with another agent
/// when its own account's allowance runs low. This is the ask's judge: it refuses what cannot be handed over by a code before
/// anything is kept, and keeps the <see cref="HandOverBox"/> the shell's chat runner takes.
/// </summary>
/// <remarks>
/// <b>Conversations only here.</b> A quest's session is <see cref="QuestHandOver"/>'s (MIXAGENT1a-ii); asked of this judge it is
/// refused <see cref="NotConversation"/>, saying where its door is. Ask Daoris's own
/// conversations and intakes never go on elsewhere (D137 §2.2), and the conversation cannot be handed to the agent and account it
/// runs on, nor while another hand-over waits, nor from a tree that is gone. The one path that moves Ask Daoris's conversation to
/// another agent is no hand-over: its turn met a limit with no account of its agent left, and <see cref="LimitMove"/> goes on with its
/// actor's next choice (LIMITGOON2, D205 §3.5(e)); the person's press and an agent's ask are still refused here.
/// </remarks>
public static class HandOver
{
    /// <summary>A quest's session where no driven roster is handed in (the shell's page press, a later row), or a conversation about a quest.</summary>
    public const string NotConversation = "not-conversation";

    /// <summary>A quest's session whose quest is not open or taken any more (MIXAGENT1a-ii): no work is left to hand over.</summary>
    public const string QuestClosed = HandOverAskWhy.QuestClosed;

    /// <summary>A quest's session whose quest is not this machine's to carry on: taken elsewhere, or the session stood down.</summary>
    public const string QuestElsewhere = HandOverAskWhy.QuestElsewhere;

    /// <summary>A quest's session whose take was lost to the person's own reopen of the work its step grew from (FABRIC-CLOSE1c20).</summary>
    public const string QuestReopened = HandOverAskWhy.QuestReopened;

    /// <summary>A quest's session that is not the quest's last run on this machine, which a later session here carries.</summary>
    public const string NotLastRun = HandOverAskWhy.NotLastRun;

    /// <summary>A quest's session parked on the person, who answers it instead.</summary>
    public const string Parked = "parked";

    /// <summary>The named agent's door cannot drive the quest's repository: a pipe-door agent where Daoris is not adopted.</summary>
    public const string NoDoor = HandOverAskWhy.NoDoor;

    /// <summary>The roster has no such agent.</summary>
    public const string UnknownAgent = HandOverAskWhy.UnknownAgent;

    /// <summary>The account is not one of that agent's.</summary>
    public const string NotItsAccount = "not-its-account";

    /// <summary>It already runs on that agent and account.</summary>
    public const string SameAgent = HandOverAskWhy.SameAgent;

    /// <summary>It runs in another Daoris process, which this one cannot end (the chat runner's own refusal).</summary>
    public const string Running = HandOverAskWhy.Running;

    /// <summary>The new conversation opened, but Daoris's briefing did not reach its agent (an answer of the take, not a refusal).</summary>
    public const string Unbriefed = "unbriefed";

    /// <summary>Another hand-over of it waits.</summary>
    public const string Waiting = HandOverAskWhy.Waiting;

    /// <summary>The tree it ran in no longer exists, so the new conversation has nowhere to go on.</summary>
    public const string TreeGone = HandOverAskWhy.TreeGone;

    /// <summary>The ask is an agent's, which the person's rule has not admitted: it waits for the person's yes, which the look keeps, and nothing is kept here.</summary>
    public const string AwaitsYes = "awaits-yes";

    /// <summary>
    /// The hand-over rule's consent for an agent's ask as the door that judged it says it (FOUNDATION1h branch 3, the foundation design §3.1): the
    /// verdict stays in <c>DriverHandOvers</c> and reaches the course as a fact, so the course admits an agent's ask only where <c>on</c> let
    /// it go, puts it to the person under <c>ask</c> and refuses it under <c>off</c>.
    /// </summary>
    internal static CourseRules ConsentOf(HandOverAskVerdict verdict) =>
        new(HandOverAdmits: verdict.Kind == HandOverAskVerdict.GoKind, HandOverAsks: verdict.Kind == HandOverAskVerdict.WaitKind);

    /// <summary>
    /// Judge and decide the move for a conversation of this machine's (FOUNDATION1f-v part a, §3.3, §13.4 step 1): the hand-over is kept as a
    /// move record in the store, decided as it is asked, and no <c>&lt;s&gt;.hand-over.json</c> is written. A record a move still stands for,
    /// whether a hand-over waiting or another move under way, takes no second ask, read from the store. Whether and where it goes is the
    /// course's (FOUNDATION1h branch 3, <see cref="JudgedAsync"/>); this keeps what it decided.
    /// </summary>
    /// <param name="accountsOf">The accounts the roster holds for an agent, or null where it has no such agent.</param>
    /// <param name="moves">The conversations' move record, whose run claims the decision.</param>
    /// <param name="consent">The hand-over rule's consent, where the ask is an agent's: null is none, which the course never admits.</param>
    /// <param name="boundary">Where the conversation stands: a turn running or between turns.</param>
    /// <param name="before">
    /// What the executor refuses itself once the course allows the move, before it is kept: a conversation another process runs (the chat
    /// runner's). Null where it refuses nothing.
    /// </param>
    /// <param name="decided">What the course answered: a test's, no person's.</param>
    internal static async Task<HandOverAsked> AskAsync(
        ServiceClient service, ConversationMoves moves, Func<string, IReadOnlyList<string>?> accountsOf, string id, string agent, string? account,
        string why, string askedBy, DateTimeOffset now, CancellationToken ct = default, CourseRules? consent = null,
        CourseBoundary boundary = CourseBoundary.BetweenTurns, Func<PriorSession, HandOverAnswer?>? before = null, Action<LineCourse>? decided = null)
    {
        var judged = await JudgedAsync(service, accountsOf, id, agent, account, ct, askedBy, why, consent, boundary, decided).ConfigureAwait(false);
        if (judged.Refused is not null) return new HandOverAsked(judged.Refused, null, null);
        if (before?.Invoke(judged.Prior!) is { } held) return new HandOverAsked(held, null, null);

        // The course's move names where it goes: the account as a pick the person's press keeps, the agent the ask named.
        var where = ((LineCourse.Move)judged.Course!).Where;
        var named = where.Account;
        var begun = await moves.BeginAsync(
            new MoveDecision(id, MoveExecutors.Conversation, MoveHows.Brief, why, askedBy) { Agent = where.Agent ?? agent, Account = named }, ct).ConfigureAwait(false);
        if (begun.Move is { } move)
        {
            return new HandOverAsked(
                new HandOverAnswer(true, null, $"it goes on with {agent} as soon as its turn is over, in a conversation of its own."), move,
                new HandOverRequest(agent, named, why, askedBy, now));
        }

        if (begun.Standing is { } standing)
        {
            return new HandOverAsked(
                Refused(Waiting, standing is { Agent: { } waiting } && string.Equals(standing.Left, id, StringComparison.Ordinal)
                    ? $"{id} is already waiting to be handed over to {waiting}."
                    : $"{id} has a move under way, which an ask waits behind."), null, null);
        }

        return new HandOverAsked(new HandOverAnswer(false, null, $"your ask could not be kept: {begun.Refused}"), null, null);
    }

    /// <summary>
    /// What can be known of a hand-over before anything is kept or taken: the refusal by its code, or the record as the ledger holds
    /// it. Judged again when the box is taken, since the conversation, its tree or the roster may have changed while it waited.
    /// </summary>
    internal static async Task<(HandOverAnswer? Refused, PriorSession? Prior)> JudgeAsync(
        ServiceClient service, Func<string, IReadOnlyList<string>?> accountsOf, string id, string agent, string? account,
        CancellationToken ct = default)
    {
        var judged = await JudgedAsync(service, accountsOf, id, agent, account, ct).ConfigureAwait(false);
        return (judged.Refused, judged.Prior);
    }

    /// <summary>
    /// <see cref="JudgeAsync"/> with the course's answer (FOUNDATION1h branch 3, the foundation design §3.1, §14.2): the record is read into the
    /// facts <see cref="CourseDecision.Decide"/> decides a hand-over over, its nevers by <see cref="WordsNever"/>'s one table, and whatever the
    /// course refuses is said by the code and sentence the ask has always had. What only the executor knows stays its own and is judged beside
    /// the course, in the order of today's refusals: no such session, a quest's session (<see cref="QuestHandOver"/>'s), an agent or an account
    /// the roster does not hold.
    /// </summary>
    /// <param name="askedBy">Who asks, one of <see cref="HandOverBy"/>: an agent's ask is admitted only by the consent handed in.</param>
    /// <param name="why">The ask's reason, one of <see cref="HandOverWhy"/>.</param>
    /// <param name="consent">The hand-over rule's consent for an agent's ask; null is none (<see cref="ConsentOf"/>).</param>
    /// <param name="decided">Told the course's answer for a conversation or what never goes on: a test's, no person's.</param>
    internal static async Task<HandOverJudged> JudgedAsync(
        ServiceClient service, Func<string, IReadOnlyList<string>?> accountsOf, string id, string agent, string? account,
        CancellationToken ct = default, string askedBy = HandOverBy.Person, string why = HandOverWhy.Press, CourseRules? consent = null,
        CourseBoundary boundary = CourseBoundary.BetweenTurns, Action<LineCourse>? decided = null)
    {
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) return new HandOverJudged(Refused(WordsNever.NotFound, $"no session here is {id}."), null, null);

        var last = WordsNever.LastHere(json, record.Quest);
        var next = WordsNever.ContinuedIn(json, id);
        var never = NeverCode(record, last, next);
        var prior = ServiceClient.ReadRecord(json, id);
        var named = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        var conversation = record.Kind == "chat" && record.Quest is null;

        // The tree is a fact only where it stands; a session that never goes on is refused first, so its tree is not looked for.
        var tree = conversation && never is null && prior?.Tree is { Length: > 0 } held && ConversationTrees.Stands(held) ? held : null;
        var facts = new CourseFacts(
            new CourseHead(
                record.State, Adapter: prior?.Adapter, Profile: prior?.Profile, Tree: tree, Conversation: conversation,
                Teammate: never == WordsNever.Teammate, Intake: never == WordsNever.Intake,
                SupersededBy: never == WordsNever.Continued ? next ?? "" : null, AskDaoris: never == WordsNever.Help, Session: id),
            boundary, Press: new CoursePress(CoursePressKind.HandOver, agent, named, Why: CourseFrom.WhyOf(why), By: CourseFrom.ByOf(askedBy)),
            Rules: consent);
        var course = CourseDecision.Decide(facts, CourseTrigger.HandOver);
        if (conversation || course is LineCourse.Never) decided?.Invoke(course);

        // What never goes on, whichever kind of session asks (the course's nevers and Ask Daoris's own conversation).
        if ((course is LineCourse.Never or LineCourse.Hold { Why: CourseHoldWhy.Refused, Code: WordsNever.Help }) && Said(never, record, id, next) is { } stays)
        {
            return new HandOverJudged(stays, null, course);
        }

        if (!conversation)
        {
            return new HandOverJudged(Refused(NotConversation, record.Kind == "chat"
                ? $"{id} is a conversation about a quest, not its session: hand over the quest's own session instead."
                : $"{id} is a quest's session: it is handed over from a terminal, `daoris-driver sessions hand-over {id} --agent <adapter>`; its page's press comes later."), null, course);
        }

        var accounts = accountsOf(agent);
        if (accounts is null) return new HandOverJudged(Refused(UnknownAgent, $"no agent here is called {agent}."), null, course);

        if (named is not null && !accounts.Contains(named, StringComparer.OrdinalIgnoreCase))
        {
            return new HandOverJudged(Refused(NotItsAccount, $"{named} is not an account of {agent}."), null, course);
        }

        // The course's own refusals, by their codes; a move is the one answer that goes on, and the executor keeps it.
        return course switch
        {
            LineCourse.Move => new HandOverJudged(null, prior, course),
            LineCourse.Hold { Why: CourseHoldWhy.Refused, Code: SameAgent } => new HandOverJudged(
                Refused(SameAgent, named is null ? $"{id} already runs on {agent}." : $"{id} already runs on {agent} as {named}."), null, course),
            LineCourse.Hold { Why: CourseHoldWhy.Refused, Code: TreeGone } => new HandOverJudged(
                Refused(TreeGone, $"the tree {id} ran in is gone, so there is nothing to go on in."), null, course),
            // An agent's ask the rule has not admitted: put to the person first under `ask`, refused under `off`, or where none was said.
            LineCourse.Hold { Why: CourseHoldWhy.ConsentRefused, Doors.Count: > 0 } => new HandOverJudged(
                Refused(AwaitsYes, "its agent's ask waits for the person's yes first: the look keeps it, and nothing is kept here."), null, course),
            LineCourse.Hold { Why: CourseHoldWhy.ConsentRefused } => new HandOverJudged(
                Refused(HandOverAskWhy.Off, HandOverAskWhy.Said(HandOverAskWhy.Off)), null, course),
            LineCourse.Hold { Code: { } code } => new HandOverJudged(Refused(code, $"{id} cannot be handed over."), null, course),
            _ => new HandOverJudged(Refused(HandOverAskWhy.Cannot, $"{id} cannot be handed over."), null, course),
        };
    }

    /// <summary>
    /// The code of what never goes on elsewhere, whichever kind of session asks (<see cref="WordsNever"/>'s one table, then Ask Daoris's own
    /// conversation): the course's facts and <see cref="Never"/>'s sentences are both read from it.
    /// </summary>
    internal static string? NeverCode(SessionRecord record, string? last, string? next) =>
        WordsNever.Judge(record, last, next) ?? (WordsNever.IsHelp(record) ? WordsNever.Help : null);

    /// <summary>
    /// What never goes on elsewhere, whichever kind of session asks: a teammate's record, Ask Daoris's own conversation, an intake,
    /// and a conversation that went on in another already (HANDWORDS1), whose tree that one holds: a second take would open another
    /// conversation in it, and a box left from before the shell closed is refused as it is taken.
    /// </summary>
    /// <param name="next">The conversation it went on in (<see cref="WordsNever.ContinuedIn(string, string)"/>), or null.</param>
    internal static HandOverAnswer? Never(SessionRecord record, string id, string? last, string? next = null) =>
        Said(NeverCode(record, last, next), record, id, next);

    /// <summary>The sentence of a never's code, or null for a code that is no refusal of a hand-over (a quest's later session, a stand-down).</summary>
    private static HandOverAnswer? Said(string? never, SessionRecord record, string id, string? next)
    {
        switch (never)
        {
            case WordsNever.Continued:
                return Refused(WordsNever.Continued, $"{id} went on in conversation {next}; hand that one over instead.");
            case WordsNever.Teammate:
                return Refused(WordsNever.Teammate, $"{id} ran on {id[..id.IndexOf('/')]}, where its conversation is; nothing said here reaches it.");
            case WordsNever.Help:
                // LIMITGOON2 (D205 §3.5(e)): only a limit with no account of its agent left moves it to another agent (LimitMove), never this.
                return Refused(WordsNever.Help,
                    $"{id} is Ask Daoris's own conversation, which stays on its own agent: Ask Daoris's history starts a new conversation from it.");
            case WordsNever.Intake:
                return Refused(WordsNever.Intake, $"{id} is an intake; answer its ask #{record.Ask} instead: publish it or close it.");
            default:
                return null;
        }
    }

    internal static HandOverAnswer Refused(string why, string message) => new(false, why, message);
}

/// <summary>What a hand-over was judged to be (<see cref="HandOver.JudgedAsync"/>): its refusal, or the record as the ledger holds it, with the course's answer where one was decided.</summary>
internal sealed record HandOverJudged(HandOverAnswer? Refused, PriorSession? Prior, LineCourse? Course);

/// <summary>What an ask came to (<see cref="HandOver.AskAsync"/>): the answer, and where it was kept the move decided for it with the request.</summary>
internal sealed record HandOverAsked(HandOverAnswer Answer, ConversationMove? Move, HandOverRequest? Request);

/// <summary>
/// Taking a hand-over (MIXAGENT1a-i, D163 §3): judged again, then the old conversation ends, the new starts in its tree on
/// the agent and account asked for and is told Daoris's briefing, and both records say where it went and where it came from.
/// Pure of the runner's own parts, which are handed in, so the sequence is held in-process.
/// </summary>
/// <remarks>
/// <b>The old ends before the new starts</b>: a tree is held by one session at a time (D51). <b>The move is ended however it came out</b>
/// (its executor's, FOUNDATION1f-v part a), so a refused start is not tried again at every shell's come-up; the asker is told, and asks again.
/// </remarks>
public static class HandOverTake
{
    /// <summary>Judge, end, start, brief and say where it went.</summary>
    /// <param name="end">Ends the old conversation by the runner's own end, and returns once its record has ended.</param>
    /// <param name="start">
    /// Opens the new conversation in the old one's tree on the ask's agent and account, its record naming the one it continues,
    /// why and who asked (MIXAGENT1a-iii), asking the host to move the old part's waiting words under the move's key where it has one
    /// (<paramref name="opens"/>): every word of the person's kept there meanwhile, none named, since a hand-over carries none of its own.
    /// </param>
    /// <param name="opens">The key the move's open is sent with (FOUNDATION1f-v part a); null for an open with no move record behind it.</param>
    /// <param name="say">Hands the new conversation its first message; false where nothing took it.</param>
    /// <param name="moved">
    /// What the usage curve's decision said, where Daoris moves the conversation to another account by it (SWITCH1c-ii): the
    /// two lines are then the moved pair (<see cref="HandOverNotes.Moved"/>, <see cref="HandOverNotes.MovedFrom"/>), naming the
    /// accounts. Null for every other take, and for a curve's box taken where the decision is not held (a shell come up since),
    /// whose lines are the hand-over's, its reason <c>curve</c>.
    /// </param>
    /// <param name="closing">
    /// The outgoing agent's closing note, where the take asked one (SESSCTX1c part B, §2.2): carried as the brief's note, or said absent;
    /// null where none was asked.
    /// </param>
    public static async Task<HandedOver> RunAsync(
        ServiceClient service, string home, SessionEvents events, SessionOutput? output, string id, HandOverRequest ask,
        Func<string, IReadOnlyList<string>?> accountsOf, Func<string, Task> end,
        Func<PriorSession, HandOverRequest, ContinuedFrom, MoveWords?, CancellationToken, Task<ChatStart>> start, Func<string, ChatMessage, Task<bool>> say,
        CancellationToken ct = default, SwitchChoice.MoveTo? moved = null, ClosingNote? closing = null, string? opens = null)
    {
        var (refused, prior) = await HandOver.JudgeAsync(service, accountsOf, id, ask.Agent, ask.Account, ct).ConfigureAwait(false);
        if (refused is not null) return new HandedOver(null, false, refused.Why, refused.Message);

        var from = prior!.Adapter ?? "its agent";
        // MIXAGENT1c: where its agent asked to hand over, its own note, told after Daoris's record as that agent's claim; SESSCTX1c part
        // B: else the closing note the take asked, or that none was written.
        var note = (ask.AskedBy == HandOverBy.Agent ? new HandOverAskFiles(home).Ask(id)?.Note : null) ?? closing?.Note;
        // SESSCTX1b: Daoris's brief of the whole line (ContextBrief), in place of the request, the last plan and words and what
        // is uncommitted, the note its own section (the design §3.1 item 6).
        var brief = await ContextBriefs.ComposeAsync(
                service, events, home, id, ask.Why, conversation: true, note: note, ct: ct, noteAsked: closing is not null, before: closing?.Before)
            .ConfigureAwait(false);

        await end(id).ConfigureAwait(false);

        // FOUNDATION1f-v part a (§3.4 rule 3): the open is keyed with nothing named and nothing read, as the curve's is, so the host moves every
        // word of the person's kept on the old part meanwhile and the new part's own go-on says it after the brief, never before.
        var moves = opens is null ? null : MovedWords.Asked(id, [], []) with { Opens = opens };
        var started = await start(prior, ask, new ContinuedFrom(id, ask.Why, ask.AskedBy), moves, ct).ConfigureAwait(false);
        if (started.SessionId is not { } to)
        {
            return new HandedOver(
                null, false, null,
                $"{id} ended, but the conversation with {ask.Agent} did not start: {started.Message} Its tree is as it was.");
        }

        // A move by the usage curve names the account it left and the one it went to (SWITCH1c-ii): the record that says so stays
        // on this machine. Only where the decision is the move's own, on the account the record ran on.
        var left = moved is not null && ask.Why == HandOverWhy.Curve
                   && string.Equals(moved.Account, ask.Account, StringComparison.OrdinalIgnoreCase) && prior.Profile is { Length: > 0 } account
            ? account
            : null;
        var opening = left is null ? HandOverNotes.TakenOver(from, id, ask.Why) : HandOverNotes.MovedFrom(left, moved!);
        var taken = await say(to, new ChatMessage("", []) { Brief = brief, TakenOver = opening }).ConfigureAwait(false);

        // Where it went, where the person wrote it: the old conversation's last line, after its own end.
        var went = left is null ? HandOverNotes.HandedOver(ask.Agent, to) : HandOverNotes.Moved(left, moved!);
        output?.Append(id, went.Note);
        events.Keep(id, new SessionEvent { Kind = SessionEventKind.Note, Text = went.Note, Parts = went.Parts }, say: null);

        // A briefing nobody took is no success: the new conversation says so, by a code, and the answer carries it.
        if (!taken)
        {
            var unsaid = HandOverNotes.Unsaid();
            output?.Append(to, unsaid.Note);
            events.Keep(to, new SessionEvent { Kind = SessionEventKind.Note, Text = unsaid.Note, Parts = unsaid.Parts }, say: null);
        }

        return new HandedOver(
            to, true, taken ? null : HandOver.Unbriefed,
            taken
                ? $"conversation `{to}` goes on from `{id}` with {ask.Agent}."
                : $"conversation `{to}` opened with {ask.Agent}, but what Daoris kept of `{id}` did not reach it: tell it where you were.");
    }
}
