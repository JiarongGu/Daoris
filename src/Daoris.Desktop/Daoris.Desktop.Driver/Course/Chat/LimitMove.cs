using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// A conversation whose turn its account's limit refused goes on in a new part on the next account: at that turn's end, a new part of its line on the account of the same agent the walk
/// offers next, the cooling one passed, carrying its own conversation there, or handed Daoris's brief, then the person's words the refused turns held, its record continuing
/// the old by <c>limit</c>, asked by <c>daoris</c>. A repository's chat opens it in the same tree; Ask Daoris's opens it in its
/// workspace's room, the link kept in the room's history, since the room's record takes no <c>from</c>. Over the runner's context, as
/// <see cref="CurveMove"/> and <see cref="BriefedMove"/> are.
/// </summary>
/// <remarks>
/// <para><b>Onto another account of its own agent it carries its own conversation</b> where its door can (<see cref="ChatLimitCarry"/>),
/// and is briefed where it cannot, saying why.</para>
/// <para><b>Never mid-turn, never during a take.</b> Judged once per turn's end with nothing running, opening or queued, as the curve's
/// move is; the turns are held while it moves, so a word said as it begins either runs its turn first or waits for the new part.</para>
/// <para><b>Where no other account has room</b> it stays where it is and its words with it: the next refused turn tries again. A teammate's
/// record, an intake and a conversation that went on already never move; a conversation the person ended has no refused turn.</para>
/// </remarks>
[Executor("limit-move", ExecutorPhase.Move)]
internal sealed class LimitMove(IExecutorWiring wiring) : IExecutor
{
    /// <summary>Every family of the desktop's store the move reads, the preview's and the rules': the wait's stamp reads their revisions.</summary>
    internal static readonly ImmutableArray<string> Reads =
        [RoleStaffing.Family, RoleCatalog.Family, StaffPolicies.Family, StaffLedger.Family, SupervisorRuleStore.Family, StaffMoves.Family, BindingStore.Family];

    private ChatContext context => wiring.Context;

    private IChatRunning runner => wiring;

    private ChatHandOver handOver => wiring.Of<ChatHandOver>();

    private IChatHelping help => wiring.Of<IChatHelping>();

    /// <summary>
    /// The <see cref="StartedFrom.Why"/> of an ended move that left its conversation waiting for the account the person picked: a lost
    /// conversation's go-on reads it and waits, since the brief's move would refuse that account and mark the words unable to go on.
    /// </summary>
    internal const string OnPick = "on-pick";

    /// <summary>What each move came to, by the session it moved from: a test hears it; no person reads it.</summary>
    public event Action<string, StartedFrom>? Moved;

    /// <summary>What the course answered each limit move, live or ended, by session: a test hears it; no person reads it.</summary>
    internal event Action<string, LineCourse>? Decided;

    /// <summary>The account rule's answer where no account of the agent has room: the course holds the words and opens nothing.</summary>
    internal static readonly LineAnswer NoRoom = new LineAnswer.Hold(NextHold.Cooling, LineRepair.None);

    /// <summary>
    /// The facts a limit move reads into the course: a conversation between its parts, its words, whether its account is picked and the account rule's answer as the walk gives it, with its actor's later choice on another agent
    /// where one can run (a conversation never takes its own account for an automatic part, except where lost).
    /// </summary>
    internal LineCourse Decide(
        string sessionId, PriorSession prior, bool isHelp, CourseTrigger trigger, IReadOnlyList<SaidWordView> waiting, bool picked, LineAnswer answer,
        IReadOnlyList<ActorEffective>? order, bool restarted, bool limited = true, bool ended = false)
    {
        List<CourseWord> words = [.. waiting.Select(word => new CourseWord(word.Id ?? "", BriefedMove.AuthorOf(word), word.Key))];
        // A restart's go-on carries its own word, which the record's waiting words need not name.
        if (restarted && !words.Any(word => DaorisKeys.IsRestart(word.Key)))
        {
            words.Add(new CourseWord("", CourseAuthor.Daoris, DaorisKeys.Restart(sessionId, "restart")));
        }

        var facts = new CourseFacts(
            new CourseHead(
                prior.State, Limited: limited, Role: prior.Actor ?? (isHelp ? "ask-daoris" : "chat"), Choice: prior.Choice, Adapter: prior.Adapter,
                Profile: prior.Profile, Conversation: true, AskDaoris: isHelp, Waiting: words, Session: sessionId, Tree: prior.Tree),
            ended ? CourseBoundary.ProcessExited : CourseBoundary.BetweenTurns,
            Account: new CourseAccount(
                Answer: answer, Picked: picked, Later: order is { Count: > 0 } later ? new CourseLater(later[0].Agent) : null),
            Rules: new CourseRules(Runner: true));
        return CourseDecision.Decide(facts, trigger);
    }

    /// <summary>
    /// The conversations' move record: the live move's record is written there, and the shell's come-up takes a dead
    /// shell's moves up through it. A test hands one over a store of its own run.
    /// </summary>
    internal ConversationMoves Moves { get; set; } = new(wiring.Context.Home, wiring.Context.Service, wiring.Context.Events, wiring.Context.Output);

    /// <summary>The supervisor's rules a limit reads before its walk; a test hands its own.</summary>
    internal ConversationRules? Rules { get; set; }

    /// <summary>A turn's end, told as the turns move: judged on a task of its own, as <see cref="CurveMove.Due"/> is.</summary>
    public void Due(string sessionId, DriverConfig config, Func<string, string, Task>? onEnded)
    {
        if (context.Closing) return;
        _ = Task.Run(async () =>
        {
            try
            {
                if (await MoveAsync(sessionId, config, onEnded).ConfigureAwait(false) is { } moved) Moved?.Invoke(sessionId, moved);
            }
            catch (Exception)
            {
                // A move that failed leaves the conversation where it is, its record saying the account cools: the next refused turn tries again.
            }
        });
    }

    /// <summary>
    /// Move the conversation where its last turn got refused by its account's limit (<see cref="LimitHeld"/>); null where nothing is
    /// judged to move: its last turn ended otherwise, it is no conversation of this machine's that runs here, or a move is under way.
    /// </summary>
    /// <remarks>
    /// The move keeps one record in the store (<see cref="Moves"/>), decided
    /// before its turns are held, so a record a move still stands for, the one it left or the one whose open made it, takes no move now; its
    /// phases are written before what each fences, the old part's end, the keyed open under the key it is sent with, and the prompt's mark
    /// before the hand; each handed word's <c>sent</c> as the turn's write returns, then the take, then the reading that closes it
    /// <c>delivered</c>. A write the store refuses stops the move before the effect it fences.
    /// </remarks>
    public async Task<StartedFrom?> MoveAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default)
    {
        // A hand-over's box an older build left, or its move the store carries and has not begun, holds the record from a move.
        if (context.Closing || context.GoingOn.ContainsKey(sessionId) || handOver.Holds(sessionId)) return null;
        if (!context.Turned.TryGetValue(sessionId, out var chat)) return null;
        var seen = context.Events.After(sessionId, 0).Events;
        if (LimitHeld.Of(seen) is not { } held) return null;

        var json = await context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var listed = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, sessionId, StringComparison.Ordinal));
        if (listed is null
            || WordsNever.Judge(listed, WordsNever.LastHere(json, listed.Quest), WordsNever.ContinuedIn(json, sessionId)) is not null)
        {
            return null;
        }

        if (ServiceClient.ReadRecord(json, sessionId) is not { Kind: "chat", Teammate: false, Ask: null, Adapter: { Length: > 0 } ranOn } prior
            || prior.Tree is not { Length: > 0 } tree)
        {
            return null;
        }

        var isHelp = WordsNever.IsHelp(listed);
        AgentDoor agent;
        try
        {
            agent = context.Harnesses.Adapters.Door(ranOn);
        }
        catch (DriverException)
        {
            return null;
        }

        // The account the walk offers now, the cooling one passed: where it is the same, or none, nothing has room, and it stays.
        // An account the person picked is never moved off, at a limit as at a go-on: its conversation waits
        // for it, its words with it, and the look's stage wakes it at that account's reset.
        var picked = context.Conversations.PickedOn(sessionId, prior.Profile);
        var line = LineKeys.Of(sessionId, ServiceClient.ReadRecords(json)).Key;
        // The line's limits rule's wait or ask, or its moves bound, keeps it on its own account before any walk.
        if (!picked && LimitRuled.Holds(context, wiring.Of<LimitWaits>(), Rules ??= ConversationRules.Of(context), prior, sessionId, line, chat, ranOn) is { } ruled)
        {
            return ruled;
        }

        var workspace = picked || isHelp ? prior.Workspace : await WorkspaceOfAsync(prior.Repository, ct).ConfigureAwait(false);
        // The one walk, as the ended move has it: the account judged here is the one the new part opens on,
        // handed the selection rather than asking the roster again, so a second walk neither names another account nor counts a start
        // never made. Ask Daoris's room places itself through its own walk, which counts, so this one judges only there.
        var selection = picked
            ? null!
            : await context.Harnesses
                .SelectAsync(agent.Id.Value, config, workspace, null, StartKind.Conversation, ct, prior.ModelOn(ranOn), counts: !isHelp).ConfigureAwait(false);
        Elsewhere? elsewhere = null;
        var answer = picked ? NoRoom : new LineAnswer.Elsewhere(selection, NextHold.Ready);
        if (!picked && (!selection.Allowed || string.Equals(selection.Profile ?? "", prior.Profile ?? "", StringComparison.OrdinalIgnoreCase)))
        {
            // Counted as chosen, and none chosen after all: its count is given back.
            if (selection.Allowed) context.Harnesses.Release(selection.Number);
            // No account of its agent has room, so its line goes on with its actor's next choice on another
            // agent, where the person allowed one; else it waits, said why.
            elsewhere = await ElsewhereAsync(prior, ranOn, isHelp, config, workspace, ct).ConfigureAwait(false);
            answer = NoRoom;
        }

        // The course decides for a limit's end from what is read above. An account the
        // person picked, or the walk's own account, is no room, and another agent's choice is the course's later one; only a move opens, and
        // where the course holds, the conversation waits.
        var course = Decide(sessionId, prior, isHelp, CourseTrigger.LimitEnd, prior.Waiting, picked, answer, elsewhere?.Order, restarted: false);
        Decided?.Invoke(sessionId, course);
        if (picked)
        {
            // The account the person picked is the one whose reset it waits for.
            if (!chat.Turns.Limited) wiring.Of<LimitWaits>().Wait(sessionId, chat, ranOn, prior.Profile, prior.ModelOn(ranOn));
            return new StartedFrom(null, false, null, $"you picked `{prior.Profile}` for `{sessionId}`, so it waits on it.");
        }

        if (elsewhere is { Order: null })
        {
            // It waits for its limit, its words with it, and the look's stage wakes it.
            if (!chat.Turns.Limited)
            {
                context.Note(sessionId, elsewhere.Stays!);
                wiring.Of<LimitWaits>().Wait(sessionId, chat, ranOn, model: prior.ModelOn(ranOn));
            }

            return new StartedFrom(null, false, null, $"no other account of {agent.Id.Value} has room, so `{sessionId}` waits on its own: {elsewhere.Why}");
        }

        // The start the one walk counted, given back below unless the new part opens. Going on another agent, the walk over its actor's other
        // choices only judged: the open walks and counts for itself. Ask Daoris's room places itself, so counts none here.
        var counted = isHelp || elsewhere is not null ? 0 : selection.Number;
        var made = false;
        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!context.GoingOn.TryAdd(sessionId, going.Task))
        {
            context.Harnesses.Release(counted);
            return null;
        }

        var holding = false;
        // The move's record, ended by its phase where the move goes no further before its hand.
        ConversationMove? record = null;
        var toWire = false;
        ConversationMove? carriedRecord = null;
        // The move's reading, started as its prompt's write returns, which ends it in the store before the take is told over.
        var read = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var decided = false;
        // The words said while it waited for its limit, kept on its record, which the move carries after the refused turns'.
        IReadOnlyList<ChatMessage> waited = [];
        try
        {
            // Decided before its turns are held, so a record a move still stands for, the one it
            // left or the one whose open made it, takes no move now and keeps its words where they are; a store that will not take the
            // decision moves nothing, said.
            var begun = await Moves.BeginAsync(Decision(sessionId, held, seen.Count, isHelp || elsewhere is not null ? null : selection.Profile), ct)
                .ConfigureAwait(false);
            if (begun.Refused is { } unwritten)
            {
                context.Note(sessionId, LimitNotes.NotMoved(unwritten));
                return new StartedFrom(null, false, null, unwritten);
            }

            if (begun.Move is not { } move) return null;
            record = move;
            decided = true;

            // Never mid-turn: a word said meanwhile started its turn first, and that turn's end judges again; the old part went on before
            // anything began, so the move closes superseded. Where the host's open moves the old part's words, one said while
            // held is kept on the record, which the open below moves.
            var keeps = await context.Service.Opens.KeepsAsync(ct).ConfigureAwait(false);
            if (chat.Turns.Limited)
            {
                if (chat.Turns.HoldLimited(keeps) is not { } heldWords)
                {
                    move.Superseded();
                    record = null;
                    return null;
                }

                waited = heldWords;
            }
            else if (!chat.Turns.Hold(keeps))
            {
                move.Superseded();
                record = null;
                return null;
            }

            holding = true;

            // The brief of its line before the refused turns, which go after it as the person's words, so they are told once.
            var brief = await ContextBriefs.ComposeAsync(
                    context.Service, context.Events, context.Home, sessionId, HandOverWhy.Limit, conversation: true, ct: ct, before: held.Before)
                .ConfigureAwait(false);
            var high = RotatedOpening.ContextOf(null, context.Events, sessionId);

            // The old part ends first: the new one holds its tree, or its room.
            await handOver.EndForHandOverAsync(sessionId).ConfigureAwait(false);
            chat.Turns.Release();
            holding = false;

            // The old part's end, then the open asked under the key it is sent with, each before
            // what it fences; a write the store refuses opens nothing, and the words stay on the old record.
            var key = MoveWords.ChatKey();
            if ((move.OldEnded() ?? move.Opening(key)) is { } unasked)
            {
                context.Note(sessionId, LimitNotes.NotMoved(unasked));
                return new StartedFrom(null, false, null, unasked);
            }

            // Onto another account of its own agent its conversation is carried there, its files whole now its old part has ended.
            var fence = new MoveFence(move);
            var plan = isHelp || elsewhere is not null || !await context.Service.Opens.MovesAsync(ct).ConfigureAwait(false)
                ? null
                : ChatLimitCarry.Planned(context, agent, prior, selection, tree);
            var carrier = plan is CarryPlan.Carried carried
                ? new ChatLimitCarry(
                    wiring, agent, prior, selection, carried, held.Words, [.. waited.Where(word => !word.Kept || word.Id is null)], held.Before, move, fence, read)
                : null;
            var from = new ContinuedFrom(sessionId, HandOverWhy.Limit, HandOverBy.Daoris);
            // The waiting words leave the old record by their ids once the new part's turn is on the wire, named as taken by it.
            List<string> waitedIds = [.. waited.Where(word => word.Kept && word.Id is not null).Select(word => word.Id!)];
            // The open moves the words the turns held and every word of the person's kept since, one said to the old
            // part after its end among them, in its own step, so none falls between the end and the open.
            var moves = MovedWords.Asked(sessionId, waitedIds, waitedIds) with { Opens = key };
            var started = isHelp
                ? await HelpStartAsync(prior, agent, config, onEnded, ct, elsewhere?.Order).ConfigureAwait(false)
                // On the account the walk judged, so its opening and `account.rotated` name the cooling one passed and what it carries;
                // on another agent, by its actor's walk over its other choices, told the earlier maker's trailer.
                : elsewhere is { Order: { } order }
                    ? await runner.StartAsync(
                            prior.Repository!, agent.Id.Value, config, onEnded, profile: null, ct: ct, tree: tree, from: from, order: order,
                            leftBy: LeftBy(context.Harnesses.Adapters, prior, agent), moves: moves)
                        .ConfigureAwait(false)
                    : await runner.StartAsync(
                            prior.Repository!, agent.Id.Value, config, onEnded, profile: null, ct: ct, tree: tree, from: from, settled: selection,
                            moves: moves, carries: carrier)
                        .ConfigureAwait(false);
            if (started.SessionId is not { } to)
            {
                carrier?.Withdraw();
                context.Note(sessionId, LimitNotes.NotMoved(started.Message));
                return new StartedFrom(null, false, null, started.Message);
            }

            made = true;
            move.Answered();
            if (elsewhere is { Order: [var other, ..] }) LimitRuled.Moved(context, prior, line, other);

            // The open's answer, every word it moved by its id on the new part, read whatever
            // `Moves` says; then `delivering` and the prompt's mark, before any word is marked attempted or handed. A write the store refuses
            // makes no hand, and the moved words wait on the new part, never attempted. Where an older build has continued the new part,
            // nothing is delivered there and the move is held for the person.
            var fenced = await fence.AskAsync(to, started.Moved).ConfigureAwait(false);
            if (carrier is { Resumed: true })
            {
                toWire = true;
                carriedRecord = record;
                return new StartedFrom(to, true, null, $"`{sessionId}` went on in its own conversation, carried to conversation `{to}` on the next account with your words.");
            }

            // The agent and account the new part opened on: the walk's, which is another agent where its own had no account left.
            var opened = await context.Service.RecordAsync(to, ct).ConfigureAwait(false);
            var account = opened?.Profile;
            var onto = opened?.Adapter is { Length: > 0 } went ? went : agent.Id.Value;
            var opening = HandOverNotes.MovedFrom(sessionId, ranOn, prior.Profile, HandOverWhy.Limit, high, brief.Text.Length);
            var words = held.Words;
            var refusedFiles = words.SelectMany(word => ChatFiles.Kept(context.Home, sessionId, word.Files ?? [])).ToList();
            ChatMessage message;
            IReadOnlyList<SessionEvent>? shown = null;
            if (MovedWords.Moving(started))
            {
                // The open moved the waiting words: the delivery is the new part's, those this process held alone after them, and they leave
                // the new part once its turn is on the wire.
                var moved = fenced is null ? await HandedAsync(sessionId, to, started, ct).ConfigureAwait(false) : null;
                List<ChatMessage> unkept = [.. waited.Where(word => !word.Kept || word.Id is null)];
                List<SaidWordView> handed = [.. moved?.Words ?? []];
                // The handed words' `sent` as the write returns, by their transfers on the new part; then
                // their take; then the one reading, which closes the move delivered only once every word it moved has `sent`.
                List<string> transfers = moved is null
                    ? []
                    : [.. started.Moved.Where(word => moved.Ids.Contains(word.Id, StringComparer.Ordinal)).Select(word => word.Transfer)];
                message = new ChatMessage(
                    string.Join("\n\n", [.. words.Select(word => word.Text), .. handed.Select(word => word.Text), .. unkept.Select(word => word.Text)]),
                    [.. refusedFiles, .. moved?.Files ?? [], .. unkept.SelectMany(word => word.Files)])
                {
                    Brief = brief, TakenOver = opening,
                    Delivered = () =>
                    {
                        move.Wire(transfers);
                        read.TrySetResult(Task.Run(async () =>
                        {
                            if (moved is { Words.Count: > 0 } taking) await taking.TakeAsync(context.Service).ConfigureAwait(false);
                            await move.ReadAsync().ConfigureAwait(false);
                        }));
                    },
                };
                shown = Shown(words, handed, unkept);
            }
            else
            {
                var files = refusedFiles.Concat(waited.SelectMany(word => word.Files)).ToList();
                var text = string.Join("\n\n", [.. words.Select(word => word.Text), .. waited.Select(word => word.Text)]);
                message = new ChatMessage(text, files)
                {
                    Brief = brief, TakenOver = opening,
                    // An older host moved nothing: a move of no word, its prompt's outcome written as the write returns.
                    Delivered = () =>
                    {
                        move.Wire([]);
                        read.TrySetResult(Task.Run(async () =>
                        {
                            if (waitedIds.Count > 0) await TakenOffAsync(sessionId, waitedIds, to).ConfigureAwait(false);
                            await move.ReadAsync().ConfigureAwait(false);
                        }));
                    },
                };
            }

            // A text door writes as it is handed, and tells no move's message of no moved word that it wrote: its return is the write's.
            var turned = context.Turned.ContainsKey(to);
            var taken = fenced is null && await handOver.SayHandedOver(to, message, shown).ConfigureAwait(false);
            toWire = taken;
            if (taken && !turned && !move.Wired)
            {
                move.Wire([]);
                read.TrySetResult(move.ReadAsync());
            }

            // Said once where the person wrote, naming the agent it moved to, and in the machine log, accounts by profile name.
            context.Note(sessionId, HandOverNotes.MovedOn(to, onto, account, HandOverWhy.Limit, high, brief.Text.Length));
            if (ChatLimitCarry.NotCarried(plan) is { } notCarried) context.Note(to, CarriedNotes.NotCarried(notCarried));
            context.Service.AccountSaid(
                Continuations.Moved(to, sessionId, ranOn, onto, prior.Profile, account, HandOverWhy.Limit, briefed: true, carry: ChatLimitCarry.NotCarried(plan)));
            if (!taken)
            {
                context.Note(to, HandOverNotes.Unsaid());
                return new StartedFrom(to, false, null, $"conversation `{to}` opened on the next account, but what Daoris kept of `{sessionId}` did not reach it.");
            }

            return new StartedFrom(to, true, null, elsewhere is null
                ? $"`{sessionId}` went on in conversation `{to}` on the next account, handed Daoris's brief and your words."
                : $"`{sessionId}` went on in conversation `{to}` with {onto}, its role's next choice, handed Daoris's brief and your words.");
        }
        finally
        {
            if (!made) context.Harnesses.Release(counted);
            if (holding) chat.Turns.Release();
            // A move that went no further than its hand is ended by its phase, its prompt known unwritten.
            if (record is not null && !toWire) await record.EndAsync().ConfigureAwait(false);
            context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
            if (decided) await TellEndedAsync(sessionId, toWire, read, carriedRecord).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The move is over, its record ended in the store: once its reading has finished, which a door that never says it wrote
    /// bounds, the take is told over, so the words the person said while it stood and the shell held go on to the line's head.
    /// </summary>
    /// <param name="unwritten">A carried move's record, wired before its run's first prompt: ended by its phase where that prompt is not written within the bound.</param>
    internal async Task TellEndedAsync(string sessionId, bool wired, TaskCompletionSource<Task> read, ConversationMove? unwritten = null)
    {
        if (wired)
        {
            if (await Task.WhenAny(read.Task, Task.Delay(handOver.WireWait)).ConfigureAwait(false) == read.Task)
            {
                await (await read.Task.ConfigureAwait(false)).ConfigureAwait(false);
            }
            else if (unwritten is not null)
            {
                await unwritten.EndAsync().ConfigureAwait(false);
            }
        }

        handOver.Ended(sessionId);
    }

    /// <summary>
    /// The live move as the course decided it: a conversation's, by the brief, for the limit, asked by Daoris;
    /// its brief's recipe bound before the refused turns, whose words are read within the events the decision saw.
    /// </summary>
    private static MoveDecision Decision(string sessionId, LimitHeld held, int seen, string? account) =>
        new(sessionId, MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris)
        {
            Account = account, RecipeWhy = HandOverWhy.Limit, RecipeBound = held.Before, RefusedBound = seen,
        };

    private EndedLimitMove? _ended;

    private EndedLimitMove Ended => _ended ??= new EndedLimitMove(wiring, this);

    public Task<StartedFrom?> MoveEndedAsync(
        string sessionId, ContinueReason? lost, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default,
        bool holding = false, bool restarted = false, bool switching = false) =>
        Ended.MoveEndedAsync(sessionId, lost, config, onEnded, ct, holding, restarted, switching);

    internal void RaiseDecided(string session, LineCourse course) => Decided?.Invoke(session, course);

    /// <summary>
    /// What a limit's move shows on the new part where its open moved the words: the refused turns' words as the
    /// person's, as one, since they wait on no record and carry no id; then each moved word by its own event under its id; then any word this
    /// process alone held, as one.
    /// </summary>
    internal static IReadOnlyList<SessionEvent> Shown(
        IReadOnlyList<SessionEvent> refused, IReadOnlyList<SaidWordView> moved, IReadOnlyList<ChatMessage> unkept)
    {
        List<SessionEvent> shown = [];
        if (refused.Count > 0)
        {
            List<string> names = [.. refused.SelectMany(word => word.Files ?? [])];
            shown.Add(new SessionEvent
            {
                Kind = SessionEventKind.User, Origin = "person", Text = string.Join("\n\n", refused.Select(word => word.Text)),
                Files = names.Count > 0 ? names : null,
            });
        }

        shown.AddRange(moved.Select(MovedWords.Of));
        if (unkept.Count > 0)
        {
            List<string> names = [.. unkept.SelectMany(word => word.Files.Select(file => file.Name))];
            shown.Add(new SessionEvent
            {
                Kind = SessionEventKind.User, Origin = "person", Text = string.Join("\n\n", unkept.Select(word => word.Text)),
                Files = names.Count > 0 ? names : null,
            });
        }

        return shown;
    }

    /// <summary>
    /// The words the open moved to the new part, as it is handed them: read there, their files copied, and marked
    /// attempted where this runner holds the part, so a hand that cannot be made marks nothing; null where the new part's record did not read.
    /// </summary>
    internal async Task<MovedWords?> HandedAsync(string from, string to, ChatStart started, CancellationToken ct)
    {
        var moved = await MovedWords.OnAsync(context.Service, context.Home, from, to, started.Moved, ct).ConfigureAwait(false);
        return moved is not null && context.Holds(to) ? await moved.AttemptedAsync(context.Service).ConfigureAwait(false) : moved;
    }

    /// <summary>
    /// The words the move named with the person's words the old record holds now that it does not name yet, after its own in the
    /// record's order: a word kept between the decision and the go-on is in neither part otherwise. <paramref name="accountMove"/> is the move
    /// off an account that cools, which carries Daoris's own words that state a fact too.
    /// </summary>
    internal static List<string> WithLater(IReadOnlyList<string> named, PriorSession now, bool accountMove) =>
        [.. named, .. now.Waiting
            .Where(word => (accountMove ? DaorisWords.NewPartCarries(word) : word.Persons) && word.Id is { } id && !named.Contains(id, StringComparer.Ordinal))
            .Select(word => word.Id!)];

    /// <summary>Words off a record by their ids, named as taken by <paramref name="by"/>: whether the service took them.</summary>
    internal async Task<bool> TakenOffAsync(string sessionId, IReadOnlyList<string> ids, string? by)
    {
        if (ids.Count == 0) return true;
        try
        {
            return (await context.Service.TakenAsync(sessionId, ids, by).ConfigureAwait(false)).Ok;
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Ask Daoris's new part, in its workspace's room: placed as a press's is, the walk choosing its account and the log's
    /// <c>account.rotated</c> naming the part it carries; its room as the go-on finds it; its link kept in the room's history.
    /// </summary>
    /// <param name="order">
    /// Its actor's choices on other agents, walked as a new conversation's are, each judged by its access under
    /// the mode standing now; null places it on <paramref name="agent"/>, as the move to its next account does.
    /// </param>
    internal async Task<ChatStart> HelpStartAsync(
        PriorSession prior, AgentDoor agent, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct,
        IReadOnlyList<ActorEffective>? order = null)
    {
        var placed = await runner.PlaceHelpAsync(agent.Id.Value, config, prior.Workspace, ct, carries: prior.Session, order: order).ConfigureAwait(false);
        if (placed is not { SessionId: { } id, Adapter: { } resolved, Selection: { } selection }) return new ChatStart(null, placed.Message);
        try
        {
            new ConversationFacts(context.Home).StartedFrom(id, prior.Session, HelpConversations.Brief);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // Its history names no earlier conversation; its brief still says which it continues.
        }

        // The new part is about what the one it carries is about, in the same room.
        var roomIn = KnowledgeRoomPaths.GoesOnIn(context.Home, prior.Workspace, prior.Tree);
        help.CarryFocus(prior.Session, id, roomIn);
        var (room, reads) = await help.HelpRoomNowAsync(id, roomIn, ct).ConfigureAwait(false);
        return await runner.RunAsync(
                prior.Actor ?? "ask-daoris", id, placed.Message, new ChatAgent(context.Harnesses.Adapters, resolved), selection, config,
                runner.HelpPlace(room, reads, prior.Workspace, help.FocusOf(id, roomIn)), onEnded, ct,
                chosen: placed.Chosen)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Where a conversation whose agent has no account left goes on: its actor's effective order walked afresh
    /// as at a start, without the out agent's entries, whose accounts are its. Nowhere for a conversation on the person's pick, which
    /// never goes on with another agent, for an actor with no choice on another agent, and where
    /// none of those choices can run now: each said in its conversation as the reason it waits.
    /// </summary>
    internal async Task<Elsewhere> ElsewhereAsync(
        PriorSession prior, string ranOn, bool isHelp, DriverConfig config, string? workspace, CancellationToken ct)
    {
        if (prior.Choice == "pick") return new(null, LimitNotes.Picked(ranOn), $"`{ranOn}` is the agent you picked, so it never goes on with another.");

        var actor = prior.Actor ?? (isHelp ? "ask-daoris" : "chat");
        // The role bound to its start, not the seeded role of its work: an added role taking Chat in this workspace has
        // its own choices. Its order as a start would arrange it now, the policy's passes (a budget, a work) left out.
        var role = RoleOrders.Bound(context.Conversations.RoleOf(prior.Session)?.Role, actor, RoleCatalog.ReadHome(context.Home), new RolePlace(workspace));
        IReadOnlyList<ActorEffective> order;
        try
        {
            order = RoleOrders.Arranged(context.Harnesses.StaffFor(context.Home), role, actor, config);
        }
        catch (ActorUnreadableException error)
        {
            return new(null, LimitNotes.NoOtherChoice(ranOn, error.Message), error.Message);
        }

        List<ActorEffective> others = [.. order.Where(entry => !string.Equals(entry.Agent, ranOn, StringComparison.OrdinalIgnoreCase))];
        if (others.Count == 0)
        {
            var none = $"{ActorWords.Name(actor)} has no choice on another agent.";
            return new(null, LimitNotes.NoOtherChoice(ranOn, none), none);
        }

        // Judged only: the new part's own walk over these choices counts the start it opens on, once.
        var walk = await runner.WalkAsync(actor, others, config, workspace, null, ct, counts: false).ConfigureAwait(false);
        return walk.Chosen is null ? new(null, LimitNotes.NoOtherChoice(ranOn, walk.Refusal!), walk.Refusal!) : new(others, null, "");
    }

    /// <summary>The part it moves from, as its record says it ran: its harness, that harness's maker and its models.</summary>
    internal static EarlierMaker LeftBy(ProviderRegistry adapters, PriorSession prior, AgentDoor ran) =>
        new(ran.Id.Value, adapters.AgentNamed(ran.Id.Value)?.Maker, prior.Ran?.Models ?? []);

    /// <summary>What <see cref="ElsewhereAsync"/> came to: the choices to walk, or the line its conversation waits under and why.</summary>
    internal sealed record Elsewhere(IReadOnlyList<ActorEffective>? Order, Noted? Stays, string Why);

    /// <summary>The workspace a repository's chat runs in, by the registry; null where the service does not name one.</summary>
    internal async Task<string?> WorkspaceOfAsync(string? repository, CancellationToken ct)
    {
        var registry = await context.Service.RegistryAsync(ct).ConfigureAwait(false);
        return registry.FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;
    }
}

