using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A quest's session handed to another agent (MIXAGENT1a-ii, D163 point 1; the mixed-agents design §2): the look takes the boxes the
/// terminal kept, judges each again, stops the session as the person's, and holds the quest on the named agent
/// (<see cref="HandedQuests"/>) until it leaves this machine. The planner reads the stop as released and the next start runs on the
/// agent named, in the same tree.
/// </summary>
/// <remarks>
/// <b>An agent's ask</b> (MIXAGENT1c, D163 point 3): before the boxes, the look settles each ask a session's agent made through
/// <c>handover_propose</c> by the person's <c>handOver</c> rule (<see cref="HandOverAskJudge"/>): held for the person's word, refused, or
/// let go to the next agent in the person's order. A quest's session whose turn still runs is never stopped for it: its run takes the
/// ask at its turn's end (<see cref="AtTurnEndAsync"/>); one whose turn ended is kept as a box this look takes. A conversation's goes to
/// the shell's chat runner where one is wired (<see cref="LoopContext.HandsOver"/>), else is kept as the box the runner takes.
/// </remarks>
internal sealed class DriverHandOvers(LoopContext context)
{
    private readonly LoopContext _context = context;

    /// <summary>
    /// The agents a quest may go on with, as the roster reads them for a start: the accounts it holds for an agent and the wire its
    /// door rides. Null for an agent this build, with its plugins, does not have.
    /// </summary>
    internal HandOverAgent? Drives(string agent) => QuestHandOver.Roster(_context.Adapters, _context.Harnesses.Home)(agent);

    /// <summary>
    /// The quests handed to another agent, at a look: each agent's ask to hand over settled by the person's rule, each box a terminal
    /// kept for a quest's session taken, a quest that left this machine goes back to the machine's agent, and what stands is read into
    /// the snapshot for the planner. Nothing is read where no ask, no box and no store is on file. A conversation's box is the shell's
    /// chat runner's, and is left to it.
    /// </summary>
    internal async Task<Snapshot> HandOversAsync(Snapshot snapshot, List<string> events, CancellationToken ct)
    {
        await AgentAsksAsync(events, ct).ConfigureAwait(false);

        var box = new HandOverBox(_context.Home);
        var handed = new HandedQuests(_context.Home);

        foreach (var id in box.Pending())
        {
            try
            {
                await TakeHandOverAsync(id, box, handed, events, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                events.Add(_context.Said.Retry("hand-over", $"hand-over  the hand-over of session `{id}` could not be looked at this look, and is looked at again at the next: {error.Message}"));
            }
        }

        var standing = handed.All();
        if (standing.Count == 0) return snapshot;

        // Held until the quest leaves this machine (design §2): done, declined or gone from the open list, or taken elsewhere.
        foreach (var (quest, _) in standing)
        {
            var row = snapshot.Quests.FirstOrDefault(each => string.Equals(each.Id, quest, StringComparison.OrdinalIgnoreCase));
            var closed = row is not { Status: "Open" or "Taken" };
            var away = !closed && snapshot.Takes.TryGetValue(quest, out var take)
                       && snapshot.LastRun.TryGetValue(quest, out var last) && Planner.TakenAway(row!, last, take);
            if (!closed && !away) continue;

            // FABRIC-CLOSE1c20: a take lost to the person's reopen is said as that, never as another machine's.
            var reopened = !closed && snapshot.Takes[quest].Reopened;
            handed.Clear(quest);
            events.Add(_context.Said.News("hand-over", new LookNews(LookSaid.HandedBack, quest, Quest: quest),
                $"hand-over  `#{quest}` {(closed ? "has closed" : reopened ? "was lost to your reopen of the work it grew from" : "is taken elsewhere")}: it goes back to this machine's agent."));
        }

        return snapshot with { Handed = handed.All() };
    }

    private async Task TakeHandOverAsync(string id, HandOverBox box, HandedQuests handed, List<string> events, CancellationToken ct)
    {
        var service = _context.Service;
        var record = (await service.SessionRecordsAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        // A conversation's box, or one for a record that is not here, is not this look's: the shell's chat runner takes the first.
        if (record is null || !QuestHandOver.IsQuestSession(record)) return;
        if (box.Read(id) is not { } ask)
        {
            box.Clear(id);
            return;
        }

        var quest = record.Quest!;
        var agentAsk = ask.AskedBy == HandOverBy.Agent;
        var (refused, _) = await QuestHandOver.JudgeAsync(service, Drives, id, ask.Agent, ask.Account, ct, agentAsk).ConfigureAwait(false);
        if (refused is not null)
        {
            box.Clear(id);
            events.Add(_context.Said.News(
                "hand-over", new LookNews(LookSaid.HandOverRefused, id, id, quest, record.Workspace), $"hand-over  session `{id}` (#{quest}): {refused.Message}"));
            NotTaken(id, ask.Agent, refused.Why, agentAsk);
            return;
        }

        // A driven session works its quest in one turn, so there is no turn's end to wait for: it is stopped at once, as the
        // person's stop, with a line of its own. One that already ended has nothing to stop; the stop it ended on is released all the same.
        // MIXAGENT1c: a park its agent ended its turn on after asking is ended as that stop too, the record moved by the ledger's door.
        var note = HandOverNotes.QuestHandedOver(ask.Agent);
        if (agentAsk && string.Equals(record.State, "awaiting-person", StringComparison.OrdinalIgnoreCase))
        {
            await service.AdvanceAsync(id, "stopped", note, ct: ct).ConfigureAwait(false);
        }
        else if (record.Live)
        {
            bool stopped;
            if (_context.Stops is { } stand)
            {
                stopped = stand(id, note);
            }
            else
            {
                var answer = await SessionMoves.StopAsync(_context.Processes, service, id, ct, note).ConfigureAwait(false);
                stopped = answer.Stopped;
            }

            if (!stopped)
            {
                box.Clear(id);
                events.Add(_context.Said.News("hand-over", new LookNews(LookSaid.HandOverElsewhere, id, id, quest, record.Workspace),
                    $"hand-over  session `{id}` (#{quest}) runs in another Daoris process on this machine: hand it over there, or stop it first."));
                NotTaken(id, ask.Agent, HandOverAskWhy.Running, agentAsk);
                return;
            }
        }

        handed.Write(quest, new HandedQuest(ask.Agent, ask.Account, id, ask.Why, ask.AskedBy, ask.At) { Note = agentAsk ? AgentNote(id) : null });
        box.Clear(id);
        events.Add(_context.Said.Progress("hand-over", $"hand-over  session `{id}` (#{quest}): stopped, and `#{quest}` goes on with {ask.Agent}."));
    }

    /// <summary>
    /// Each agent's ask to hand its session over (MIXAGENT1c), settled by the person's rule: what changed is said once on the session's
    /// record and in a line, so the look's tick reaches the windows where an ask comes to wait on the person.
    /// </summary>
    private async Task AgentAsksAsync(List<string> events, CancellationToken ct)
    {
        var files = new HandOverAskFiles(_context.Home);
        foreach (var ask in files.Asks())
        {
            try
            {
                await SettleAsync(ask, files, events, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                events.Add(_context.Said.Retry(
                    "hand-over", $"hand-over  the ask to hand session `{ask.Session}` over could not be looked at this look, and is looked at again at the next: {error.Message}"));
            }
        }
    }

    private async Task SettleAsync(HandOverAsk ask, HandOverAskFiles files, List<string> events, CancellationToken ct)
    {
        var own = files.ReadState(ask.Session) is { } kept && kept.Id == ask.Id ? kept : null;
        if (own is { State: HandOverAskStates.Taken or HandOverAskStates.Refused }) return;

        var record = await _context.Service.RecordAsync(ask.Session, ct).ConfigureAwait(false);
        var verdict = await JudgeAsync(ask, own, record, ct).ConfigureAwait(false);
        var session = ask.Session;
        switch (verdict.Kind)
        {
            case HandOverAskVerdict.RefuseKind when verdict.Code == HandOverAskWhy.AskerGone:
                // Its session left this machine: nothing to say it on, and nothing left to keep.
                files.Clear(session);
                events.Add(_context.Said.News("hand-over", new LookNews(LookSaid.HandOverDropped, ask.Id, session),
                    $"hand-over  the ask to hand session `{session}` over is dropped: {HandOverAskWhy.Said(verdict.Code)}."));
                break;
            case HandOverAskVerdict.RefuseKind:
                RefuseAsk(session, verdict.Code!, ask.Id);
                events.Add(_context.Said.News("hand-over", new LookNews(LookSaid.HandOverAskRefused, ask.Id, session, Workspace: record?.Workspace),
                    $"hand-over  session `{session}`'s agent asked to hand it over, refused ({verdict.Code}): {HandOverAskWhy.Said(verdict.Code!)}."));
                break;
            case HandOverAskVerdict.WaitKind:
                // HANDOVERSAID1: what the take would refuse is refused at the ask, never left to wait on a yes it cannot honour: its
                // quest closed or carried elsewhere, its tree gone, the agent named not this machine's. Judged at each look it waits.
                if (await TakeRefusesAsync(record!, verdict.Agent!, ct).ConfigureAwait(false) is { } refused)
                {
                    var code = HandOverAskWhy.Of(refused.Why);
                    RefuseAsk(session, code, ask.Id);
                    events.Add(_context.Said.News("hand-over", new LookNews(LookSaid.HandOverAskRefused, ask.Id, session, Workspace: record?.Workspace),
                        $"hand-over  session `{session}`'s agent asked to hand it over, refused ({code}): {refused.Message}"));
                    break;
                }

                if (own is { State: HandOverAskStates.Waiting } waiting && waiting.Agent == verdict.Agent) break;
                files.WriteState(session, new HandOverAskState(ask.Id, HandOverAskStates.Waiting, null, verdict.Agent, DateTimeOffset.UtcNow));
                if (own is null) Say(session, HandOverAskNotes.Asked(verdict.Agent!));
                // The line makes the loop's tick reach the windows, so the page reads what waits on the person again.
                events.Add(_context.Said.Wait(
                    "hand-over", $"hand-over  session `{session}`'s agent asks to hand it over to {verdict.Agent}: it waits for the person's yes or no in What needs you"));
                break;
            case HandOverAskVerdict.GoKind:
                await GoAsync(ask, own, record!, verdict.Agent!, files, events, ct).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>The verdict on one ask, from the session's record, the rule standing for it, its turns and the person's order.</summary>
    private async Task<HandOverAskVerdict> JudgeAsync(HandOverAsk ask, HandOverAskState? own, PriorSession? record, CancellationToken ct)
    {
        var rule = record is { Repository: { } repository } ? HandOverRules.Resolve(_context.Config, repository, record.Workspace).Rule : HandOverRule.Default;
        // Read only for a part a hand-over started: whether a turn of its own ended before the ask.
        var turnSince = record is not { From: not null } || !HandOverWhy.Known(record.FromWhy) || TurnEndedBefore(record.Session, ask.At);
        var next = record is null || record.Teammate ? null : await NextAgentAsync(record, ct).ConfigureAwait(false);
        return HandOverAskJudge.Judge(ask, own, record, rule, turnSince, next);
    }

    /// <summary>
    /// An ask let go: a quest's session whose turn runs is left to that turn's end, one whose turn ended is kept as the box this look
    /// takes, and a conversation goes to the chat runner, or is kept as its box.
    /// </summary>
    private async Task GoAsync(
        HandOverAsk ask, HandOverAskState? own, PriorSession record, string agent, HandOverAskFiles files, List<string> events, CancellationToken ct)
    {
        var session = ask.Session;
        var service = _context.Service;
        var said = own is { State: HandOverAskStates.Approved, Agent: { } before } && before == agent;
        if (IsQuest(record) && record.Running)
        {
            // 🔴 Never mid-turn (D191): the run takes it at its turn's end, its note kept for the next start before the session stops.
            if (said) return;
            files.WriteState(session, new HandOverAskState(ask.Id, HandOverAskStates.Approved, null, agent, DateTimeOffset.UtcNow));
            Say(session, HandOverAskNotes.Taking(agent));
            events.Add(_context.Said.Progress("hand-over", $"hand-over  session `{session}`'s agent asked to hand it over: it goes on with {agent} once its turn ends."));
            return;
        }

        bool kept;
        string? message;
        string? code;
        if (IsQuest(record))
        {
            var asked = await QuestHandOver.AskAsync(
                service, _context.Home, Drives, session, agent, null, HandOverWhy.Agent, HandOverBy.Agent, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            (kept, message, code) = (asked.Kept, asked.Message, asked.Why);
        }
        else if (_context.HandsOver is { } take)
        {
            // The state first, so a look meanwhile reads it taken and never keeps a second box.
            files.WriteState(session, new HandOverAskState(ask.Id, HandOverAskStates.Taken, null, agent, DateTimeOffset.UtcNow));
            var handed = await take(session, agent, ct).ConfigureAwait(false);
            (kept, message, code) = (handed.Kept || handed.SessionId is not null, handed.Message, handed.Why);
        }
        else
        {
            var asked = await HandOver.AskAsync(
                service, _context.Home, AccountsOf, session, agent, null, HandOverWhy.Agent, HandOverBy.Agent, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            (kept, message, code) = (asked.Kept, asked.Message, asked.Why);
        }

        if (!kept)
        {
            NotTaken(session, agent, code, agentAsk: true, ask.Id);
            events.Add(_context.Said.News("hand-over", new LookNews(LookSaid.HandOverFailed, ask.Id, session, Workspace: record.Workspace),
                $"hand-over  session `{session}`'s agent asked to hand it over to {agent}, and it could not be taken ({code ?? "refused"}): {message}"));
            return;
        }

        files.WriteState(session, new HandOverAskState(ask.Id, HandOverAskStates.Taken, null, agent, DateTimeOffset.UtcNow));
        if (!said) Say(session, HandOverAskNotes.Taking(agent));
        events.Add(_context.Said.Progress("hand-over", $"hand-over  session `{session}`'s agent asked to hand it over: it goes on with {agent}."));
    }

    /// <summary>
    /// At a quest session's turn's end, before its record is concluded (MIXAGENT1c; D191: never mid-turn, its note written before the
    /// session stops): its agent's ask, where the rule or the person's yes lets it go, hands the quest over. The store is written with
    /// the agent's note and the ask is taken; the agent it goes on with is answered, for the run to conclude the record as handed over.
    /// Null where nothing is to be taken: no ask, one waiting on the person, or one refused, which is said as the look says it.
    /// </summary>
    internal async Task<string?> AtTurnEndAsync(string session, string quest, CancellationToken ct)
    {
        var files = new HandOverAskFiles(_context.Home);
        if (files.Ask(session) is not { } ask) return null;
        var own = files.ReadState(session) is { } kept && kept.Id == ask.Id ? kept : null;
        if (own is { State: HandOverAskStates.Taken or HandOverAskStates.Refused }) return null;

        var record = await _context.Service.RecordAsync(session, ct).ConfigureAwait(false);
        var verdict = await JudgeAsync(ask, own, record, ct).ConfigureAwait(false);
        switch (verdict.Kind)
        {
            case HandOverAskVerdict.GoKind when Drives(verdict.Agent!) is not null:
                new HandedQuests(_context.Home).Write(
                    quest, new HandedQuest(verdict.Agent!, null, session, HandOverWhy.Agent, HandOverBy.Agent, DateTimeOffset.UtcNow) { Note = ask.Note });
                files.WriteState(session, new HandOverAskState(ask.Id, HandOverAskStates.Taken, null, verdict.Agent, DateTimeOffset.UtcNow));
                if (own is not { State: HandOverAskStates.Approved }) Say(session, HandOverAskNotes.Taking(verdict.Agent!));
                return verdict.Agent;
            case HandOverAskVerdict.RefuseKind when verdict.Code != HandOverAskWhy.AskerGone:
                RefuseAsk(session, verdict.Code!, ask.Id);
                return null;
            default:
                // Waiting on the person: its turn's end parks it, and their yes hands it over from there.
                return null;
        }
    }

    /// <summary>The next agent in the person's order that can take this session: a door that converses for a conversation, one that drives for a quest's.</summary>
    private async Task<string?> NextAgentAsync(PriorSession record, CancellationToken ct)
    {
        var doors = _context.HandOverDoors is { } stand ? await stand(ct).ConfigureAwait(false) : await RosterDoorsAsync(ct).ConfigureAwait(false);
        var quest = IsQuest(record);
        return HandOverOrder.Others(record.Adapter, doors).FirstOrDefault(agent => quest ? Drives(agent) is not null : AccountsOf(agent) is not null);
    }

    /// <summary>The roster's doors in its order, as the page's offer reads them: each adapter, whether it is here, and whose accounts it runs as.</summary>
    private async Task<IReadOnlyList<HandOverDoor>> RosterDoorsAsync(CancellationToken ct)
    {
        var reports = await _context.Harnesses.RosterAsync(_context.Config, refresh: false, ct).ConfigureAwait(false);
        return [.. reports.Select(report => new HandOverDoor(report.Adapter, report.Present, AccountOfDoor(report.Adapter)))];
    }

    private string? AccountOfDoor(string adapter)
    {
        try
        {
            return _context.Harnesses.Toolchain(adapter)?.AccountOf;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// The accounts the roster holds for an agent that holds conversations, or null where it has none such: the chat runner's own
    /// judge of a conversation's hand-over (<c>ChatHandOver.AccountsOf</c>), read over the loop's roster.
    /// </summary>
    private IReadOnlyList<string>? AccountsOf(string agent)
    {
        try
        {
            var resolved = _context.Adapters.Resolve(agent);
            if (!resolved.Interactive) return null;
            return resolved.Toolchain is { } toolchain ? HarnessSettings.Profiles(_context.Harnesses.Home, toolchain.Owner(resolved.Name)) : [];
        }
        catch (DriverException)
        {
            return null;
        }
    }

    private static bool IsQuest(PriorSession record) => record is { Kind: not "chat", Quest: not null, Ask: null, Teammate: false };

    /// <summary>Whether a turn of the session's own ended at or before <paramref name="at"/>, read from its record's events.</summary>
    private bool TurnEndedBefore(string session, DateTimeOffset at) =>
        _context.Events.After(session, 0).Events.Any(e => e.Kind == SessionEventKind.Turn && e.At <= at);

    /// <summary>The note the agent left with its ask, for the next start; null where it left none.</summary>
    private string? AgentNote(string session) => new HandOverAskFiles(_context.Home).Ask(session)?.Note is { Length: > 0 } note ? note : null;

    /// <summary>What the take would refuse a hand-over of this session to <paramref name="agent"/> for now, judged as the take judges it; null where it would go.</summary>
    private async Task<HandOverAnswer?> TakeRefusesAsync(PriorSession record, string agent, CancellationToken ct)
    {
        var (refused, _) = IsQuest(record)
            ? await QuestHandOver.JudgeAsync(_context.Service, Drives, record.Session, agent, null, ct, agentAsk: true).ConfigureAwait(false)
            : await HandOver.JudgeAsync(_context.Service, AccountsOf, record.Session, agent, null, ct).ConfigureAwait(false);
        return refused;
    }

    /// <summary>
    /// A hand-over let go that could not be taken (HANDOVERSAID1): an agent's ask's state refused with the take's own code, and on the
    /// session's record, whoever asked, the not-taken line with its reason and what the session does next. Never said as refused.
    /// </summary>
    private void NotTaken(string session, string agent, string? code, bool agentAsk, string? id = null)
    {
        var why = HandOverAskWhy.Of(code);
        if (agentAsk)
        {
            var files = new HandOverAskFiles(_context.Home);
            if ((id ?? files.Ask(session)?.Id) is { } askId)
            {
                files.WriteState(session, new HandOverAskState(askId, HandOverAskStates.Refused, why, null, DateTimeOffset.UtcNow));
            }
        }

        Say(session, HandOverAskNotes.NotTaken(agent, why));
    }

    /// <summary>An ask refused: its state with the code, and the coded line on its session's record.</summary>
    private void RefuseAsk(string session, string code, string? id = null)
    {
        var files = new HandOverAskFiles(_context.Home);
        var askId = id ?? files.Ask(session)?.Id;
        if (askId is null) return;
        files.WriteState(session, new HandOverAskState(askId, HandOverAskStates.Refused, code, null, DateTimeOffset.UtcNow));
        Say(session, HandOverAskNotes.Refused(code));
    }

    /// <summary>A coded line on the session's record and its transcript, as every look's note is written.</summary>
    private void Say(string session, Noted note) => HandOverAskNotes.Say(_context.Events, _context.Output, session, note);
}
