namespace Daoris.Driver;

/// <summary>
/// A conversation whose turn its account's limit refused goes on, in the same conversation, on the next account (LIMITGOON1; the
/// limit-goes-on design §2; D197, D189 point 3): at that turn's end, a new part of its line on the account of the same agent the walk
/// offers next, the cooling one passed, handed Daoris's brief and then the person's words the refused turns held, its record continuing
/// the old by <c>limit</c>, asked by <c>daoris</c>. A repository's chat opens it in the same tree; Ask Daoris's opens it in its
/// workspace's room, the link kept in the room's history, since the room's record takes no <c>from</c>. Over the runner's context, as
/// <see cref="CurveMove"/> and <see cref="BriefedMove"/> are.
/// </summary>
/// <remarks>
/// <para><b>The carrier is the brief</b> (design §1): the agent's own conversation is in the cooling account's home, which Daoris never
/// reads, copies or writes (D189 point 5), so it cannot resume on the next account.</para>
/// <para><b>Never mid-turn, never during a take.</b> Judged once per turn's end with nothing running, opening or queued, as the curve's
/// move is; the turns are held while it moves, so a word said as it begins either runs its turn first or waits for the new part.</para>
/// <para><b>Where no other account has room</b> it stays where it is and its words with it: the next refused turn tries again. A teammate's
/// record, an intake and a conversation that went on already never move; a conversation the person ended has no refused turn.</para>
/// </remarks>
internal sealed class LimitMove(ChatContext context, ChatRunner runner, ChatHandOver handOver, ChatHelp help)
{
    /// <summary>
    /// The <see cref="StartedFrom.Why"/> of an ended move that left its conversation waiting for the account the person picked (PICKLOST1):
    /// the go-on of a lost conversation reads it, so it waits there rather than going on in the brief's move, which would refuse the same
    /// account and mark the words as unable to go on.
    /// </summary>
    internal const string OnPick = "on-pick";

    /// <summary>What each move came to, by the session it moved from: a test hears it; no person reads it.</summary>
    public event Action<string, StartedFrom>? Moved;

    /// <summary>
    /// Where no other account has room (LIMITRESUME1c, D206 §3.5): the conversation waits for its limit, its words with it, rather than
    /// staying to be refused turn by turn (<see cref="LimitWaits.Wait"/>). Told the session, its turns, the adapter it runs on and, where the
    /// person picked its account, that account, whose reset its wait is (FOUNDATION1a-ii).
    /// </summary>
    internal Func<string, ChatRunner.ITurnedChat, string, string?, bool>? Stays { get; set; }

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
    /// Move the conversation where its last turn was refused by its account's limit (<see cref="LimitHeld"/>); null where nothing was
    /// judged to move: its last turn ended otherwise, it is no conversation of this machine's that runs here, or a move is under way.
    /// </summary>
    public async Task<StartedFrom?> MoveAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default)
    {
        if (context.Closing || context.GoingOn.ContainsKey(sessionId) || new HandOverBox(context.Home).Read(sessionId) is not null) return null;
        if (!context.Turned.TryGetValue(sessionId, out var chat)) return null;
        if (LimitHeld.Of(context.Events.After(sessionId, 0).Events) is not { } held) return null;

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
        ISessionAdapter agent;
        try
        {
            agent = context.Harnesses.Adapters.Resolve(ranOn);
        }
        catch (DriverException)
        {
            return null;
        }

        // The account the walk offers now, the cooling one passed (D197): where it is the same, or none, nothing has room, and it stays.
        // FOUNDATION1a-ii (D205 §3.5(d)): an account the person picked is never moved off, at a limit as at a go-on: its conversation waits
        // for it, its words with it, and the look's stage wakes it at that account's reset.
        if (context.Conversations.PickedOn(sessionId, prior.Profile))
        {
            if (!chat.Turns.Limited) Stays?.Invoke(sessionId, chat, ranOn, prior.Profile);
            return new StartedFrom(null, false, null, $"you picked `{prior.Profile}` for `{sessionId}`, so it waits on it.");
        }

        var workspace = isHelp ? prior.Workspace : await WorkspaceOfAsync(prior.Repository, ct).ConfigureAwait(false);
        // The one walk (FOUNDATION1a-iii, as LIMITSWITCH2 made the ended move): the account judged here is the one the new part opens on,
        // handed the selection rather than asking the roster again, so a second walk neither names another account nor counts a start
        // never made. Ask Daoris's room places itself through its own walk, which counts, so this one judges only there.
        var selection = await context.Harnesses
            .SelectAsync(agent.Name, config, workspace, null, StartKind.Conversation, ct, counts: !isHelp).ConfigureAwait(false);
        Elsewhere? elsewhere = null;
        if (!selection.Allowed || string.Equals(selection.Profile ?? "", prior.Profile ?? "", StringComparison.OrdinalIgnoreCase))
        {
            // Counted as chosen, and none chosen after all: its count is given back.
            if (selection.Allowed) context.Harnesses.Release(selection.Number);
            // LIMITGOON2 (D205 §3.5(e)): no account of its agent has room, so its line goes on with its actor's next choice on another
            // agent, where the person allowed one; else it waits, said why.
            elsewhere = await ElsewhereAsync(prior, ranOn, isHelp, config, workspace, ct).ConfigureAwait(false);
            if (elsewhere.Order is null)
            {
                // LIMITRESUME1c: it waits for its limit, its words with it, and the look's stage wakes it (D206 §3.5).
                if (!chat.Turns.Limited)
                {
                    context.Note(sessionId, elsewhere.Stays!);
                    Stays?.Invoke(sessionId, chat, ranOn, null);
                }

                return new StartedFrom(null, false, null, $"no other account of {agent.Name} has room, so `{sessionId}` waits on its own: {elsewhere.Why}");
            }
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
        // LIMITRESUME1c: the words said while it waited for its limit, kept on its record, which the move carries after the refused turns'.
        IReadOnlyList<ChatMessage> waited = [];
        try
        {
            // Never mid-turn: a word said meanwhile started its turn first, and that turn's end judges again.
            if (chat.Turns.Limited)
            {
                if (chat.Turns.HoldLimited() is not { } heldWords) return null;
                waited = heldWords;
            }
            else if (!chat.Turns.Hold())
            {
                return null;
            }

            holding = true;

            // The brief of its line before the refused turns, which go after it as the person's words, so they are told once.
            var brief = await ContextBriefs.ComposeAsync(
                    context.Service, context.Events, context.Home, sessionId, HandOverWhy.Limit, conversation: true, ct: ct, before: held.Before)
                .ConfigureAwait(false);
            var high = RotatedOpening.ContextOf(null, context.Events, sessionId);

            // The old part ends first (D51): the new one holds its tree, or its room.
            await handOver.EndForHandOverAsync(sessionId).ConfigureAwait(false);
            chat.Turns.Release();
            holding = false;

            var from = new ContinuedFrom(sessionId, HandOverWhy.Limit, HandOverBy.Daoris);
            var started = isHelp
                ? await HelpStartAsync(prior, agent, config, onEnded, ct, elsewhere?.Order).ConfigureAwait(false)
                // On the account the walk judged, so its opening and `account.rotated` name the cooling one passed and what it carries;
                // on another agent (LIMITGOON2), by its actor's walk over its other choices, told the earlier maker's trailer (MAKERTRAIL2).
                : elsewhere is { Order: { } order }
                    ? await runner.StartAsync(
                            prior.Repository!, agent.Name, config, onEnded, profile: null, ct: ct, tree: tree, from: from, order: order,
                            leftBy: LeftBy(prior, agent))
                        .ConfigureAwait(false)
                    : await runner.StartAsync(
                            prior.Repository!, agent.Name, config, onEnded, profile: null, ct: ct, tree: tree, from: from, settled: selection)
                        .ConfigureAwait(false);
            if (started.SessionId is not { } to)
            {
                context.Note(sessionId, LimitNotes.NotMoved(started.Message));
                return new StartedFrom(null, false, null, started.Message);
            }

            made = true;

            // The agent and account the new part opened on: the walk's, which is another agent where its own had no account left.
            var opened = await context.Service.RecordAsync(to, ct).ConfigureAwait(false);
            var account = opened?.Profile;
            var onto = opened?.Adapter is { Length: > 0 } went ? went : agent.Name;
            var opening = HandOverNotes.MovedFrom(sessionId, ranOn, prior.Profile, HandOverWhy.Limit, high, brief.Text.Length);
            var words = held.Words;
            var files = words.SelectMany(word => ChatFiles.Kept(context.Home, sessionId, word.Files ?? [])).Concat(waited.SelectMany(word => word.Files)).ToList();
            var text = string.Join("\n\n", [.. words.Select(word => word.Text), .. waited.Select(word => word.Text)]);
            // The waiting words leave the old record by their ids once the new part's turn is on the wire, named as taken by it.
            List<string> waitedIds = [.. waited.Where(word => word.Kept && word.Id is not null).Select(word => word.Id!)];
            var message = new ChatMessage(text, files)
            {
                Brief = brief, TakenOver = opening,
                Delivered = waitedIds.Count == 0 ? null : () => _ = TakenOffAsync(sessionId, waitedIds, to),
            };
            var taken = await handOver.SayHandedOver(to, message).ConfigureAwait(false);

            // Said once where the person wrote, naming the agent it moved to, and in the machine log, accounts by profile name (D125 point 7).
            context.Note(sessionId, HandOverNotes.MovedOn(to, onto, account, HandOverWhy.Limit, high, brief.Text.Length));
            context.Service.AccountSaid(
                Continuations.Moved(to, sessionId, ranOn, onto, prior.Profile, account, HandOverWhy.Limit, briefed: true));
            if (!taken)
            {
                context.Note(to, HandOverNotes.Unsaid());
                return new StartedFrom(to, false, null, $"conversation `{to}` opened on the next account, but what Daoris kept of `{sessionId}` did not reach it.");
            }

            return new StartedFrom(to, true, null, elsewhere is null
                ? $"`{sessionId}` went on in conversation `{to}` on the next account, handed Daoris's brief and your words."
                : $"`{sessionId}` went on in conversation `{to}` with {onto}, its actor's next choice, handed Daoris's brief and your words.");
        }
        finally
        {
            if (!made) context.Harnesses.Release(counted);
            if (holding) chat.Turns.Release();
            context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    /// <summary>
    /// 🔴 The same move for a conversation its limit holds that has already ended (LIMITRESUME1c, D206 §3.5, B3, F5): <see cref="MoveAsync"/>'s
    /// body with no live turn to hold and no part to end. To the walk's next account where another frees first; or, where its own
    /// conversation is proven lost (<paramref name="lost"/>), on whichever the walk gives, its own account allowed for that reason
    /// alone (relaxing MIXAGENT1a-i's refusal of the same account). Handed the brief of its line before the refused turns, then those
    /// turns' words, then the words said while it waited, each once.
    /// </summary>
    /// <remarks>
    /// <b>One durable owner</b> (Codex 8): the move's <see cref="LimitBoxes"/> box is written before the new part opens, and names it once
    /// it does; the message is composed from the box, and the waiting words leave the old record only at delivery, when the new part's
    /// turn is on the wire, the box with them. A shell that ends between leaves the box for the next come-up (<see cref="TakeUpBoxesAsync"/>).
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
        var held = LimitHeld.Of(events);
        if (held is null && !restarted && !switching) return null;
        var moveWhy = ComposedWhy(held is not null, switching);

        var json = await context.Service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        if (ServiceClient.ReadRecord(json, sessionId) is not { Teammate: false, Ask: null, Adapter: { Length: > 0 } ranOn } prior
            || prior.Tree is not { Length: > 0 } tree || WordsNever.ContinuedIn(json, sessionId) is not null
            || !await EndedLineAsync(prior, json, switching, ct).ConfigureAwait(false))
        {
            return null;
        }

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
        if (onPick && lost is null)
        {
            return new StartedFrom(null, false, OnPick, $"you picked `{prior.Profile}` for `{sessionId}`, so it waits on it.");
        }

        var workspace = isHelp ? prior.Workspace : await WorkspaceOfAsync(prior.Repository, ct).ConfigureAwait(false);
        // The one walk (LIMITSWITCH2): the account judged here is the one the new part opens on, which is handed the selection rather than
        // asking the roster again, so a second walk neither names another account nor counts a start never made.
        // Ask Daoris's room places itself through its own walk, which counts, so this one judges only there.
        var selection = await context.Harnesses
            .SelectAsync(agent.Name, config, workspace, onPick ? prior.Profile : null, StartKind.Conversation, ct, counts: !isHelp)
            .ConfigureAwait(false);
        Elsewhere? elsewhere = null;
        if (!selection.Allowed
            || (lost is null && string.Equals(selection.Profile ?? "", prior.Profile ?? "", StringComparison.OrdinalIgnoreCase)))
        {
            // Counted as chosen, and none chosen after all: its count is given back.
            if (selection.Allowed) context.Harnesses.Release(selection.Number);
            if (onPick)
            {
                return new StartedFrom(null, false, OnPick, $"you picked `{prior.Profile}` for `{sessionId}`, so it waits on it.");
            }

            // FOUNDATION1a-iii (O2-13, as the live move has it, LIMITGOON2, D205 §3.5(e)): a limit ended it and every account of its agent
            // cools, so it goes on with its actor's next choice on another agent where the person allowed one; else it waits. Never for a
            // closed quest's session, or a go-on outside a limit, whose going elsewhere is the owner's to say (the design's §8).
            if (held is not null && !switching)
            {
                elsewhere = await ElsewhereAsync(prior, ranOn, isHelp, config, workspace, ct).ConfigureAwait(false);
            }

            if (elsewhere?.Order is null)
            {
                return new StartedFrom(null, false, null, $"no other account of {agent.Name} has room, so `{sessionId}` waits on its own.");
            }
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

        try
        {
            // The words said while it waited, the person's: Daoris's limit word never goes on, and was taken off before a refusal. Where its own
            // account cools and the part opens for that (LIMITSWITCH3), Daoris's own words that state a fact go too, said as Daoris's.
            List<string> said = [.. prior.Waiting.Where(word => (switching ? DaorisWords.NewPartCarries(word) : word.Persons) && word.Id is not null).Select(word => word.Id!)];
            var box = new LimitMoveBox(
                lost?.Code ?? (held is null && switching ? ContinueWhy.Account : HandOverWhy.Limit), held?.Before ?? events.Count, events.Count, said);
            boxes.Write(sessionId, box);

            var from = new ContinuedFrom(sessionId, moveWhy, HandOverBy.Daoris);
            var started = isHelp
                ? await HelpStartAsync(prior, agent, config, onEnded, ct, elsewhere?.Order).ConfigureAwait(false)
                : elsewhere is { Order: { } order }
                    ? await runner.StartAsync(
                            prior.Repository!, agent.Name, config, onEnded, profile: null, ct: ct, tree: tree, from: from, order: order,
                            leftBy: LeftBy(prior, agent))
                        .ConfigureAwait(false)
                    : await runner.StartAsync(
                            prior.Repository!, agent.Name, config, onEnded, profile: null, ct: ct, tree: tree, from: from, settled: selection)
                        .ConfigureAwait(false);
            if (started.SessionId is not { } to)
            {
                boxes.Clear(sessionId);
                context.Note(sessionId, LimitNotes.NotMoved(started.Message));
                return new StartedFrom(null, false, null, started.Message);
            }

            opened = true;

            // LIMITWORD1: read the record again at the go-on, where the new part's record takes the words said after it; a word kept
            // here since the box was written goes on with the rest, in the box before the message is composed from it.
            var now = await context.ReadAgainAsync(sessionId, ct).ConfigureAwait(false) ?? prior;
            box = Carried(box with { To = to }, now);
            boxes.Write(sessionId, box);
            var message = await ComposeAsync(sessionId, box, now, ct).ConfigureAwait(false);
            // The agent and account the new part opened on: the walk's, which is another agent where its own had no account left.
            var landed = await context.Service.RecordAsync(to, ct).ConfigureAwait(false);
            var account = landed?.Profile;
            var onto = landed?.Adapter is { Length: > 0 } went ? went : agent.Name;
            if (lost is not null) context.Note(sessionId, LimitNotes.ResumeRefused(lost));
            context.Note(sessionId, HandOverNotes.MovedOn(to, onto, account, moveWhy, null, message.Brief?.Text.Length ?? 0));
            context.Service.AccountSaid(
                Continuations.Moved(to, sessionId, ranOn, onto, prior.Profile, account, moveWhy, briefed: true));
            if (!await handOver.SayHandedOver(to, message with { Delivered = () => _ = DeliveredAsync(sessionId, box) }).ConfigureAwait(false))
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
            if (!holding) context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

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
    /// The ended-record move's message, composed from its box (Codex 8): Daoris's brief of the line before the refused turns, then those
    /// turns' words read within the box's bound, then the words said while it waited, by their ids on the old record, each with its files.
    /// </summary>
    private async Task<ChatMessage> ComposeAsync(string sessionId, LimitMoveBox box, PriorSession prior, CancellationToken ct)
    {
        var events = context.Events.After(sessionId, 0).Events;
        var held = LimitHeld.Of([.. events.Take(box.Through)]);
        var refused = held?.Words ?? [];
        var moveWhy = ComposedWhy(held is not null, box.Why == ContinueWhy.Account);
        var waited = prior.Waiting.Where(word => word.Id is { } id && box.Said.Contains(id, StringComparer.Ordinal)).ToList();
        var brief = await ContextBriefs.ComposeAsync(
                context.Service, context.Events, context.Home, sessionId, moveWhy, conversation: prior.Kind == "chat", ct: ct, before: box.Before)
            .ConfigureAwait(false);
        var high = RotatedOpening.ContextOf(null, context.Events, sessionId);
        var opening = HandOverNotes.MovedFrom(sessionId, prior.Adapter ?? "", prior.Profile, moveWhy, high, brief.Text.Length);
        var files = refused.SelectMany(word => ChatFiles.Kept(context.Home, sessionId, word.Files ?? []))
            .Concat(waited.SelectMany(word => ChatFiles.Kept(context.Home, sessionId, word.Files)))
            .ToList();
        // Daoris's own words that waited (LIMITSWITCH3) are said as Daoris's after the person's, never as theirs.
        List<string> parts = [.. refused.Select(word => word.Text), .. waited.Where(word => !word.Daoris).Select(word => word.Text)];
        if (waited.Where(word => word.Daoris).ToList() is { Count: > 0 } daoris) parts.Add(TargetPrompt.DaorisWaited(daoris));
        return new ChatMessage(string.Join("\n\n", parts), files) { Brief = brief, TakenOver = opening };
    }

    /// <summary>
    /// The box with the person's words the old record holds now that it does not name yet (LIMITWORD1), after its own in the record's
    /// order: a word kept between the box and the go-on is in neither part otherwise. The box unchanged where there are none.
    /// </summary>
    private static LimitMoveBox Carried(LimitMoveBox box, PriorSession now)
    {
        var switching = box.Why == ContinueWhy.Account;
        List<string> later = [.. now.Waiting
            .Where(word => (switching ? DaorisWords.NewPartCarries(word) : word.Persons) && word.Id is { } id && !box.Said.Contains(id, StringComparer.Ordinal))
            .Select(word => word.Id!)];
        return later.Count == 0 ? box : box with { Said = [.. box.Said, .. later] };
    }

    /// <summary>
    /// The move's words are delivered (Codex 8): the words that waited leave the old record by their ids, named as taken by the new part,
    /// and the box goes. Never faults: what the record does not take waits there, and the box with it, for the next come-up.
    /// </summary>
    private async Task DeliveredAsync(string sessionId, LimitMoveBox box)
    {
        if (await TakenOffAsync(sessionId, box.Said, box.To).ConfigureAwait(false)) new LimitBoxes(context.Home).Clear(sessionId);
    }

    /// <summary>
    /// A new part a shell before this one opened went on with its words now (<see cref="ChatGoOn.WentOnAsync"/>): the move it was opened
    /// for is delivered, as at its turn's wire (<see cref="DeliveredAsync"/>). Nothing for a part no box names.
    /// </summary>
    public async Task DeliveredToAsync(string to)
    {
        var boxes = new LimitBoxes(context.Home);
        if (boxes.MovedTo(to) is { } from && boxes.Read(from) is { } box) await DeliveredAsync(from, box).ConfigureAwait(false);
    }

    /// <summary>
    /// The limit's moves a shell before this one left (LIMITRESUME1c, D206 §3.5, Codex 8), taken as it comes up, before any ended chat
    /// is taken up: a box with no new part is a move not yet made, left to the wake to make again; one whose new part took its turn
    /// is the box's end, its waiting words off the old record; one whose new part took none is delivered to it again from the box, kept
    /// on its record as Daoris's words, which it then goes on with in its own conversation, the old record's words still on it until.
    /// </summary>
    public async Task TakeUpBoxesAsync(CancellationToken ct)
    {
        var boxes = new LimitBoxes(context.Home);
        foreach (var session in boxes.Pending())
        {
            if (context.Closing || ct.IsCancellationRequested) return;
            try
            {
                if (boxes.Read(session) is not { To: { } to } box)
                {
                    boxes.Clear(session);
                    continue;
                }

                if (context.Events.After(to, 0).Events.Any(e => e.Kind == SessionEventKind.User))
                {
                    await DeliveredAsync(session, box).ConfigureAwait(false);
                    continue;
                }

                if (await context.ReadAgainAsync(session, ct).ConfigureAwait(false) is not { } prior) continue;
                // LIMITWORD1: a shell that ended before the go-on's words were boxed leaves them on the old record; they go on too.
                box = Carried(box, prior);
                boxes.Write(session, box);
                var message = await ComposeAsync(session, box, prior, ct).ConfigureAwait(false);
                await context.Service.SayAsync(
                        to, message.Prompt + ChatFiles.PathLines(message.Files), ct: ct, from: DaorisWords.From, key: $"limit-move/{session}")
                    .ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or IOException or System.Text.Json.JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // The box stays for the next come-up; nothing was taken off the old record.
            }
        }
    }

    /// <summary>Words off a record by their ids, named as taken by <paramref name="by"/>: whether the service took them.</summary>
    private async Task<bool> TakenOffAsync(string sessionId, IReadOnlyList<string> ids, string? by)
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
    /// Ask Daoris's new part, in its workspace's room (ASKSCOPE1b): placed as a press's is, the walk choosing its account and the log's
    /// <c>account.rotated</c> naming the part it carries; its room as the go-on finds it; its link kept in the room's history.
    /// </summary>
    /// <param name="order">
    /// Its actor's choices on other agents (LIMITGOON2, D205 §3.5(e)), walked as a new conversation's are, each judged by its access under
    /// the mode standing now; null places it on <paramref name="agent"/>, as LIMITGOON1's move to its next account does.
    /// </param>
    private async Task<ChatStart> HelpStartAsync(
        PriorSession prior, ISessionAdapter agent, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct,
        IReadOnlyList<ActorEffective>? order = null)
    {
        if (await help.SetAsideHelpAsync(null, prior.Workspace, ct).ConfigureAwait(false) is { } answering)
        {
            return new ChatStart(null, $"{HeldWords.AnsweringWhy(answering)}.");
        }

        var placed = await runner.PlaceHelpAsync(agent.Name, config, prior.Workspace, ct, carries: prior.Session, order: order).ConfigureAwait(false);
        if (placed is not { SessionId: { } id, Adapter: { } resolved, Selection: { } selection }) return new ChatStart(null, placed.Message);
        try
        {
            new HelpConversations(context.Home).StartedFrom(id, prior.Session, HelpConversations.Brief);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // Its history names no earlier conversation; its brief still says which it continues.
        }

        var (room, reads) = await help.HelpRoomNowAsync(id, HelpRoom.GoesOnIn(context.Home, prior.Workspace, prior.Tree), ct).ConfigureAwait(false);
        return await runner.RunAsync(
                prior.Actor ?? "ask-daoris", id, placed.Message, resolved, selection, config, runner.HelpPlace(room, reads, prior.Workspace), onEnded, ct,
                chosen: placed.Chosen)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Where a conversation whose agent has no account left goes on (LIMITGOON2, D205 §3.5(e)): its actor's effective order walked afresh
    /// as at a start, without the out agent's entries, whose accounts are its. Nowhere for a conversation on the person's pick, which
    /// never goes on with another agent (§3.5(d)), for an actor with no choice on another agent, <i>as before</i> among them, and where
    /// none of those choices can run now: each said in its conversation as the reason it waits.
    /// </summary>
    private async Task<Elsewhere> ElsewhereAsync(
        PriorSession prior, string ranOn, bool isHelp, DriverConfig config, string? workspace, CancellationToken ct)
    {
        if (prior.Choice == "pick") return new(null, LimitNotes.Picked(ranOn), $"`{ranOn}` is the agent you picked, so it never goes on with another.");

        var actor = prior.Actor ?? (isHelp ? "ask-daoris" : "chat");
        IReadOnlyList<ActorEffective> order;
        try
        {
            order = ActorChoices.Effective(actor, config);
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

        // Judged only (FOUNDATION1a-iii): the new part's own walk over these choices counts the start it opens on, once.
        var walk = await runner.WalkAsync(actor, others, config, workspace, null, ct, counts: false).ConfigureAwait(false);
        return walk.Chosen is null ? new(null, LimitNotes.NoOtherChoice(ranOn, walk.Refusal!), walk.Refusal!) : new(others, null, "");
    }

    /// <summary>The part it moves from, as its record says it ran (MAKERTRAIL2): its harness, that harness's maker and its models.</summary>
    private static EarlierMaker LeftBy(PriorSession prior, ISessionAdapter ran) =>
        new(ran.Name, ran.Toolchain?.Maker, prior.Ran?.Models ?? []);

    /// <summary>What <see cref="ElsewhereAsync"/> came to: the choices to walk, or the line its conversation waits under and why.</summary>
    private sealed record Elsewhere(IReadOnlyList<ActorEffective>? Order, Noted? Stays, string Why);

    /// <summary>The workspace a repository's chat runs in, by the registry; null where the service does not name one.</summary>
    private async Task<string?> WorkspaceOfAsync(string? repository, CancellationToken ct)
    {
        var registry = await context.Service.RegistryAsync(ct).ConfigureAwait(false);
        return registry.FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;
    }
}

/// <summary>A conversation's own line where its limit's move could not open the next part (LIMITGOON1), by its code.</summary>
public static class LimitNotes
{
    /// <summary>
    /// The new part did not open: the conversation ended, the words its refused turns held stay in it, and the start's own words follow
    /// in the English alone, as a program's.
    /// </summary>
    public static Noted NotMoved(string why) =>
        Noted.Of(
                NoteCodes.ChatLimitNotMoved,
                "Its account reached its limit, and it could not go on on the next account: write to it again once one has room.")
            .Then(" ", Noted.Said(why, NoteBy.Program));

    /// <summary>
    /// LIMITGOON1's *stays* written as a wait (LIMITRESUME1c, D206 §3.5): no account of its agent has room, the first ready again at
    /// <paramref name="first"/>'s <c>until</c>, the margin-added moment (§3.2), and it goes on by itself then, its words with it.
    /// </summary>
    public static Noted Waits(CoolingEntry first, TimeZoneInfo zone) =>
        Noted.Of(
            NoteCodes.ChatLimitWaits,
            $"No account of `{first.Agent}` has room now; the first is ready again at {CoolingWords.When(first.Until, zone)}. "
            + "It waits, your words with it, and goes on by itself then.",
            ("owner", first.Agent), ("until", NoteCodes.Moment(first.Until)), ("why", CoolingWhy.Of(first)));

    /// <summary>
    /// Why a conversation whose agent has no account left does not go on with another (LIMITGOON2, D205 §3.5(e)): it runs on the agent
    /// the person picked, which never moves (§3.5(d)). Said before its wait's line.
    /// </summary>
    public static Noted Picked(string agent) =>
        Noted.Of(
            NoteCodes.ChatLimitPicked,
            $"You picked `{agent}` for it, so it does not go on with another agent: it waits for an account of `{agent}`.",
            ("owner", agent));

    /// <summary>
    /// Why a conversation whose agent has no account left does not go on with another (LIMITGOON2, D205 §3.5(e)): no choice of its actor's
    /// on another agent can run now, or it has none. The walk's own words follow in the English alone, as a program's.
    /// </summary>
    public static Noted NoOtherChoice(string agent, string why) =>
        Noted.Of(
                NoteCodes.ChatLimitNoOther,
                $"None of its choices on another agent can run now, so it waits for an account of `{agent}`.",
                ("owner", agent))
            .Then(" ", Noted.Said(why, NoteBy.Program));

    /// <summary>The person's *Stop waiting* (LIMITRESUME1c, D206 §3.7): nothing goes on by itself, and their next word goes on as any does.</summary>
    public static Noted StoppedWaiting() =>
        Noted.Of(
            NoteCodes.ChatLimitStoppedWaiting,
            "It no longer waits for its account's limit: your words stay with it, and it goes on when you write to it.");

    /// <summary>The wake's first line (D206 §3.7, <c>limit.resumed</c>): its own account ready again at the margin-added <paramref name="until"/>.</summary>
    public static Noted Resumed(DateTimeOffset until) =>
        Noted.Of(
            NoteCodes.LimitResumed, "Its account was ready again; it went on in its own conversation.",
            ("until", NoteCodes.Moment(until)));

    /// <summary>
    /// Its own conversation could not be resumed at the wake, by the refusal's code, so it went on in a new part handed the brief (D206
    /// §3.5, §3.7, <c>limit.resume-refused</c>).
    /// </summary>
    public static Noted ResumeRefused(ContinueReason why) =>
        Noted.Of(
            NoteCodes.LimitResumeRefused,
            $"Its own conversation could not be resumed ({why.Code}), so it went on in a new session with Daoris's brief.",
            ("why", why.Code));
}

/// <summary>
/// Daoris's word to a conversation its account's limit cut off, kept at the wake through the say door under
/// <see cref="DaorisKeys.LimitReset"/> (LIMITRESUME1, D206 §3.3 step 1, §3.5): said last, after the person's words, in its own
/// conversation. The agent's English, as every word of Daoris's to an agent is.
/// </summary>
public static class LimitResetWord
{
    public const string Text = "Your account's limit has lifted. Go on with the work where you stopped.";
}
