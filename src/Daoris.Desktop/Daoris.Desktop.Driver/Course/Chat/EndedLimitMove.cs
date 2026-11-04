namespace Daoris.Driver;

/// <summary>The limit move of a conversation that has already ended: no live turn to hold and no part to end. It reaches its siblings through the wiring at run time.</summary>
internal sealed class EndedLimitMove(IExecutorWiring wiring, LimitMove move)
{
    private ChatContext context => wiring.Context;

    private IChatRunning runner => wiring;

    private ChatHandOver handOver => wiring.Of<ChatHandOver>();

    private IChatHelping help => wiring.Of<IChatHelping>();

    private LimitMove owner => move;

    private ConversationMoves Moves => owner.Moves;

    /// <summary>
    /// 🔴 The same move for a conversation its limit holds that has already ended (LIMITRESUME1c, D206 §3.5, B3, F5): <see cref="MoveAsync"/>'s
    /// body with no live turn to hold and no part to end. To the walk's next account where another frees first; or, where its own
    /// conversation is proven lost (<paramref name="lost"/>), on whichever the walk gives, its own account allowed for that reason
    /// alone (relaxing MIXAGENT1a-i's refusal of the same account). Handed the brief of its line before the refused turns, then those
    /// turns' words, then the words said while it waited, each once.
    /// </summary>
    /// <remarks>
    /// <b>One durable owner</b> (FOUNDATION1f-vi part a, the foundation design §3.3, §13.4, §13.5): the move keeps one record in the store
    /// (<see cref="Moves"/>), decided at <c>old-ended</c> before the new part opens, an ended record having no part to end, and no
    /// <c>&lt;s&gt;.limit-move.json</c> box is written beside it: an older build recovers the move from the ledger alone, as the live move's.
    /// Its phases are written before what each fences, as <see cref="MoveAsync"/>'s are; the waiting words leave the old record, or the new
    /// part, only at delivery, when the new part's turn is on the wire. A shell that ends between leaves the move to the come-up's take-up
    /// (<see cref="ConversationMoves.ComeUpAsync"/>), and a box an older build wrote is imported there once, which is the course's
    /// <c>Never</c> for its record until then: a box in place keeps this move off it.
    /// </remarks>
    /// <param name="holding">Whether the caller holds the session's going-on already (a go-on refused before its spawn).</param>
    /// <param name="switching">
    /// LIMITSWITCH2 (D137's note of 2026-10-25): the guard widened from a record a limit ended to a record whose own account is cooling while
    /// another account of its agent is ready, the words going on there now, the reason <c>account</c>. The caller has judged that: an ended
    /// conversation's go-on, or the driver's closed quest's session, which this goes on in as a conversation in its tree. Never Ask Daoris's
    /// room, which goes elsewhere only at a limit (D205 §3.5(e)).
    /// </param>
    /// <returns>Where it went on; null where nothing was judged to move.</returns>
    public async Task<StartedFrom?> MoveEndedAsync(
        string sessionId, ContinueReason? lost, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default,
        bool holding = false, bool restarted = false, bool switching = false)
    {
        var boxes = new LimitBoxes(context.Home);
        if (context.Closing || context.Holds(sessionId) || boxes.Read(sessionId) is not null) return null;
        var events = context.Events.After(sessionId, 0).Events;

        var json = await context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        if (ServiceClient.ReadRecord(json, sessionId) is not { Teammate: false, Ask: null, Adapter: { Length: > 0 } ranOn } prior
            || prior.Tree is not { Length: > 0 } tree || WordsNever.ContinuedIn(json, sessionId) is not null)
        {
            return null;
        }

        // FOUNDATION1g part c: the ended record's flag decides; its events give the words, and decide alone for a record an older build ended.
        var held = LimitHeld.Of(events, prior.Limit);
        if (held is null && !restarted && !switching) return null;
        var moveWhy = ComposedWhy(held is not null, switching);
        if (!await EndedLineAsync(prior, json, switching, ct).ConfigureAwait(false)) return null;

        var listed = SessionRecords.Parse(json).First(each => string.Equals(each.Id, sessionId, StringComparison.Ordinal));
        var isHelp = WordsNever.IsHelp(listed);
        if (isHelp && switching) return null;
        ISessionAdapter agent;
        try
        {
            agent = context.Harnesses.Adapters.Resolve(ranOn);
        }
        catch (DriverException)
        {
            return null;
        }

        // FOUNDATION1a-ii (D205 §3.5(d)): an account the person picked is never moved off. Its words wait for it, as the rule says. A
        // conversation proven lost (`lost`) has no resume to keep, but its account is not the resume (PICKLOST1): its new part is asked
        // for the picked account alone, starts afresh there once it is ready and keeps the pick, and waits while it cools, so the walk
        // below is for a conversation with no pick.
        var onPick = context.Conversations.PickedOn(sessionId, prior.Profile);
        // FOUNDATION1h branch 5 (the foundation design §3.1): each entry has its own trigger, a limit's end, a restart or a conversation proven
        // lost (the conversation lost), and a switch off a cooling account (the words kept). The executor reads the walk, and the course
        // decides; where it holds, the conversation waits as it did, and where it answers a move the executor opens as before.
        var trigger = lost is not null || restarted ? CourseTrigger.ConversationLost
            : held is not null ? CourseTrigger.LimitEnd
            : CourseTrigger.WordsKept;
        LimitMove.Elsewhere? elsewhere = null;
        HarnessSelection selection = null!;
        LineAnswer answer = LimitMove.NoRoom;
        string? workspace = null;
        if (!(onPick && lost is null))
        {
            workspace = isHelp ? prior.Workspace : await move.WorkspaceOfAsync(prior.Repository, ct).ConfigureAwait(false);
            // The one walk (LIMITSWITCH2): the account judged here is the one the new part opens on, which is handed the selection rather than
            // asking the roster again, so a second walk neither names another account nor counts a start never made.
            // Ask Daoris's room places itself through its own walk, which counts, so this one judges only there.
            selection = await context.Harnesses
                .SelectAsync(agent.Name, config, workspace, onPick ? prior.Profile : null, StartKind.Conversation, ct, counts: !isHelp)
                .ConfigureAwait(false);
            var same = string.Equals(selection.Profile ?? "", prior.Profile ?? "", StringComparison.OrdinalIgnoreCase);
            if (!selection.Allowed || (lost is null && same))
            {
                // Counted as chosen, and none chosen after all: its count is given back.
                if (selection.Allowed) context.Harnesses.Release(selection.Number);
                // FOUNDATION1a-iii (O2-13, as the live move has it, LIMITGOON2, D205 §3.5(e)): a limit ended it and every account of its agent
                // cools, so it goes on with its actor's next choice on another agent where the person allowed one; else it waits. Never for a
                // closed quest's session, or a go-on outside a limit, whose going elsewhere is the owner's to say (the design's §8).
                if (!onPick && held is not null && !switching)
                {
                    elsewhere = await move.ElsewhereAsync(prior, ranOn, isHelp, config, workspace, ct).ConfigureAwait(false);
                }
            }
            else
            {
                answer = same ? new LineAnswer.Resume() : new LineAnswer.Elsewhere(selection, NextHold.Ready, Switched: switching);
            }
        }

        var course = move.Decide(
            sessionId, prior, isHelp, trigger, prior.Waiting, onPick, answer, elsewhere?.Order, restarted, limited: held is not null, ended: true);
        move.RaiseDecided(sessionId, course);
        if (onPick && answer == LimitMove.NoRoom)
        {
            return new StartedFrom(null, false, LimitMove.OnPick, $"you picked `{prior.Profile}` for `{sessionId}`, so it waits on it.");
        }

        if (answer == LimitMove.NoRoom && elsewhere?.Order is null)
        {
            return new StartedFrom(null, false, null, $"no other account of {agent.Name} has room, so `{sessionId}` waits on its own.");
        }

        // The start the one walk counted, given back unless the new part opens; going on another agent, the walk over its actor's other
        // choices only judged and the open counts for itself (Ask Daoris's room places itself, so this counts none).
        var counted = isHelp || elsewhere is not null ? 0 : selection.Number;
        var opened = false;
        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!holding && !context.GoingOn.TryAdd(sessionId, going.Task))
        {
            context.Harnesses.Release(counted);
            return null;
        }

        // FOUNDATION1f-vi part a: the move's record, ended by its phase where the move goes no further before its hand.
        ConversationMove? record = null;
        var toWire = false;
        var read = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var decided = false;
        try
        {
            // FOUNDATION1f-vi part a (§3.3, §13.4 step 1): decided at `old-ended` before anything is asked, so a record a move still stands for
            // takes no move now and keeps its words where they are; a store that will not take the decision moves nothing, said.
            var before = held?.Before ?? events.Count;
            var begun = await Moves.BeginAsync(
                    EndedDecision(sessionId, moveWhy, before, events.Count, isHelp || elsewhere is not null ? null : selection.Profile), ct)
                .ConfigureAwait(false);
            if (begun.Refused is { } unwritten)
            {
                context.Note(sessionId, LimitNotes.NotMoved(unwritten));
                return new StartedFrom(null, false, null, unwritten);
            }

            if (begun.Move is not { } move) return null;
            record = move;
            decided = true;

            // The words said while it waited, the person's: Daoris's limit word never goes on, and was taken off before a refusal. Where its own
            // account cools and the part opens for that (LIMITSWITCH3), Daoris's own words that state a fact go too, said as Daoris's.
            List<string> said = [.. prior.Waiting.Where(word => (switching ? DaorisWords.NewPartCarries(word) : word.Persons) && word.Id is not null).Select(word => word.Id!)];

            // The open asked under the key it is sent with, so a retried open whose answer was lost reuses it (§13.4 step 3); a write the store
            // refuses opens nothing, and the words stay on the old record.
            var key = MoveWords.ChatKey();
            if (move.Opening(key) is { } unasked)
            {
                context.Note(sessionId, LimitNotes.NotMoved(unasked));
                return new StartedFrom(null, false, null, unasked);
            }

            var from = new ContinuedFrom(sessionId, moveWhy, HandOverBy.Daoris);
            // FOUNDATION1d-ii part 4: the open moves the words named and every word of the person's kept since this read, in its own step.
            var moves = MovedWords.Asked(sessionId, said, prior.Waiting.Select(word => word.Id)) with { Opens = key };
            var started = isHelp
                ? await owner.HelpStartAsync(prior, agent, config, onEnded, ct, elsewhere?.Order).ConfigureAwait(false)
                : elsewhere is { Order: { } order }
                    ? await runner.StartAsync(
                            prior.Repository!, agent.Name, config, onEnded, profile: null, ct: ct, tree: tree, from: from, order: order,
                            leftBy: LimitMove.LeftBy(prior, agent), moves: moves)
                        .ConfigureAwait(false)
                    : await runner.StartAsync(
                            prior.Repository!, agent.Name, config, onEnded, profile: null, ct: ct, tree: tree, from: from, settled: selection,
                            moves: moves)
                        .ConfigureAwait(false);
            if (started.SessionId is not { } to)
            {
                context.Note(sessionId, LimitNotes.NotMoved(started.Message));
                return new StartedFrom(null, false, null, started.Message);
            }

            opened = true;
            move.Answered();

            // FOUNDATION1f-vi part a (§13.4 steps 2 and 3, as the live move's): the open's answer, every word it moved by its id on the new
            // part, the line's head read before anything is handed, then `delivering` and the prompt's mark. A write the store refuses makes
            // no hand, and the moved words wait on the new part, never attempted.
            var fenced = move.Opened(to, started.Moved);
            if (fenced is null && await move.SupersededAsync().ConfigureAwait(false)) fenced = "the line went on past its new part";
            fenced ??= move.Delivering();

            var refused = held?.Words ?? [];
            ChatMessage message;
            IReadOnlyList<SessionEvent>? shown = null;
            if (MovedWords.Moving(started))
            {
                // The open moved the words, any kept since the decision among them: they wait on the new part until they are delivered, and the
                // record left holds none to read again. The delivery is the new part's moved words, those marked attempted for the hand; the
                // rest wait there for its own go-on. Each handed word's `sent` as the turn's write returns, then the take, then the one reading.
                var moved = fenced is null ? await owner.HandedAsync(sessionId, to, started, ct).ConfigureAwait(false) : null;
                List<string> transfers = moved is null
                    ? []
                    : [.. started.Moved.Where(word => moved.Ids.Contains(word.Id, StringComparer.Ordinal)).Select(word => word.Transfer)];
                message = (await ComposeAsync(sessionId, moveWhy, before, refused, prior, moved?.Words ?? [], to, ct).ConfigureAwait(false)) with
                {
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
                shown = LimitMove.Shown(refused, moved?.Words ?? [], []);
            }
            else
            {
                // LIMITWORD1: an older host moved nothing, so the record is read again at the go-on, where the new part's record takes the words
                // said after it; a word kept since the decision goes on with the rest, after its own, and leaves the old record named as taken
                // by the new part once it is on the wire. A move of no word, its prompt's outcome written as the write returns.
                var now = await context.ReadAgainAsync(sessionId, ct).ConfigureAwait(false) ?? prior;
                List<string> waitedIds = LimitMove.WithLater(said, now, lost is null && held is null && switching);
                message = (await ComposeAsync(sessionId, moveWhy, before, refused, now, Waited(waitedIds, now), sessionId, ct).ConfigureAwait(false)) with
                {
                    Delivered = () =>
                    {
                        move.Wire([]);
                        read.TrySetResult(Task.Run(async () =>
                        {
                            if (waitedIds.Count > 0) await owner.TakenOffAsync(sessionId, waitedIds, to).ConfigureAwait(false);
                            await move.ReadAsync().ConfigureAwait(false);
                        }));
                    },
                };
            }

            // The agent and account the new part opened on: the walk's, which is another agent where its own had no account left.
            var landed = await context.Service.RecordAsync(to, ct).ConfigureAwait(false);
            var account = landed?.Profile;
            var onto = landed?.Adapter is { Length: > 0 } went ? went : agent.Name;
            if (lost is not null) context.Note(sessionId, LimitNotes.ResumeRefused(lost));
            context.Note(sessionId, HandOverNotes.MovedOn(to, onto, account, moveWhy, null, message.Brief?.Text.Length ?? 0));
            context.Service.AccountSaid(
                Continuations.Moved(to, sessionId, ranOn, onto, prior.Profile, account, moveWhy, briefed: true));

            // A text door writes as it is handed, and tells no message of no moved word that it wrote: its return is the write's.
            var turned = context.Turned.ContainsKey(to);
            var taken = fenced is null && await handOver.SayHandedOver(to, message, shown).ConfigureAwait(false);
            toWire = taken;
            if (taken && !turned && !move.Wired)
            {
                move.Wire([]);
                read.TrySetResult(move.ReadAsync());
            }

            if (!taken)
            {
                context.Note(to, HandOverNotes.Unsaid());
                return new StartedFrom(to, false, null, $"conversation `{to}` opened on the next account, but what Daoris kept of `{sessionId}` did not reach it.");
            }

            return new StartedFrom(to, true, null, elsewhere is null
                ? $"`{sessionId}` went on in conversation `{to}`, handed Daoris's brief and your words."
                : $"`{sessionId}` went on in conversation `{to}` with {onto}, its actor's next choice, handed Daoris's brief and your words.");
        }
        finally
        {
            if (!opened) context.Harnesses.Release(counted);
            // FOUNDATION1f-vi part a: a move that went no further than its hand is ended by its phase, its prompt known unwritten.
            if (record is not null && !toWire) await record.EndAsync().ConfigureAwait(false);
            if (!holding) context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
            if (decided) await move.TellEndedAsync(sessionId, toWire, read).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The ended move as the course decided it (FOUNDATION1f-vi part a, §3.3): a conversation's, by the brief, asked by Daoris, from an ended
    /// record, so written at <c>old-ended</c>; its brief's recipe bound before the refused turns, whose words are read within the events the
    /// decision saw, and its reason the one its message is composed by.
    /// </summary>
    private static MoveDecision EndedDecision(string sessionId, string why, int before, int through, string? account) =>
        new(sessionId, MoveExecutors.Conversation, MoveHows.Brief, why, HandOverBy.Daoris)
        {
            FromEnded = true, Account = account, RecipeWhy = why, RecipeBound = before, RefusedBound = through,
        };

    /// <summary>
    /// The reason the new part's record continues the old by: <c>limit</c> where a limit held it; <c>retry</c> where its own account cools and
    /// another's is ready (LIMITSWITCH2), as the driven carry-on's switch says it (LIMITSWITCH1: its own conversation could not be resumed);
    /// else <c>resume</c>, its own conversation proven lost.
    /// </summary>
    private static string ComposedWhy(bool limited, bool switching) =>
        limited ? HandOverWhy.Limit : switching ? HandOverWhy.Retry : HandOverWhy.Resume;

    /// <summary>
    /// Whether the record's line is one an ended move goes on in (LIMITSWITCH2): a conversation, or, where its own account cools while
    /// another is ready, a driven record whose quest is closed, which goes on as a conversation in its tree. A session of an open or taken
    /// quest is the driver's own carry-on, never this.
    /// </summary>
    private async Task<bool> EndedLineAsync(PriorSession prior, string json, bool switching, CancellationToken ct)
    {
        if (prior.Kind == "chat") return true;
        if (!switching || prior.Quest is not { } quest) return false;
        var listed = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, prior.Session, StringComparison.Ordinal));
        if (listed is null || WordsNever.Judge(listed, WordsNever.LastHere(json, listed.Quest), null) is not null) return false;
        return await context.Service.FindQuestAsync(quest, ct).ConfigureAwait(false) is { Status: not ("Open" or "Taken") };
    }

    /// <summary>
    /// The ended-record move's message, composed from its decision (FOUNDATION1f-vi part a; Codex 8): Daoris's brief of the line before the
    /// refused turns, then those turns' words, then the words said while it waited, each with its files: <paramref name="waited"/>, as the
    /// record <paramref name="waitedOn"/> holds them, the old record's by the ids the move named, or, where the open moved them
    /// (FOUNDATION1d-ii part 4, O6-F13), the new part's, their files read from its folder.
    /// </summary>
    /// <param name="moveWhy">The decision's reason, which the brief and its opening are composed for.</param>
    /// <param name="before">The decision's recipe bound: how many of the record's events stand before the first refused turn's words.</param>
    /// <param name="refused">The refused turns' words, read within the events the decision saw.</param>
    /// <param name="prior">The old record, whose agent, account and kind the brief and its opening name.</param>
    private async Task<ChatMessage> ComposeAsync(
        string sessionId, string moveWhy, int before, IReadOnlyList<SessionEvent> refused, PriorSession prior, IReadOnlyList<SaidWordView> waited,
        string waitedOn, CancellationToken ct)
    {
        var brief = await ContextBriefs.ComposeAsync(
                context.Service, context.Events, context.Home, sessionId, moveWhy, conversation: prior.Kind == "chat", ct: ct, before: before)
            .ConfigureAwait(false);
        var high = RotatedOpening.ContextOf(null, context.Events, sessionId);
        var opening = HandOverNotes.MovedFrom(sessionId, prior.Adapter ?? "", prior.Profile, moveWhy, high, brief.Text.Length);
        var files = refused.SelectMany(word => ChatFiles.Kept(context.Home, sessionId, word.Files ?? []))
            .Concat(waited.SelectMany(word => ChatFiles.Kept(context.Home, waitedOn, word.Files)))
            .ToList();
        // Daoris's own words that waited (LIMITSWITCH3) are said as Daoris's after the person's, never as theirs.
        List<string> parts = [.. refused.Select(word => word.Text), .. waited.Where(word => !word.Daoris).Select(word => word.Text)];
        if (waited.Where(word => word.Daoris).ToList() is { Count: > 0 } daoris) parts.Add(TargetPrompt.DaorisWaited(daoris));
        return new ChatMessage(string.Join("\n\n", parts), files) { Brief = brief, TakenOver = opening };
    }

    /// <summary>The words named as the old record holds them, in its order: an older host's move, which moved nothing.</summary>
    private static IReadOnlyList<SaidWordView> Waited(IReadOnlyList<string> ids, PriorSession prior) =>
        [.. prior.Waiting.Where(word => word.Id is { } id && ids.Contains(id, StringComparer.Ordinal))];
}
