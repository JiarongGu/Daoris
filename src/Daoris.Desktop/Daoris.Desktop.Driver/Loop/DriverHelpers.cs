using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The helper session (MIXAGENT2a-ii, D163 point 2; the mixed agents design §3 and §5): a working session's <c>helper_ask</c> (or the
/// person's terminal verb) is answered by a read-only session on another agent, whose answer returns as the asker's next turn,
/// marked as the helper's. The look reads the asks the connector and the person's door wrote, judges each (the person's rule, two
/// at once per asker, the cap), chooses the agent, runs one turn in a clone of its own on the reviewer's machinery, and takes the
/// answer the helper's tool wrote, or its last words, to the asker.
/// </summary>
/// <remarks>
/// <para><b>The state file is the driver's alone</b> (<see cref="HelperFiles"/>): <c>waiting</c> (for the person's yes, or for the
/// cap or an agent), <c>started</c> naming the helper's session BEFORE its turn (<c>helper_answer</c> finds its ask by that),
/// <c>answered</c> and <c>refused</c> with a code the asker reads. A restart reads a <c>started</c> ask whose helper ended, and
/// settles it.</para>
///
/// <para><b>Delivery is the opinion's door</b> (<see cref="DeliverHelperAsync"/>): a word on the asker's record, through the local
/// host, marked as the helper's; a quest asker that is running or parked takes it at its turn's end, never into a step in flight,
/// and one that never goes on (a teammate's, stood down) is refused with its code. A conversation takes it as its next turn
/// (MIXAGENT2a-iii): the shell's chat runner says it, or goes the ended one on with it.</para>
/// </remarks>
/// <param name="driver">
/// The driver whose run machinery a helper's pass goes through (PART10n, D166): the spawn's preparation, the connector, the rules
/// handed, the hold of the process and the account sayings, as <see cref="DriverOpinions"/> does. The front's <c>HelperPasses</c>,
/// <c>HelperTrees</c> and <c>HelperChoices</c> write through to the context, and this class reads them at each use.
/// </param>
internal sealed class DriverHelpers(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>
    /// The asks for a helper on this machine, at a look: each judged and started, settled or delivered as the facts
    /// let it go, beside the look. Returns how many helpers this look starts, which take slots under the cap before any quest's
    /// first start, as a second opinion's pass does (D130). Nothing is read where no ask is on file.
    /// </summary>
    /// <param name="taken">The slots this look's second opinions already took.</param>
    public async Task<int> HelpersAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct)
    {
        var service = _context.Service;
        var config = _context.Config;
        var files = new HelperFiles(_context.Home);
        var asks = files.Asks();
        var facts = _context.Runs.Facts;
        if (_context.Beside) facts.KeepAsksOf(asks.Select(ask => ask.Id));
        ForgetHeldBut(asks);
        if (asks.Count == 0)
        {
            await BackToPersonAsync(snapshot, asks, files, events, ct).ConfigureAwait(false);
            return 0;
        }

        var states = asks.ToDictionary(ask => ask.Id, ask => files.ReadState(ask.Id), StringComparer.Ordinal);
        var begun = 0;
        var begunFor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // The agents set up and signed in here, read once a look and only where an ask is judged (HELPERDEFAULT1).
        IReadOnlyList<string>? setUp = null;
        foreach (var ask in asks)
        {
            var state = states[ask.Id];
            try
            {
                switch (state?.State)
                {
                    case HelperStates.Refused:
                        continue;
                    case HelperStates.Answered or HelperStates.Started when config.Draining:
                        // DRAINHOLD1: a hand or a settling would go a conversation on in a turn the drain does not count; the answer is kept
                        // on file and handed at the first look that does not drain. A helper still running settles itself.
                        if (state.State == HelperStates.Answered) _context.DrainHolds(events, "helper", ask.Id, $"the answer to ask `{ask.Id}`");
                        continue;
                    case HelperStates.Answered:
                        // HELPERWAIT1 (point 6): a hand held while its asker stands as it did then waits on its asker's move, which wakes a look,
                        // and is not tried again at every look (200-480 ms each on the install, a record read whole).
                        if (state.Handed is null && !HeldStill(ask, snapshot))
                        {
                            var seen = Seen(snapshot, ask.Asker);
                            // Codex's round-2 #1: a park the person answered goes on at this look's plan, so its helper's answer is handed
                            // here, before the plan, never beside it, and both reach it in one prompt.
                            if (_context.Beside && seen is { } shown && shown.EndsWith("|answered", StringComparison.Ordinal))
                            {
                                if (await SettleOnceAsync(ask, token => DeliverHelperAsync(ask, state, files, token, seen), ct).ConfigureAwait(false) is { } line)
                                {
                                    events.Add(_context.Said.Say(line));
                                }
                            }
                            else
                            {
                                await SettlingAsync(ask, events, token => DeliverHelperAsync(ask, state, files, token, seen), ct).ConfigureAwait(false);
                            }
                        }

                        continue;
                    case HelperStates.Started:
                        await SettlingAsync(ask, events, token => SettleStartedAsync(ask, state, files, token), ct).ConfigureAwait(false);
                        continue;
                    case HelperStates.Cooling when !LeadWaits(snapshot, ask.Asker):
                        // LIMITRESUME1h (D206 design §3.8): its asker no longer waits on it, so an answer after the reset would land in a turn
                        // that no longer wants it. A cooling ask its asker still waits on is judged and run again below, as an approved one is.
                        events.Add(_context.Said.Say(await RefuseAsync(
                            ask, HelperWhy.Limited, state!.Agent, files, ct,
                            note: HelperNotes.Limited(state.Agent ?? "another agent", state.Until, _context.Harnesses.Zone)).ConfigureAwait(false)));
                        continue;
                }

                // PARALLEL1g (D181 point 3): under the watch the asker's record is read as the `helper` topic's judge beside the look, once
                // per ask, since what the verdict reads of it never moves; an ask not judged yet begins nothing, and its judgment wakes the
                // look that judges it. The verdict, its slot and its start stay here, before the plan, so a helper is counted as it was.
                PriorSession? record;
                if (!_context.Beside)
                {
                    record = await service.RecordAsync(ask.Asker, ct).ConfigureAwait(false);
                }
                else if (!facts.TryAsker(ask.Id, out record))
                {
                    var asker = ask.Asker;
                    var id = ask.Id;
                    Judging(id, async token =>
                    {
                        if (facts.KeepAsker(id, await service.RecordAsync(asker, token).ConfigureAwait(false))) _context.Runs.FactsMoved();
                        return [];
                    }, ct);
                    continue;
                }
                // HELPERDEFAULT1 (D163's 2026-10-24 note): where neither the repository nor its workspace sets a rule, Daoris's own agents,
                // those set up and signed in on this machine as its accounts say, are asked at once; the person's rule wins wherever set.
                var rule = (record is { Repository: { } repository }
                    ? HelperRules.Resolve(config, repository, record.Workspace).Rule
                    : HelperRule.Default).Over(setUp ??= _context.Harnesses.SetUpAgents());
                var startedFor = asks.Count(other => other.Id != ask.Id && string.Equals(other.Asker, ask.Asker, StringComparison.OrdinalIgnoreCase)
                                                     && states[other.Id] is { State: HelperStates.Started })
                                 + begunFor.GetValueOrDefault(ask.Asker);
                var slot = config.Cap - snapshot.SlotsHeld - taken - begun > 0;
                var verdict = HelperJudge.Judge(ask, state, record, rule, startedFor, slot, Between(ask.Asker, record, snapshot));
                switch (verdict.Kind)
                {
                    case HelperVerdict.RefuseKind:
                        // The asker is told on its own session, or it has left and the refusal is news (NOTIFY2h2); an agent not set up
                        // is named in it (HELPERDEFAULT1).
                        events.Add(_context.Said.Say(await RefuseAsync(
                            ask, verdict.Code!, verdict.Code == HelperWhy.NotSetUp ? ask.Agent : null, files, ct).ConfigureAwait(false)));
                        break;
                    case HelperVerdict.WaitKind:
                        // An agent's ask that comes to wait on the person's word is said once (MIXAGENT2c, D168): the line makes
                        // the loop's tick reach the windows, so the page reads what waits on the person again.
                        if (Wait(files, ask, state, verdict.Code) && verdict.Code is null)
                        {
                            events.Add(_context.Said.Wait("helper", $"helper  ask `{ask.Id}` from session `{ask.Asker}` waits for the person's yes or no in What needs you"));
                        }

                        break;
                    case HelperVerdict.GoKind when config.Draining:
                        // DRAINHOLD1: judged and left as it was, no state written and no slot taken; it goes at the first look that does not drain.
                        _context.DrainHolds(events, "helper", ask.Id, $"ask `{ask.Id}` from session `{ask.Asker}`");
                        break;
                    case HelperVerdict.GoKind when _context.Runs.TryOpinion(HelperClaim(ask.Id)):
                        begun++;
                        begunFor[ask.Asker] = begunFor.GetValueOrDefault(ask.Asker) + 1;
                        HelperBeside(ask.Id, token => RunHelperAsync(ask, record!, rule, files, token), ct);
                        break;
                }
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                events.Add(_context.Said.Retry(
                    "helper", $"helper  the ask for a helper `{ask.Id}` could not be looked at this look, and is looked at again at the next: {error.Message}"));
            }
        }

        await BackToPersonAsync(snapshot, asks, files, events, ct).ConfigureAwait(false);
        return begun;
    }

    // ——— HELPERWAIT1 (D163's and D188's notes): a session waiting on its own helper is not waiting on the person.

    /// <summary>
    /// Whether a driven session whose turn ends now, holding its quest as a park would, waits on its own helper instead (HELPERWAIT1, point
    /// 1; <see cref="Observation.Conclude"/>'s <c>awaitsHelper</c>): its agent has an ask of its own still open, and its last turn asked
    /// the person nothing, by <c>question_ask</c> since <paramref name="since"/> or by a go-ahead on its work's ask that still waits on
    /// them. A real question parks as before, whatever helper it asked too.
    /// </summary>
    /// <param name="since">When the run last handed its agent words, as a question is bound to its park (<see cref="QuestionFiles.SettleRun"/>).</param>
    internal async Task<bool> WaitsOnOwnHelperAsync(string session, QuestView quest, DateTimeOffset since, CancellationToken ct)
    {
        var files = new HelperFiles(_context.Home);
        if (HelperWaits.Open(session, files.Asks(), files.ReadState).Count == 0) return false;
        var exchanges = Exchanges.Of(await OwnLookAsync(session, since, ct).ConfigureAwait(false), session);
        return Exchanges.HoldsOnOwnHelper(exchanges, session);
    }

    /// <summary>
    /// The look of <paramref name="session"/>'s own exchanges as the loop composes it (COMMS1d): the helper files, its question since
    /// <paramref name="since"/> and the go-aheads on the ask its line is on, its entry's origin (COMMS1c). A source it cannot read is left
    /// null, which the read says as unread; the sources a park does not wait on here are not read.
    /// </summary>
    internal async Task<ExchangeLook> OwnLookAsync(string session, DateTimeOffset since, CancellationToken ct)
    {
        var files = new HelperFiles(_context.Home);
        var questions = new QuestionFiles(_context.Home);
        var asked = questions.AskedSince(session, since);
        var (ask, goAheads) = await GoAheadsAsync(session, ct).ConfigureAwait(false);
        return new ExchangeLook
        {
            Helpers = files.Asks(),
            HelperState = files.ReadState,
            Questions = asked is null ? null : questions.Read(session) is { } question ? [question] : [],
            QuestionsSince = since,
            Ask = ask,
            GoAheads = goAheads,
        };
    }

    /// <summary>
    /// The ask <paramref name="session"/>'s line is on, its entry's origin (COMMS1c), and the go-aheads on it (HELPERWAIT1): none for a
    /// line on no ask or an ask the service does not hold; null go-aheads where the service did not answer, unread, so its caller parks
    /// as before rather than read a question as absent.
    /// </summary>
    private async Task<(string? Ask, IReadOnlyList<GoAheadView>? GoAheads)> GoAheadsAsync(string session, CancellationToken ct)
    {
        try
        {
            var record = await _context.Service.RecordAsync(session, ct).ConfigureAwait(false);
            if (AskWords.AskOfEntry(record?.Entry) is not { } id) return (null, []);
            return (id, await _context.Service.FindAskAsync(id, ct).ConfigureAwait(false) is { } ask
                ? AskWords.Of(ask).GoAheads ?? []
                : []);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return (null, null);
        }
    }

    // The hands held, by ask, with how the look's own list showed the asker when each was held (point 6), null where it showed none, and
    // the state its record was read in, which a held hand is said once for.
    private readonly object _heldGate = new();
    private readonly Dictionary<string, (string? Seen, string State)> _held = new(StringComparer.Ordinal);

    /// <summary>
    /// How the look shows <paramref name="asker"/>: its state and whether the person answered it, from the list of live sessions; else, for
    /// one that ended, its state, when it last moved and how many words wait on it, from the records the look read (HELPERWAIT1); null where
    /// the look shows it neither way.
    /// </summary>
    private static string? Seen(Snapshot snapshot, string asker) =>
        snapshot.Active.FirstOrDefault(view => string.Equals(view.Id, asker, StringComparison.OrdinalIgnoreCase)) is { } view
            ? $"{view.State}|{(view.Answered ? "answered" : "")}"
            : snapshot.LastRun.Values.FirstOrDefault(last => string.Equals(last.Session, asker, StringComparison.OrdinalIgnoreCase)) is { } ended
            ? $"{ended.State}|{ended.Updated?.ToUnixTimeMilliseconds()}|{ended.Said?.Count}"
            : null;

    /// <summary>
    /// Whether a hand held before is still held (point 6): its asker shows in the look's list as it did when the hand was held. A record
    /// read whole tells nothing more until it moves, and its move wakes a look; an asker the list never showed is read again.
    /// </summary>
    private bool HeldStill(HelperAsk ask, Snapshot snapshot)
    {
        lock (_heldGate) return _held.TryGetValue(ask.Id, out var was) && was.Seen is not null && was.Seen == Seen(snapshot, ask.Asker);
    }

    /// <summary>
    /// A hand held (point 6): kept with how the list showed its asker, and said once for the state its record holds it in; a later look
    /// holds it again in silence until the asker moves.
    /// </summary>
    internal LookLine? Held(HelperAsk ask, PriorSession asker, string? seen)
    {
        lock (_heldGate)
        {
            var said = !_held.TryGetValue(ask.Id, out var was) || !string.Equals(was.State, asker.State, StringComparison.OrdinalIgnoreCase);
            _held[ask.Id] = (seen, asker.State);
            if (!said) return null;
        }

        return Helper(LookClass.Progress, asker.Parked
            ? $"helper  ask `{ask.Id}` is answered, and waits for the person's answer to session {asker.Session}, then for its turn to end: never into a step in flight."
            : $"helper  ask `{ask.Id}` is answered, and waits for session {asker.Session}'s turn to end: never into a step in flight.");
    }

    /// <summary>
    /// A hand the host refused (HELPERWAIT1): kept as a held hand is, said once for each refusal, and not asked again while the look shows its
    /// asker as it did; a transient refusal is then asked again once the asker moves, as a held one is.
    /// </summary>
    internal LookLine? Unhanded(HelperAsk ask, string? seen, LookLine line)
    {
        lock (_heldGate)
        {
            var said = !_held.TryGetValue(ask.Id, out var was) || !string.Equals(was.State, line.Text, StringComparison.Ordinal);
            _held[ask.Id] = (seen, line.Text);
            if (!said) return null;
        }

        return line;
    }

    /// <summary>
    /// What a hand does on this machine once the word is kept on <paramref name="to"/> (design §3; COWORK1d; HELPERWAIT1b2): an urgent
    /// word held in a running asker's inbox for its next step, under the record's id; the asker's note; and for a conversation, the event
    /// its runner hears (D137 §3.1), kept after the note so the turn it makes reads beneath it. Idempotent by the word's id, so the look
    /// that reads back a hand whose answer was lost completes it, never twice: the inbox holds an id once, and a conversation already told
    /// of the word is not told again. True where the word reaches the run at its next step.
    /// </summary>
    internal bool Dispatch(HelperState state, PriorSession to, string? word, string composed, DrivenInbox? inbox, SessionEvents events)
    {
        if (to.Kind == "chat" && word is not null
            && events.After(to.Session, 0).Events.Any(each => each.Kind == SessionEventKind.User && each.Reaches == "resume"
                                                              && string.Equals(each.Id, word, StringComparison.Ordinal)))
        {
            return false;
        }

        // Held where the run takes it at its next step; one the inbox no longer takes stays on the record, which goes on with it once.
        var atNextStep = inbox is not null && word is not null && inbox.HoldKept(
            new ChatMessage(composed, [])
            {
                Id = word, Level = MessageLevel.Urgent,
                Helped = new SaidWordView(word, composed, DateTimeOffset.UtcNow, [], true) { Helper = state.Helper, Level = MessageLevel.Urgent },
            },
            new SaidDoor(_context.Service, to.Session));
        var noted = HelperNotes.Answered(state.Agent ?? "another agent", state.Helper!, state.LastWords);
        _context.Output?.Append(to.Session, $"— {noted.Note}");
        events.Keep(to.Session, new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts }, say: null);

        // The shell's chat runner hears this event, as it hears the person's words to an ended conversation: the origin is Daoris's own
        // (`target`), never the person's, and an open conversation says the word as its next turn.
        if (to.Kind == "chat" && word is not null)
        {
            events.Keep(
                to.Session,
                new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Helper = state.Helper, Id = word, Text = composed, Reaches = "resume" },
                say: null);
        }

        return atNextStep;
    }

    /// <summary>The asker's inbox an urgent ask's word reaches at its next step (COWORK1d), or null where it reaches the turn's end.</summary>
    internal DrivenInbox? NextStepInbox(HelperAsk ask, PriorSession to) =>
        ask.Urgent && to.Kind != "chat" && to.Running && _context.Processes.InboxOf(to.Session) is { } inbox
        && inbox.ReachAt(MessageLevel.Urgent) == DrivenReach.NextStep
            ? inbox
            : null;

    internal void Forget(string ask)
    {
        lock (_heldGate) _held.Remove(ask);
    }

    private void ForgetHeldBut(IReadOnlyList<HelperAsk> asks)
    {
        var on = asks.Select(ask => ask.Id).ToHashSet(StringComparer.Ordinal);
        lock (_heldGate)
        {
            foreach (var gone in _held.Keys.Where(ask => !on.Contains(ask)).ToList()) _held.Remove(gone);
        }
    }

    // The waiting sessions whose move back to the person failed, by session, with the line said for it: each failure said once.
    private readonly Dictionary<string, string> _backFailed = new(StringComparer.Ordinal);

    /// <summary>
    /// The sessions that waited on their own helper and have none of their own asks open any more (HELPERWAIT1, point 4): every helper
    /// ended with no answer (refused, gone, unreadable, the person's no, past its bound), so each is brought back to the person as its
    /// turn's end would have parked it, the reason first. Read from the look's records in memory. Only while its quest is still taken: a
    /// quest closed meanwhile holds no park.
    /// </summary>
    /// <remarks>
    /// <b>A failed move is tried again at every look, and said once</b> (Codex's round-2 #5 on COWORKUX1): nothing else moves the record,
    /// so waiting for it to move would hold the lead for good. A service that could not be reached is said as a retry; one that refused the
    /// move (a service from before it, a tree another holds) is said to the person as a wait, since the session is theirs until it moves.
    /// One post a look, for a record in this rare state alone.
    /// <para><b>A refusal is kept on file</b> (HELPERWAIT1c; the co-worker design §5 point 4, §12 row 11): the record stays
    /// <c>completed</c>, so no park row says it, and its <see cref="HelperBack"/> is what <i>What needs you</i> says it from, with the
    /// service's words, from the first refusal. An unreachable service keeps none and leaves one kept as it was: nothing new was said. It
    /// goes once the move lands, and at the first look the session is no longer brought back: its quest closed, an ask of its own open
    /// again, words waiting on it, or no longer waiting at all.</para>
    /// </remarks>
    private async Task BackToPersonAsync(
        Snapshot snapshot, IReadOnlyList<HelperAsk> asks, HelperFiles files, List<string> events, CancellationToken ct)
    {
        var refused = files.Backs().ToDictionary(back => back.Session, StringComparer.Ordinal);
        var still = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in snapshot.LastRun.Values.Where(HelperWaits.Waits))
        {
            if (!snapshot.Quests.Any(quest => quest.Status == "Taken" && string.Equals(quest.Id, record.Quest, StringComparison.OrdinalIgnoreCase))) continue;
            if (HelperWaits.Open(record.Session, asks, files.ReadState).Count > 0) continue;
            still.Add(record.Session);

            var transcript = DaorisHome.Entry(_context.Home, HomeEntry.Sessions, $"{record.Session}.log");
            var back = Observation.HelperGone(
                HelperWaits.Why(record.Session, asks, files.ReadState), SessionLastWords.ParkedWords(_context.Events, record.Session, transcript));
            try
            {
                await _context.Service.AdvanceAsync(record.Session, back.State, back.Note, ct: ct, parts: back.Parts).ConfigureAwait(false);
                lock (_heldGate) _backFailed.Remove(record.Session);
                still.Remove(record.Session);
                events.Add(_context.Said.Wait(
                    "helper", $"helper  session {record.Session} waited on its own helper, which ended with no answer: it waits on the person again."));
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // Classed where it is made (NOTIFY2h2's census): a refusal is the person's to look at, an unreachable service a retry.
                var line = error is DriverException
                    ? Helper(LookClass.Wait, $"helper  session {record.Session} waited on its own helper, which ended with no answer; the service refused bringing it back to the person, so it is yours to look at: {error.Message}")
                    : Helper(LookClass.Retry, $"helper  session {record.Session} waited on its own helper, which ended with no answer, and could not be brought back to the person yet; it is tried again at the next look: {error.Message}");
                if (error is DriverException
                    && (!refused.TryGetValue(record.Session, out var kept) || kept.Words != error.Message || kept.Quest != record.Quest))
                {
                    files.WriteBack(new HelperBack(record.Session, record.Quest, error.Message, kept?.At ?? DateTimeOffset.UtcNow));
                }

                lock (_heldGate)
                {
                    if (_backFailed.TryGetValue(record.Session, out var said) && said == line.Text) continue;
                    _backFailed[record.Session] = line.Text;
                }

                events.Add(_context.Said.Say(line));
            }
        }

        foreach (var gone in refused.Keys.Where(session => !still.Contains(session))) files.ClearBack(gone);
    }

    /// <summary>
    /// Whether a driven session of this machine's waits on its own helpers now (LIMITRESUME1h, D206 design §3.8; HELPERWAIT1): it ended its
    /// turn waiting on them, by the look's records, and its quest is still taken, as <see cref="BackToPersonAsync"/> reads it.
    /// </summary>
    private static bool LeadWaits(Snapshot snapshot, string asker) =>
        snapshot.LastRun.Values.Any(record => string.Equals(record.Session, asker, StringComparison.Ordinal) && HelperWaits.Waits(record)
                                              && snapshot.Quests.Any(quest => quest.Status == "Taken"
                                                                              && string.Equals(quest.Id, record.Quest, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Whether the asker has no turn in flight (COAGENT1a, D199 point 5): its work in progress is captured only then. Whether it
    /// runs is this look's list of active sessions under the watch, where the asker's record is read once per ask, and its record
    /// otherwise; a running one is between turns where its newest turn has ended (<see cref="SessionEvents.TurnEnded"/>).
    /// </summary>
    private bool Between(string asker, PriorSession? record, Snapshot snapshot)
    {
        var running = _context.Beside
            ? snapshot.Active.FirstOrDefault(view => string.Equals(view.Id, asker, StringComparison.OrdinalIgnoreCase)) is { } view
              && view.State.ToLowerInvariant() is "queued" or "starting" or "working"
            : record is { Running: true };
        return !running || _context.Events.TurnEnded(asker);
    }

    /// <summary>How many asks for a helper are judged at once beside the looks (PARALLEL1g; a topic bounded, the events design §A3).</summary>
    internal const int HelpersAtOnce = 2;

    /// <summary>
    /// An answered ask handed to its asker, or a started one a restart left settled (<paramref name="settle"/>), once at a time per ask
    /// under its claim: under the watch as the <c>helper</c> topic's judge beside the look (PARALLEL1g), its line joining a later report,
    /// so an asker's record or a helpers' door slow to answer holds no start; in the look otherwise, as before.
    /// </summary>
    private async Task SettlingAsync(HelperAsk ask, List<string> events, Func<CancellationToken, Task<LookLine?>> settle, CancellationToken ct)
    {
        if (_context.Beside)
        {
            Judging(ask.Id, async token => await SettleOnceAsync(ask, settle, token).ConfigureAwait(false) is { } said ? [_context.Said.Say(said)] : [], ct);
            return;
        }

        if (await SettleOnceAsync(ask, settle, ct).ConfigureAwait(false) is { } line) events.Add(_context.Said.Say(line));
    }

    private async Task<LookLine?> SettleOnceAsync(HelperAsk ask, Func<CancellationToken, Task<LookLine?>> settle, CancellationToken ct)
    {
        if (!_context.Runs.TryOpinion(HelperClaim(ask.Id))) return null;
        try
        {
            return await settle(ct).ConfigureAwait(false);
        }
        finally
        {
            _context.Runs.OpinionDone(HelperClaim(ask.Id));
        }
    }

    /// <summary>
    /// One ask judged as the <c>helper</c> topic's judge beside the look (PARALLEL1g): one judgement per ask, <see cref="HelpersAtOnce"/> at a
    /// time, timed as work beside the look, and what stops it said as the look would have said it.
    /// </summary>
    private void Judging(string ask, Func<CancellationToken, Task<IReadOnlyList<string>>> judge, CancellationToken ct) =>
        _context.Runs.Judge("helper", ask, HelpersAtOnce, async token =>
        {
            try
            {
                var said = await _driver.Stages.TimeAsync("helpers", () => judge(token), ask, beside: true).ConfigureAwait(false);
                _context.Runs.Facts.Judged($"helper/{ask}");
                return said;
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !token.IsCancellationRequested))
            {
                return _context.Runs.Facts.Failed($"helper/{ask}")
                    ? [_context.Said.Retry("helper", $"helper  the ask for a helper `{ask}` could not be looked at this time, and is looked at again at a later look: {error.Message}")]
                    : [];
            }
        }, ct);

    // The asker's tree is read with git, unless a test hands in a stand-in (the context's HelperTrees), since a real read spawns git,
    // which the suite's fast half may not (MOD8); a helper's agent is chosen by HelperChoice.ChooseAsync, whose account walk reads this
    // machine's installs and sign-ins, unless a test hands in a choice (the context's HelperChoices).
    internal async Task<(string? Tip, string? Root)> ReadTreeAsync(string tree, CancellationToken ct) =>
        _context.HelperTrees is { } read
            ? await read(tree, ct).ConfigureAwait(false)
            : (await ReviewGit.HeadAsync(tree, ct).ConfigureAwait(false), await TreeGates.CheckoutOfTreeAsync(tree, ct).ConfigureAwait(false));

    // The asker's work in progress is captured with git and its files (COAGENT1a), unless a test hands in a stand-in (the context's
    // HelperCaptures), for the same reason.
    internal Task<HelperCaptureRead> CaptureAsync(string tree, CancellationToken ct) =>
        _context.HelperCaptures is { } capture ? capture(tree, ct) : HelperCapture.TakeAsync(tree, git: null, ct);

    private static string HelperClaim(string ask) => $"helper:{ask}";

    /// <summary>A pass beside the look, as a second opinion's is: told opened at once, its line joining a later look's report.</summary>
    private void HelperBeside(string ask, Func<CancellationToken, Task<LookLine>> work, CancellationToken ct) =>
        _ = _context.Runs.StartAsync(async opened =>
        {
            opened();
            try
            {
                var line = await work(ct).ConfigureAwait(false);
                return new StartRun(line.Text, Opened: true) { Look = line.Look };
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                return new StartRun($"helper  the helper for ask `{ask}` stopped, and is looked at again at the next look: {error.Message}", Opened: true)
                {
                    Look = new LookClassed(LookClass.Retry, "helper"),
                };
            }
            finally
            {
                _context.Runs.OpinionDone(HelperClaim(ask));
            }
        });

    /// <summary>
    /// An ask held: <c>waiting</c>, with why it waits where it waits for something but the person's yes. Written when it changes;
    /// whether it was written. An <c>approved</c> ask is left as it is (HELPERYES1, D163 point 3): the person's yes outlives a full
    /// cap or an agent not ready, and the next look starts it once a slot frees, without a second yes.
    /// </summary>
    internal static bool Wait(HelperFiles files, HelperAsk ask, HelperState? state, string? code)
    {
        // A cooling ask keeps its state and its reset while it waits for a slot or an account (LIMITRESUME1h), as an approved one keeps its yes.
        if (state is { State: HelperStates.Approved or HelperStates.Cooling }) return false;
        if (state is { State: HelperStates.Waiting } kept && kept.Code == code) return false;
        files.WriteState(new HelperState(ask.Id, HelperStates.Waiting, code, null, DateTimeOffset.UtcNow));
        return true;
    }

    /// <summary>
    /// An ask refused: its state <c>refused</c> with the code, and a coded line on the asker's record saying the agent (where one
    /// was chosen) and why. Never an account's name or a path. Its line is the log's, the asker told; an asker that has left is told
    /// nothing, so the refusal is news (NOTIFY2h2).
    /// </summary>
    /// <param name="detail">What refused it beyond its code, for the log's line alone (a capture's path): never the notes'.</param>
    /// <param name="note">The asker's line where its code says more than <see cref="HelperNotes.Refused"/> does (LIMITRESUME1h's reset).</param>
    internal Task<LookLine> RefuseAsync(
        HelperAsk ask, string code, string? agent, HelperFiles files, CancellationToken ct, string? detail = null, Noted? note = null)
    {
        var kept = files.ReadState(ask.Id);
        files.WriteState(new HelperState(ask.Id, HelperStates.Refused, code, kept?.Helper, DateTimeOffset.UtcNow)
        {
            Agent = agent ?? kept?.Agent, Until = kept?.Until,
        });
        note ??= HelperNotes.Refused(code, agent ?? kept?.Agent);
        if (code != HelperWhy.AskerGone)
        {
            _context.Output?.Append(ask.Asker, $"— {note.Note}");
            _context.Events.Keep(ask.Asker, new SessionEvent { Kind = SessionEventKind.Note, Text = note.Note, Parts = note.Parts }, say: null);
        }

        return Task.FromResult(new LookLine(
            detail is { Length: > 0 }
                ? $"helper  ask `{ask.Id}` refused ({code}): {HelperWhy.Said(code)} ({detail})."
                : $"helper  ask `{ask.Id}` refused ({code}): {HelperWhy.Said(code)}.",
            code == HelperWhy.AskerGone
                ? LookClassed.Noticed("helper", new LookNews(LookSaid.HelperAskerGone, ask.Id))
                : new LookClassed(LookClass.Progress, "helper")));
    }

    /// <summary>A helper's line, classed where it is made (NOTIFY2h2).</summary>
    internal static LookLine Helper(LookClass @class, string line) => new(line, new LookClassed(@class, "helper"));

    private HelperSettling Settling => new(this, _context);

    private Task<LookLine> RunHelperAsync(HelperAsk ask, PriorSession asker, HelperRule rule, HelperFiles files, CancellationToken ct) =>
        Settling.RunHelperAsync(ask, asker, rule, files, ct);

    private Task<LookLine?> SettleStartedAsync(HelperAsk ask, HelperState state, HelperFiles files, CancellationToken ct) =>
        Settling.SettleStartedAsync(ask, state, files, ct);

    private Task<LookLine?> DeliverHelperAsync(HelperAsk ask, HelperState state, HelperFiles files, CancellationToken ct, string? seen = null) =>
        Settling.DeliverHelperAsync(ask, state, files, ct, seen);


    private readonly HelperTurn _turn = new(context, driver);

    internal Task<HelperPassRun> HelperPassAsync(HelperPassAsk ask, ReviewerChoice choice, CancellationToken ct = default) =>
        _turn.HelperPassAsync(ask, choice, ct);

    internal static SessionConclusion ReadHelper(string asker, int exitCode, string? turnFailed) => HelperTurn.ReadHelper(asker, exitCode, turnFailed);
}
