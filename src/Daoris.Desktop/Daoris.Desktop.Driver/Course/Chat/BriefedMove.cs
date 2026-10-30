namespace Daoris.Driver;

/// <summary>
/// The codes *Go on with Daoris's brief* is refused by that are its own (SESSCTX1c part B), beside <see cref="StartFrom"/>'s, the
/// hand-over's tree and <see cref="WordsNever"/>'s: the page's <c>GO_ON_BRIEF</c> twin, held to this by <c>locales/say.test.ts</c>.
/// </summary>
public static class GoOnBrief
{
    /// <summary>The agent the session ran on is no longer one this machine has: the hand-over chooses another.</summary>
    public const string AgentGone = "agent-gone";
}

/// <summary>
/// A conversation, or a closed quest's session, goes on in a new part of its session handed Daoris's brief (SESSCTX1c part B, D189
/// points 2-3; the session-context design §2): by itself where its own conversation is proven lost (<see cref="ContinueReason.Lost"/>),
/// or by the person's *Go on with Daoris's brief* where it waits for them. A class over the runner's context, as
/// <see cref="ChatHandOver"/> is: the new part opens through the runner's own start, in the tree the old part ran in, on its agent and
/// its account, its record naming the one it continues, why <c>resume</c>, by Daoris or by the person.
/// </summary>
/// <remarks>
/// <para><b>Refused before anything starts</b>, by a code: what never goes on (<see cref="WordsNever"/>), a session that runs or parks
/// (<see cref="StartFrom.Running"/>), no words of the person's waiting (<see cref="StartFrom.NoWords"/>), a quest still open or taken,
/// whose words the driver carries on by itself (<see cref="StartFrom.Carried"/>), a tree that is gone (<see cref="HandOver.TreeGone"/>)
/// and an agent this machine no longer has (<see cref="GoOnBrief.AgentGone"/>), which only the hand-over's choice of agent answers.</para>
///
/// <para><b>A closed quest's session goes on as a conversation</b> in its own tree (the steward's choice): a closed quest opens no
/// driven record. <b>Ask Daoris's goes on in its own room</b>, a conversation of its workspace's, its link kept in the room's history
/// (<see cref="HelpConversations.StartedFrom"/>), since the room's record takes no <c>from</c>, on its own agent: the one path that places
/// Ask Daoris's new part on another agent is <see cref="LimitMove"/>'s, a limit with no account of its agent left (LIMITGOON2, D205
/// §3.5(e)).</para>
///
/// <para><b>Only the person's words go</b>, after the brief and in the same prompt, as the person's: another agent's answer waiting
/// beside them stays on the old record (XAGENT1e, MIXAGENT2a-iii). They leave the old record by their ids, naming the new part, once
/// it took them; until then they stay where they were.</para>
///
/// <para><b>Moved by the open</b> (FOUNDATION1d-ii part 4, the foundation design §3.4 rule 3): against a host whose <c>opens</c> mark is 2 the
/// new part's open moves the person's words, and any of theirs kept since the read, in its own step, so no word falls between the read and the
/// open. The delivery is then the new part's moved words (<see cref="MovedWords"/>): their files copied into its folder, each marked attempted
/// there as it is handed and shown under its id; they leave the new part once it took them, and where its first hand fails they wait on it,
/// whose own go-on carries them. Ask Daoris's room opens through its own door, unkeyed, and moves as before.</para>
///
/// <para><b>The old part ended before the new starts</b> (D51): an ended record holds no tree, and a resume refused on the wire has put
/// its record back first (<see cref="ChatGoOn.WentBackAsync"/>). No closing note is asked: a lost conversation cannot be asked, and a
/// refused one has no turn left (D189's amendment).</para>
/// </remarks>
internal sealed class BriefedMove(ChatContext context, IChatRunning runner, ChatHandOver handOver, IChatHelping help)
{
    /// <summary>What the course answered each briefed move, by session (FOUNDATION1h branch 4): a test hears it; no person reads it.</summary>
    internal event Action<string, LineCourse>? Decided;

    /// <summary>
    /// Go on in a new part handed the brief, one at a time per session: the person's press, a closed quest's session the driver hands
    /// over, and a conversation whose resume was refused on the wire once its run let go.
    /// </summary>
    /// <param name="lost">Why its own conversation was proven lost, where it goes on by itself; null for the person's press.</param>
    /// <param name="by">Who asks: <see cref="HandOverBy.Daoris"/> or <see cref="HandOverBy.Person"/>.</param>
    public async Task<StartedFrom> MoveAsync(
        string sessionId, ContinueReason? lost, string by, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        if (context.Closing) return new StartedFrom(null, false, null, "the application is closing, so no conversation goes on now.");

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!context.GoingOn.TryAdd(sessionId, going.Task))
        {
            return new StartedFrom(null, false, StartFrom.Running, $"`{sessionId}` is going on already.");
        }

        try
        {
            return await MovingAsync(sessionId, lost, by, config, onEnded, ct).ConfigureAwait(false);
        }
        finally
        {
            context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    /// <summary><see cref="MoveAsync"/> within a go-on that already holds the session (<see cref="ChatGoOn"/>'s judgement before a spawn).</summary>
    internal async Task<StartedFrom> MovingAsync(
        string sessionId, ContinueReason? lost, string by, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        var json = await context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var listed = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, sessionId, StringComparison.Ordinal));
        if (listed is null) return Refused(WordsNever.NotFound, $"no session here is {sessionId}.");

        // FOUNDATION1h branch 4 (the foundation design §3.1, §14.2): this is the course's adapter for the briefed move. What the line is, the
        // words waiting, the tree and the agent are read into the facts, and the course decides for CourseTrigger.BriefedPress (the person's
        // press) or CourseTrigger.ConversationLost (Daoris's, on a conversation proven lost); every refusal is said by the code and sentence
        // it has always had. The account's answer is not asked here: the new part's own walk (`ownFirst`) takes it, as before.
        var person = by == HandOverBy.Person;
        var next = WordsNever.ContinuedIn(json, sessionId);
        var last = WordsNever.LastHere(json, listed.Quest);
        var never = WordsNever.Judge(listed, last, next);
        var record = ServiceClient.ReadRecord(json, sessionId);

        // A part that runs is judged before the course only for Daoris's own go-on of a lost conversation, which the course does not ask the
        // boundary of; for the person's press the course refuses it.
        if (!person && never is null && listed.Live) return Refused(StartFrom.Running, RunningWords(sessionId, listed));

        // The quest of a closed session is read only where nothing before it refused, as it was. A quest the driver carries on by itself is
        // the course's refusal of the person's press; a lost go-on's is its own (the course's lost answer for it is the driver's fall-back).
        var carried = (Quest: (string?)null, Status: (string?)null);
        if (never is null && !listed.Live && record is { WordsWaiting: true, Quest: { } quest } && record.Kind != "chat")
        {
            var serving = await context.Service.FindQuestAsync(quest, ct).ConfigureAwait(false);
            carried = (quest, serving?.Status);
            if (!person && serving is { Status: "Open" or "Taken" }) return Refused(StartFrom.Carried, CarriedWords(quest, serving.Status));
        }

        // Its agent is the one whose conversation it is; a record naming none goes on with its role's choice, Chat's order, never the
        // machine's `adapter` key (FOUNDATION1j part iii, the foundation design §2.4's first row). Chat off, or its entry unread, is said below.
        ISessionAdapter? resolved = null;
        var named = record?.Adapter;
        var unchosenWords = (string?)null;
        if (record is not null)
        {
            try
            {
                // FOUNDATION1x: its binding's role first, as a quest's go-on reads it (§2.4).
                named = record.Adapter is { Length: > 0 } opened
                    ? opened
                    : StartPlan.ContinuedOn(
                        record, config, "chat", context.Conversations.RoleOf(sessionId)?.Role, RoleCatalog.ReadHome(context.Home), new RolePlace());
                resolved = context.Harnesses.Adapters.Resolve(named);
            }
            catch (DriverException unchosen)
            {
                // Said below: only the person can choose the agent it goes on with.
                if (named is not { Length: > 0 }) unchosenWords = $"{unchosen.Message} Hand {sessionId} over to another agent.";
            }
        }

        var waiting = (record?.Waiting ?? []).Select(word => new CourseWord(word.Id ?? "", AuthorOf(word), word.Key)).ToList();
        var tree = record?.Tree is { Length: > 0 } held && ConversationTrees.Stands(held) ? held : null;
        var pinned = record is not null && context.Conversations.PickedOn(sessionId, record.Profile);
        var help = WordsNever.IsHelp(listed);
        var facts = new CourseFacts(
            new CourseHead(
                listed.State, Adapter: record?.Adapter, Profile: record?.Profile, Tree: tree, Conversation: record is null || record.Kind == "chat" || record.Quest is null,
                Teammate: never == WordsNever.Teammate, Intake: never == WordsNever.Intake, StoodDown: never == WordsNever.StoodDown,
                SupersededBy: never == WordsNever.Superseded ? last ?? "" : never == WordsNever.Continued ? next ?? "" : null, Waiting: waiting,
                Session: sessionId, QuestStatus: carried.Status, AskDaoris: help, AgentGone: record is not null && resolved is not { Interactive: true },
                Judged: lost?.Code),
            listed.Live ? CourseBoundary.TurnRunning : CourseBoundary.ProcessExited,
            // An account the person picked stays the one account (§3.5(d)): the course says it as the pick the new part keeps.
            Account: new CourseAccount(Picked: pinned), Press: person ? new CoursePress(CoursePressKind.Briefed) : null,
            Rules: new CourseRules(Runner: true));
        var course = CourseDecision.Decide(facts, person ? CourseTrigger.BriefedPress : CourseTrigger.ConversationLost);
        Decided?.Invoke(sessionId, course);

        if (course is not LineCourse.Move move)
        {
            // What the course held, said by the code the refusal has always had: its own refusal's, or, for Daoris's cannot-go-on, the first
            // thing the facts lack in the course's order.
            var code = course switch
            {
                LineCourse.Never => never,
                LineCourse.Hold { Why: CourseHoldWhy.Refused, Code: { } refused } => refused,
                _ => record is not { WordsWaiting: true } || !waiting.Any(word => word.Persons) ? StartFrom.NoWords
                    : tree is null && !help ? HandOver.TreeGone : GoOnBrief.AgentGone,
            };
            return Refused(code!, code switch
            {
                StartFrom.Running => RunningWords(sessionId, listed),
                StartFrom.NoWords when record is not { WordsWaiting: true } => $"no words wait on {sessionId} to go on with.",
                StartFrom.NoWords => $"only another agent's answer waits on {sessionId}, which is never a new part's words.",
                StartFrom.Carried => CarriedWords(carried.Quest!, carried.Status!),
                HandOver.TreeGone => $"the tree {sessionId} ran in is gone, so there is nothing to go on in.",
                GoOnBrief.AgentGone => unchosenWords
                    ?? $"`{named}` is no longer an agent on this machine that holds conversations: hand {sessionId} over to another.",
                WordsNever.Continued => WordsNever.WentOn(sessionId, next!),
                _ => $"{sessionId} never goes on in a new part here ({code}).",
            });
        }

        // Which words go is the course's (D212's difference 1): the person's alone (XAGENT1e, MIXAGENT2a-iii), another agent's claims stay.
        var wanted = move.Words.Ids.ToHashSet(StringComparer.Ordinal);
        var words = record!.Waiting.Where(word => wanted.Contains(word.Id ?? "")).ToList();
        var agent = resolved!;

        // The brief of its line, composed before the new part opens, while the old part is the last of it.
        var brief = await ContextBriefs.ComposeAsync(
                context.Service, context.Events, context.Home, sessionId, HandOverWhy.Resume, conversation: record.Kind == "chat", ct: ct)
            .ConfigureAwait(false);
        // FOUNDATION1a-iii (the design's §3.2 step 6, §7 row 4): the new part takes the walk with its own account first where that is ready,
        // the person's press and a part whose own conversation is proven lost alike, rather than the cooling account as a pick, which
        // refused exactly when the person needed it. An account the person picked stays the one account (§3.5(d)): it waits while it cools.
        // FOUNDATION1d-ii part 4: the new part's open moves the person's words, and any of theirs kept since this read, in its own step.
        var started = help
            ? await HelpStartAsync(record, agent.Name, config, onEnded, ct).ConfigureAwait(false)
            : await runner.StartAsync(
                    record.Repository!, agent.Name, config, onEnded, record.Profile, ct: ct, tree: tree,
                    from: new ContinuedFrom(sessionId, HandOverWhy.Resume, by), ownFirst: move.Where.Source != CourseAccountSource.Pick,
                    moves: Moves(record, words))
                .ConfigureAwait(false);
        if (started.SessionId is not { } to) return new StartedFrom(null, false, null, started.Message);

        var high = RotatedOpening.ContextOf(null, context.Events, sessionId);
        var ranOn = record.Adapter ?? agent.Name;
        var opening = HandOverNotes.MovedFrom(sessionId, ranOn, record.Profile, HandOverWhy.Resume, high, brief.Text.Length);
        var moving = MovedWords.Moving(started);
        var moved = moving ? await HandedAsync(sessionId, to, started, ct).ConfigureAwait(false) : null;
        var files = moved?.Files ?? [.. words.SelectMany(word => ChatFiles.Kept(context.Home, sessionId, word.Files))];
        var message = new ChatMessage(moved?.Text ?? string.Join("\n\n", words.Select(word => word.Text)), files) { Brief = brief, TakenOver = opening };
        var taken = (!moving || moved is { Words.Count: > 0 }) && await handOver.SayHandedOver(to, message, moved?.Shown).ConfigureAwait(false);

        // The move, said on the part it left, where the person wrote it, and in the machine log, by profile name (D125 point 7).
        var on = HandOverNotes.MovedOn(to, agent.Name, record.Profile, HandOverWhy.Resume, high, brief.Text.Length);
        context.Note(sessionId, on);
        context.Service.AccountSaid(Continuations.Moved(to, sessionId, ranOn, agent.Name, record.Profile, record.Profile, HandOverWhy.Resume, briefed: true));

        if (!taken)
        {
            // A part nobody briefed is no success: said on it, and the words stay where they were, the new part where the open moved them.
            context.Note(to, HandOverNotes.Unsaid());
            return new StartedFrom(to, false, null, moving
                ? $"conversation `{to}` opened, but what Daoris kept of `{sessionId}` and your words did not reach it, so your words wait on `{to}`."
                : $"conversation `{to}` opened, but what Daoris kept of `{sessionId}` and your words did not reach it, so they stay on `{sessionId}`.");
        }

        var ids = moved?.Left ?? [.. words.Select(word => word.Id).OfType<string>()];
        if (await TakenAsync(sessionId, to, moved, ids, ct).ConfigureAwait(false)) context.Marks.Clear(sessionId);

        // Where they went, where the person wrote them: their ids, the part and the reason's code (MSG1d), which the page words.
        var went = Continuations.Went(ids, to, lost ?? ContinueWhy.Of(ContinueWhy.Started));
        context.Output?.Append(sessionId, went.Text!);
        context.Events.Keep(sessionId, went, say: null);
        return new StartedFrom(to, true, null, $"`{sessionId}` goes on in conversation `{to}`, handed Daoris's brief and your words.");
    }

    /// <summary>
    /// The go-on of a conversation its agent refused at the open (MODELROLE1d2, D205 §3.5(b)), after an interruption between its two records:
    /// no conversation began there, so it is a fresh start of its actor, never a resume, its record the new part's <c>from</c> (Ask Daoris's
    /// room history) and its waiting words the first message. A pick is never walked afresh: only the picked agent's entries on the picked
    /// account are tried (§3.5(d)), the refused one passed by the reading kept. A refused continuation, whose line holds an earlier part
    /// that held a conversation, is handed Daoris's brief of that line first, recomposed here (<see cref="ContextBriefs"/>), since its
    /// words carried none; a refused new conversation goes on with its words alone. Within a go-on that already holds the session.
    /// </summary>
    internal async Task<StartedFrom> NotOfferedAsync(
        PriorSession record, string json, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        var sessionId = record.Session;
        var isHelp = string.Equals(record.Repository, KnowledgeRoomIdentity.Repository, StringComparison.OrdinalIgnoreCase);
        var actor = isHelp ? "ask-daoris" : "chat";

        // FOUNDATION1h branch 4: a fresh start of the actor is the course's too (the go-on of a conversation its agent refused at the open);
        // the executor reads the facts and says the refusals by the codes they have always had.
        var facts = new CourseFacts(
            new CourseHead(
                record.State, Role: actor, Choice: record.Choice, Adapter: record.Adapter, Profile: record.Profile,
                Tree: record.Tree is { Length: > 0 } held && ConversationTrees.Stands(held) ? held : null, Conversation: true, AskDaoris: isHelp,
                NotOffered: true, Waiting: [.. record.Waiting.Select(word => new CourseWord(word.Id ?? "", AuthorOf(word), word.Key))], Session: sessionId),
            CourseBoundary.ProcessExited);
        var course = CourseDecision.Decide(facts, CourseTrigger.WordsKept);
        Decided?.Invoke(sessionId, course);
        if (course is not LineCourse.Move fresh)
        {
            return course is LineCourse.Hold { Code: HandOver.TreeGone }
                ? Refused(HandOver.TreeGone, $"the tree {sessionId} ran in is gone, so there is nothing to go on in.")
                : Refused(StartFrom.NoWords, $"only another agent's answer waits on {sessionId}, which is never a new part's words.");
        }

        var wanted = fresh.Words.Ids.ToHashSet(StringComparer.Ordinal);
        var words = record.Waiting.Where(word => wanted.Contains(word.Id ?? "")).ToList();

        IReadOnlyList<ActorEffective> order;
        string? pinned = null;
        try
        {
            if (record.Choice == "pick" && record.Adapter is { Length: > 0 } picked)
            {
                // Only an account the person picked is held to its account (FOUNDATION1a-iii, §7 row 2): an agent pick walks its agent's
                // accounts, the refused one passed by the reading kept.
                order = ChatPending.Recovering(
                    actor, [new ActorEffective(picked, record.Asked?.NamedModel, record.Asked?.Effort.Value, "pick")], config);
                pinned = context.Conversations.PickedOn(sessionId, record.Profile) ? record.Profile : null;
            }
            else
            {
                order = ActorChoices.Effective(actor, config);
            }
        }
        catch (ActorUnreadableException unread)
        {
            return Refused(StartFrom.NoWords, unread.Message);
        }

        if (order.Count == 0) return new StartedFrom(null, false, null, $"{(isHelp ? "Ask Daoris" : "Chat")} is off, so `{sessionId}`'s words wait.");

        // A refused continuation: its line holds an earlier part that held a conversation, the refused parts skipped as `LastRun` skips them.
        var records = ServiceClient.ReadRecords(json);
        var continues = false;
        for (var at = record.From; at is { Length: > 0 } && records.TryGetValue(at, out var earlier); at = earlier.From)
        {
            if (earlier.NotOffered) continue;
            continues = true;
            break;
        }

        var brief = continues
            ? await ContextBriefs.ComposeAsync(
                    context.Service, context.Events, context.Home, sessionId, HandOverWhy.NotOffered, conversation: true, ct: ct)
                .ConfigureAwait(false)
            : null;
        var started = isHelp
            ? await HelpStartAsync(record, order[0].Agent, config, onEnded, ct, order, HelpConversations.NotOffered).ConfigureAwait(false)
            : await runner.StartAsync(
                    record.Repository!, order[0].Agent, config, onEnded, pinned, ct: ct, tree: record.Tree,
                    from: new ContinuedFrom(sessionId, HandOverWhy.NotOffered, HandOverBy.Daoris), order: order, moves: Moves(record, words))
                .ConfigureAwait(false);
        if (started.SessionId is not { } to) return new StartedFrom(null, false, null, started.Message);

        // FOUNDATION1d-ii part 4: where the open moved the words, the fresh start's first message is the new part's moved words.
        var moving = MovedWords.Moving(started);
        var moved = moving ? await HandedAsync(sessionId, to, started, ct).ConfigureAwait(false) : null;
        var files = moved?.Files ?? [.. words.SelectMany(word => ChatFiles.Kept(context.Home, sessionId, word.Files))];
        var text = moved?.Text ?? string.Join("\n\n", words.Select(word => word.Text));
        var message = brief is null
            ? new ChatMessage(text, files)
            : new ChatMessage(text, files)
            {
                Brief = brief,
                TakenOver = HandOverNotes.MovedFrom(sessionId, record.Adapter ?? order[0].Agent, record.Profile, HandOverWhy.NotOffered, null, brief.Text.Length),
            };
        if ((moving && moved is not { Words.Count: > 0 }) || !await handOver.SayHandedOver(to, message, moved?.Shown).ConfigureAwait(false))
        {
            context.Note(to, HandOverNotes.Unsaid());
            return new StartedFrom(to, false, null, moving
                ? $"conversation `{to}` opened, but your words did not reach it, so they wait on `{to}`."
                : $"conversation `{to}` opened, but your words did not reach it, so they stay on `{sessionId}`.");
        }

        var ids = moved?.Left ?? [.. words.Select(word => word.Id).OfType<string>()];
        if (await TakenAsync(sessionId, to, moved, ids, ct).ConfigureAwait(false)) context.Marks.Clear(sessionId);
        var went = Continuations.Went(ids, to, ContinueWhy.Of(ContinueWhy.Started));
        context.Output?.Append(sessionId, went.Text!);
        context.Events.Keep(sessionId, went, say: null);
        return new StartedFrom(to, true, null, $"`{sessionId}` goes on in conversation `{to}`, a fresh start of its actor, with your words.");
    }

    /// <summary>
    /// Ask Daoris's new part, in the room of its workspace (ASKSCOPE1b): that workspace's running conversation set aside, as going on in an
    /// earlier one sets it aside, the new one placed and run as a press's is, and its link kept in the room's history.
    /// </summary>
    /// <param name="order">Ask Daoris's order its placement walks (MODELROLE1d2's fresh start); null places it on <paramref name="agent"/>.</param>
    /// <param name="handed">What its history says it was handed: <see cref="HelpConversations.Brief"/> or <see cref="HelpConversations.NotOffered"/>.</param>
    private async Task<ChatStart> HelpStartAsync(
        PriorSession record, string agent, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct,
        IReadOnlyList<ActorEffective>? order = null, string handed = HelpConversations.Brief)
    {
        if (await help.SetAsideHelpAsync(null, record.Workspace, ct).ConfigureAwait(false) is { } answering)
        {
            return new ChatStart(null, $"{HeldWords.AnsweringWhy(answering)}.");
        }

        var placed = await runner.PlaceHelpAsync(agent, config, record.Workspace, ct, order: order).ConfigureAwait(false);
        if (placed.SessionId is not { } id) return new ChatStart(null, placed.Message);
        try
        {
            new HelpConversations(context.Home).StartedFrom(id, record.Session, handed);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // Its history names no earlier conversation; its brief still says which it continues.
        }

        return await runner.RunHelpAsync(placed, config, help.DescribeHelp ?? (_ => Task.FromResult<HelpMachine?>(null)), onEnded, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The words off the record they were said to by their ids, naming the part that took them (MSG1a's taken door). A refusal costs a
    /// line in that part, never the part: false then, and the record still keeps them.
    /// </summary>
    private async Task<bool> TakeAsync(string sessionId, IReadOnlyList<string> ids, string to, CancellationToken ct)
    {
        if (ids.Count == 0) return true;
        string? failed;
        try
        {
            var (taken, message) = await context.Service.TakenAsync(sessionId, ids, by: to, ct).ConfigureAwait(false);
            failed = taken ? null : message;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            failed = error.Message;
        }

        if (failed is null) return true;
        context.Note(to, WordsNotes.StartNotTaken(sessionId, failed));
        return false;
    }

    /// <summary>What the new part's open is asked to move (FOUNDATION1d-ii part 4): the person's words named, and every word this read saw waiting.</summary>
    private static MoveWords Moves(PriorSession record, IReadOnlyList<SaidWordView> words) =>
        MovedWords.Asked(record.Session, words.Select(word => word.Id), record.Waiting.Select(word => word.Id));

    /// <summary>
    /// The words the open moved to the new part, as it is handed them (FOUNDATION1d-ii part 4): read there, their files copied, and marked
    /// attempted where this runner holds the part, so a hand that cannot be made marks nothing; null where the new part's record did not read.
    /// </summary>
    private async Task<MovedWords?> HandedAsync(string from, string to, ChatStart started, CancellationToken ct)
    {
        var moved = await MovedWords.OnAsync(context.Service, context.Home, from, to, started.Moved, ct).ConfigureAwait(false);
        return moved is not null && context.Holds(to) ? await moved.AttemptedAsync(context.Service).ConfigureAwait(false) : moved;
    }

    /// <summary>
    /// The handed words taken off: the new part's by their ids there where the open moved them, judged by what it holds after, a failure said
    /// on it; else the old record's by theirs, naming the new part, as before. True where none of them waits any more.
    /// </summary>
    private async Task<bool> TakenAsync(string sessionId, string to, MovedWords? moved, IReadOnlyList<string> ids, CancellationToken ct)
    {
        if (moved is null) return await TakeAsync(sessionId, ids, to, ct).ConfigureAwait(false);
        if (await moved.TakeAsync(context.Service, ct).ConfigureAwait(false)) return true;
        context.Note(to, WordsNotes.StartNotTaken(to, "they still wait on it."));
        return false;
    }

    private static StartedFrom Refused(string why, string message) => new(null, false, why, message);

    private static string RunningWords(string sessionId, SessionRecord listed) =>
        $"{sessionId} is {listed.State}: words reach it there, so it needs no new part.";

    private static string CarriedWords(string quest, string status) =>
        $"#{quest} is {status.ToLowerInvariant()}, so the driver carries these words on with it by itself.";

    /// <summary>Who wrote a word, as the course tells them apart (the person's, Daoris's, the supervisor's, a second opinion's, a helper's).</summary>
    internal static CourseAuthor AuthorOf(SaidWordView word) =>
        word.Persons ? CourseAuthor.Person
        : word.Daoris ? CourseAuthor.Daoris
        : word.Supervisors ? CourseAuthor.Supervisor
        : word.By is not null ? CourseAuthor.Findings
        : CourseAuthor.Helper;
}
