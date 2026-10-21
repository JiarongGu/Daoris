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
    /// The asks for a helper on this machine, at a look (MIXAGENT2a-ii): each judged and started, settled or delivered as the facts
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
        if (asks.Count == 0) return 0;

        var states = asks.ToDictionary(ask => ask.Id, ask => files.ReadState(ask.Id), StringComparer.Ordinal);
        var begun = 0;
        var begunFor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var ask in asks)
        {
            var state = states[ask.Id];
            try
            {
                switch (state?.State)
                {
                    case HelperStates.Refused:
                        continue;
                    case HelperStates.Answered:
                        if (state.Handed is null) await SettlingAsync(ask, events, token => DeliverHelperAsync(ask, state, files, token), ct).ConfigureAwait(false);
                        continue;
                    case HelperStates.Started:
                        await SettlingAsync(ask, events, token => SettleStartedAsync(ask, state, files, token), ct).ConfigureAwait(false);
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
                var rule = record is { Repository: { } repository }
                    ? HelperRules.Resolve(config, repository, record.Workspace).Rule
                    : HelperRule.Default;
                var startedFor = asks.Count(other => other.Id != ask.Id && string.Equals(other.Asker, ask.Asker, StringComparison.OrdinalIgnoreCase)
                                                     && states[other.Id] is { State: HelperStates.Started })
                                 + begunFor.GetValueOrDefault(ask.Asker);
                var slot = config.Cap - snapshot.SlotsHeld - taken - begun > 0;
                var verdict = HelperJudge.Judge(ask, state, record, rule, startedFor, slot);
                switch (verdict.Kind)
                {
                    case HelperVerdict.RefuseKind:
                        // The asker is told on its own session; nobody else must answer (NOTIFY2h).
                        events.Add(_context.Said.Progress("helper", await RefuseAsync(ask, verdict.Code!, null, files, ct).ConfigureAwait(false)));
                        break;
                    case HelperVerdict.WaitKind:
                        // An agent's ask that comes to wait on the person's word is said once (MIXAGENT2c, D168): the line makes
                        // the loop's tick reach the windows, so the page reads what waits on the person again.
                        if (Wait(files, ask, state, verdict.Code) && verdict.Code is null)
                        {
                            events.Add(_context.Said.Wait("helper", $"helper  ask `{ask.Id}` from session `{ask.Asker}` waits for the person's yes or no in What needs you"));
                        }

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

        return begun;
    }

    /// <summary>How many asks for a helper are judged at once beside the looks (PARALLEL1g; a topic bounded, the events design §A3).</summary>
    internal const int HelpersAtOnce = 2;

    /// <summary>
    /// An answered ask handed to its asker, or a started one a restart left settled (<paramref name="settle"/>), once at a time per ask
    /// under its claim: under the watch as the <c>helper</c> topic's judge beside the look (PARALLEL1g), its line joining a later report,
    /// so an asker's record or a helpers' door slow to answer holds no start; in the look otherwise, as before.
    /// </summary>
    private async Task SettlingAsync(HelperAsk ask, List<string> events, Func<CancellationToken, Task<string?>> settle, CancellationToken ct)
    {
        if (_context.Beside)
        {
            Judging(ask.Id, async token => await SettleOnceAsync(ask, settle, token).ConfigureAwait(false) is { } said ? [_context.Said.Progress("helper", said)] : [], ct);
            return;
        }

        if (await SettleOnceAsync(ask, settle, ct).ConfigureAwait(false) is { } line) events.Add(_context.Said.Progress("helper", line));
    }

    private async Task<string?> SettleOnceAsync(HelperAsk ask, Func<CancellationToken, Task<string?>> settle, CancellationToken ct)
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
                    ? [$"helper  the ask for a helper `{ask}` could not be looked at this time, and is looked at again at a later look: {error.Message}"]
                    : [];
            }
        }, ct);

    // The asker's tree is read with git, unless a test hands in a stand-in (the context's HelperTrees), since a real read spawns git,
    // which the suite's fast half may not (MOD8); a helper's agent is chosen by HelperChoice.ChooseAsync, whose account walk reads this
    // machine's installs and sign-ins, unless a test hands in a choice (the context's HelperChoices).
    private async Task<(string? Tip, string? Root)> ReadTreeAsync(string tree, CancellationToken ct) =>
        _context.HelperTrees is { } read
            ? await read(tree, ct).ConfigureAwait(false)
            : (await ReviewGit.HeadAsync(tree, ct).ConfigureAwait(false), await TreeGates.CheckoutOfTreeAsync(tree, ct).ConfigureAwait(false));

    private static string HelperClaim(string ask) => $"helper:{ask}";

    /// <summary>A pass beside the look, as a second opinion's is: told opened at once, its line joining a later look's report.</summary>
    private void HelperBeside(string ask, Func<CancellationToken, Task<string>> work, CancellationToken ct) =>
        _ = _context.Runs.StartAsync(async opened =>
        {
            opened();
            try
            {
                return new StartRun(await work(ct).ConfigureAwait(false), Opened: true);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                return new StartRun($"helper  the helper for ask `{ask}` stopped, and is looked at again at the next look: {error.Message}", Opened: true);
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
    private static bool Wait(HelperFiles files, HelperAsk ask, HelperState? state, string? code)
    {
        if (state is { State: HelperStates.Approved }) return false;
        if (state is { State: HelperStates.Waiting } kept && kept.Code == code) return false;
        files.WriteState(new HelperState(ask.Id, HelperStates.Waiting, code, null, DateTimeOffset.UtcNow));
        return true;
    }

    /// <summary>
    /// An ask refused: its state <c>refused</c> with the code, and a coded line on the asker's record saying the agent (where one
    /// was chosen) and why. Never an account's name or a path.
    /// </summary>
    private Task<string> RefuseAsync(HelperAsk ask, string code, string? agent, HelperFiles files, CancellationToken ct)
    {
        var kept = files.ReadState(ask.Id);
        files.WriteState(new HelperState(ask.Id, HelperStates.Refused, code, kept?.Helper, DateTimeOffset.UtcNow) { Agent = agent ?? kept?.Agent });
        var note = HelperNotes.Refused(code, agent ?? kept?.Agent);
        if (code != HelperWhy.AskerGone)
        {
            _context.Output?.Append(ask.Asker, $"— {note.Note}");
            _context.Events.Keep(ask.Asker, new SessionEvent { Kind = SessionEventKind.Note, Text = note.Note, Parts = note.Parts }, say: null);
        }

        return Task.FromResult($"helper  ask `{ask.Id}` refused ({code}): {HelperWhy.Said(code)}.");
    }

    /// <summary>One ask taken up: its agent chosen, its pass run, and what the helper said settled and delivered.</summary>
    private async Task<string> RunHelperAsync(HelperAsk ask, PriorSession asker, HelperRule rule, HelperFiles files, CancellationToken ct)
    {
        string? tip = null, root = null;
        if (asker.Tree is { Length: > 0 } tree) (tip, root) = await ReadTreeAsync(tree, ct).ConfigureAwait(false);
        if (tip is null || root is null)
        {
            return await RefuseAsync(ask, HelperWhy.Unreadable, null, files, ct).ConfigureAwait(false);
        }

        var choice = _context.HelperChoices is { } choose
            ? await choose(ask, rule, asker, ct).ConfigureAwait(false)
            : await HelperChoice.ChooseAsync(_context.Harnesses, ask, rule, asker.Adapter, _context.Config, asker.Workspace, ct).ConfigureAwait(false);
        if (!choice.Chosen)
        {
            // No agent ready: it waits, and the state says why (a cool-off, a sign-in, no other maker), tried again at a later look.
            Wait(files, ask, files.ReadState(ask.Id), choice.Code ?? ReviewerUnavailable.Refused);
            return $"helper  ask `{ask.Id}` waits: {choice.Sentence}";
        }

        var agent = choice.Reviewer!;
        var passAsk = new HelperPassAsk(ask, asker.Repository ?? "", root, tip)
        {
            Workspace = asker.Workspace,
            Opened = helper =>
            {
                // 🔴 Started, naming the helper, BEFORE its turn: `helper_answer` finds its ask by this state.
                files.WriteState(new HelperState(ask.Id, HelperStates.Started, null, helper, DateTimeOffset.UtcNow) { Agent = agent });
                var asked = HelperNotes.Asked(agent, helper);
                _context.Output?.Append(ask.Asker, $"— {asked.Note}");
                _context.Events.Keep(ask.Asker, new SessionEvent { Kind = SessionEventKind.Note, Text = asked.Note, Parts = asked.Parts }, say: null);
                var helping = HelperNotes.Helping(ask.Asker);
                _context.Output?.Append(helper, $"— {helping.Note}");
                _context.Events.Keep(helper, new SessionEvent { Kind = SessionEventKind.Note, Text = helping.Note, Parts = helping.Parts }, say: null);
            },
        };
        var run = await (_context.HelperPasses ?? HelperPassAsync)(passAsk, choice, ct).ConfigureAwait(false);
        if (run.Session is null)
        {
            return await RefuseAsync(ask, run.Code ?? HelperWhy.NotStarted, agent, files, ct).ConfigureAwait(false);
        }

        return await SettleHelperAsync(ask, files, run.State, run.LastWords, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// What the helper said, settled once its turn ended: the answer <c>helper_answer</c> wrote, else its last words bounded as a
    /// parked session's are (only where the turn completed), else <c>refused</c> with <c>no-answer</c>; then delivered.
    /// </summary>
    private async Task<string> SettleHelperAsync(HelperAsk ask, HelperFiles files, string? ended, string? lastWords, CancellationToken ct)
    {
        var state = files.ReadState(ask.Id);
        if (state is not { State: HelperStates.Started, Helper: { } helper }) return $"helper  ask `{ask.Id}` is not started: nothing to settle.";

        if (files.ReadAnswer(ask.Id) is { } answer && string.Equals(answer.Helper, helper, StringComparison.Ordinal))
        {
            state = state with { State = HelperStates.Answered, At = DateTimeOffset.UtcNow };
        }
        else if (ended == "completed" && lastWords is { Length: > 0 } words)
        {
            state = state with { State = HelperStates.Answered, At = DateTimeOffset.UtcNow, LastWords = true, Words = words };
        }
        else
        {
            return await RefuseAsync(ask, HelperWhy.NoAnswer, state.Agent, files, ct).ConfigureAwait(false);
        }

        files.WriteState(state);
        return await DeliverHelperAsync(ask, state, files, ct).ConfigureAwait(false)
               ?? $"helper  ask `{ask.Id}` is answered; the answer reaches session {ask.Asker} at its turn's end, never into a step in flight.";
    }

    /// <summary>
    /// A <c>started</c> ask nothing in this process runs (a restart ended its pass): its helper's record read, and if that ended the
    /// ask is settled from what the helper kept, its last words read from its transcript. A helper still running is left.
    /// </summary>
    private async Task<string?> SettleStartedAsync(HelperAsk ask, HelperState state, HelperFiles files, CancellationToken ct)
    {
        if (state.Helper is not { } helper) return null;
        var record = await _context.Service.RecordAsync(helper, ct).ConfigureAwait(false);
        if (record is { Running: true }) return null;

        var lastWords = record is { State: "completed" }
            ? SessionLastWords.ParkedWords(_context.Events, helper, DaorisHome.Entry(_context.Home, HomeEntry.Sessions, $"{helper}.log"))
            : null;
        return await SettleHelperAsync(ask, files, record?.State, lastWords, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Hand an answered ask's answer to the asker (design §3), once, through the local host's door, as the opinion's findings are:
    /// a word marked as the helper's, Daoris's words around the claim. A running or parked quest asker takes it at its turn's end,
    /// never into a step in flight, so it waits; one that never goes on is refused with its code. A conversation asker (MIXAGENT2a-iii)
    /// takes it open between turns, and ended: a shown event is left for the shell's chat runner, which says the word as its next
    /// turn, or goes the ended one on with it.
    /// </summary>
    private async Task<string?> DeliverHelperAsync(HelperAsk ask, HelperState state, HelperFiles files, CancellationToken ct)
    {
        var service = _context.Service;
        var output = _context.Output;
        var record = await service.RecordAsync(ask.Asker, ct).ConfigureAwait(false);
        var never = record switch
        {
            null => HelperWhy.AskerGone,
            { Serves: not null } or { Opinion: not null } or { Ask.Length: > 0 } => HelperWhy.AskerUnfit,
            _ when string.Equals(record.State, "stood-down", StringComparison.OrdinalIgnoreCase) => HelperWhy.CannotTake,
            _ => null,
        };
        if (never is not null) return await RefuseAsync(ask, never, state.Agent, files, ct).ConfigureAwait(false);

        // Never into a step in flight (D136): a running turn takes another agent's claim at its end, and a park after the person's
        // answer. Said nowhere: each look would say it again, and the next look after the record ended hands it.
        // A conversation open between turns is `working` and takes it (its runner says it as the next turn); one still starting waits.
        // 🔴 COWORK1d (the message-urgency design §5): an urgent ask's answer reaches a running asker this loop runs at its next step,
        // where its door takes words then, kept on its record for its run and held in its inbox under the record's id.
        var conversation = record!.Kind == "chat";
        var open = conversation && string.Equals(record.State, "working", StringComparison.OrdinalIgnoreCase);
        var inbox = ask.Urgent && !conversation && record.Running ? _context.Processes.InboxOf(ask.Asker) : null;
        var nextStep = inbox?.ReachAt(MessageLevel.Urgent) == DrivenReach.NextStep;
        if (!open && !nextStep && (record.Running || record.Parked)) return null;

        var text = state.LastWords
            ? state.Words ?? ""
            : files.ReadAnswer(ask.Id)?.Answer ?? "";
        var agent = state.Agent ?? "another agent";
        var composed = HelperWords.Compose(agent, state.Helper!, ask.Question, text, state.LastWords);
        var (handed, message, word) = await service.Helpers
            .HandAsync(state.Helper!, composed, ct, running: nextStep, level: ask.Urgent ? MessageLevels.Spelled(MessageLevel.Urgent) : null)
            .ConfigureAwait(false);
        if (!handed) return $"helper  ask `{ask.Id}` is answered, and waits to be handed to session {ask.Asker}: {message}";

        // Held where the run takes it at its next step; one the inbox no longer takes stays on the record, which goes on with it once.
        var atNextStep = nextStep && word is not null && inbox!.HoldKept(
            new ChatMessage(composed, [])
            {
                Id = word, Level = MessageLevel.Urgent,
                Helped = new SaidWordView(word, composed, DateTimeOffset.UtcNow, [], true) { Helper = state.Helper, Level = MessageLevel.Urgent },
            },
            new SaidDoor(service, ask.Asker));
        files.WriteState(state with { Handed = word ?? "handed" });
        var noted = HelperNotes.Answered(agent, state.Helper!, state.LastWords);
        output?.Append(ask.Asker, $"— {noted.Note}");
        _context.Events.Keep(ask.Asker, new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts }, say: null);

        // The shell's chat runner hears this event, as it hears the person's words to an ended conversation (D137 §3.1), after the
        // note above so the turn it makes reads beneath it: the origin is Daoris's own (`target`), never the person's, and an
        // open conversation says the word as its next turn.
        if (conversation && word is not null)
        {
            _context.Events.Keep(
                ask.Asker,
                new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Helper = state.Helper, Id = word, Text = composed, Reaches = "resume" },
                say: null);
        }

        return atNextStep
            ? $"helper  ask `{ask.Id}` answered by {agent}: its answer reaches session {ask.Asker} at its next step, marked as the helper's."
            : $"helper  ask `{ask.Id}` answered by {agent}: its answer is session {ask.Asker}'s next turn, marked as the helper's, at the driver's next look.";
    }

    /// <summary>
    /// The helper's pass (MIXAGENT2a-ii; the reviewer's machinery, <see cref="PassAsync"/>): a read-only clone of the asker's
    /// repository at its tree's last commit, its record a chat that serves the asker, one turn under the reviewer's read-only rules
    /// with its one tool <c>helper_answer</c>, and the clone removed however it ended. A choice with no agent starts nothing.
    /// </summary>
    internal async Task<HelperPassRun> HelperPassAsync(HelperPassAsk ask, ReviewerChoice choice, CancellationToken ct = default)
    {
        if (choice is not { Chosen: true, Selection: { Allowed: true } selection, Reviewer: { } helper })
        {
            return new HelperPassRun(null, $"held  helper for ask `{ask.Ask.Id}`: {choice.Sentence}") { Code = HelperWhy.NotStarted };
        }

        ISessionAdapter adapter;
        try
        {
            adapter = _context.Adapters.Resolve(helper);
        }
        catch (DriverException error)
        {
            return new HelperPassRun(null, $"held  helper for ask `{ask.Ask.Id}`: {error.Message}") { Code = HelperWhy.NotStarted };
        }

        OpinionTreeOpened copy;
        try
        {
            copy = await OpinionTree.OpenAsync(_context.Home, ask.Root, ask.Repository, ask.Workspace, ask.Tip, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return new HelperPassRun(null, $"held  helper for ask `{ask.Ask.Id}`: {error.Message}") { Code = HelperWhy.Unreadable };
        }

        HelperPassRun run;
        string? left;
        try
        {
            run = await ReadHelperInCopyAsync(ask, adapter, selection, helper, copy, ct).ConfigureAwait(false);
        }
        finally
        {
            // Removed however the run ended, the person's stop and the driver's own included: nothing in it is read back.
            left = await OpinionTree.RemoveAsync(_context.Home, copy.Path).ConfigureAwait(false);
        }

        return left is null ? run : run with { Line = $"{run.Line} {left}" };
    }

    /// <summary>The helper's turn from its record's open to its record's end, in its copy.</summary>
    private async Task<HelperPassRun> ReadHelperInCopyAsync(
        HelperPassAsk ask, ISessionAdapter adapter, HarnessSelection selection, string agent, OpinionTreeOpened copy, CancellationToken ct)
    {
        var service = _context.Service;
        var home = _context.Home;
        var output = _context.Output;
        var named = $"ask `{ask.Ask.Id}`";
        var opened = await service.Helpers.OpenAsync(ask.Ask.Asker, agent, copy.Path, selection.Profile, selection.Version, ask.Tip, ct).ConfigureAwait(false);
        if (!opened.Opened)
        {
            return new HelperPassRun(null, $"refused  helper for {named}: {opened.Message}") { Code = HelperWhy.NotStarted };
        }

        var sessionId = opened.Session!;
        // Marked as this process's start until its spawn's marker replaces it (QUEUED1).
        _context.Processes.MarkOpening(sessionId);
        // The state names the helper now, before its turn (helper_answer finds its ask by it).
        ask.Opened?.Invoke(sessionId);
        HelperPassRun Ended(string state, string note, string? lastWords = null) =>
            new(sessionId, $"{state}  helper {sessionId} ({named}): {note}") { State = state, LastWords = lastWords };
        // The record says who asked: an opening's line, then the helper's own on another account of the order, as a quest's start does.
        RotatedOpening.Say(service, _context.Events, sessionId, agent, selection, carried: null);
        var bound = HelperPass.Bound(_context.Config);
        var posture = OpinionPosture.Of(adapter);

        try
        {
            var packet = new HelperPacket(ask.Ask.Asker, ask.Repository, ask.Tip, ask.Ask.Question, bound);
            var target = new SessionTarget(
                QuestId: "", $"Helper for session {ask.Ask.Asker}", "", "Daoris", ask.Repository, copy.Path, service.BaseUrl)
            {
                Helper = ask.Ask.Id,
                Session = sessionId,
                Prompt = HelperInstruction.Compose(packet),
            };
            var (info, harnessNotice) = _driver.Prepare(adapter, target, selection);
            await _driver.KeepHandedAsync(sessionId, info, ct).ConfigureAwait(false);
            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            // Its connector and nothing else, as a reviewer's is: no plugin's server, which could write where a helper may not.
            string? handed = null;
            string? preamble = null;
            if (adapter.Wire == SessionWire.Pipe)
            {
                var connector = _driver.Connector(sessionId, scope: null);
                if (connector is null) preamble = HelperPass.NoConnector;
                handed = SpawnServers.Hand(adapter, info, home, sessionId, connector is null ? [] : [connector]);
            }

            // HELPPLUG1c (D170 point 4): the skills plugins hand sessions, as a reviewer's are handed them: words, never a tool.
            var (skills, skillPlugins) = SpawnSkills.Hand(adapter, info, home, sessionId, _context.Catalog, PluginServerTo.Sessions);
            using var skilled = new Disposer(() => SpawnSkills.Remove(skills));

            // What it may do: the person's composition under the reviewer's read-only shape with `helper_answer`, held to its copy
            // by the tree guard.
            var handedRules = _driver.HandRules(
                adapter, info, sessionId, ask.Workspace, ask.Repository, tree: copy.Path, kept: null, opinion: true, helper: true,
                plugins: skillPlugins);
            var opening = HelperPass.Opening(ask.Repository, ask.Tip, posture);

            return await _driver.HoldAsync(
                adapter, info, target, sessionId, transcript, copy.Path, Driver.JoinNotices(harnessNotice, opening), handedRules, handed,
                // One turn, and no words: nobody talks to a helper while it reads, on either door.
                refusesInput: HelperPass.TakesNoWords(ask.Ask.Id),
                ct: ct,
                preamble: Driver.JoinNotices(preamble, opening),
                handedServers: [],
                said: _driver.Said(adapter, selection, sessionId),
                minutes: bound,
                noConnector: HelperPass.NoConnector,
                conclude: async (exitCode, used, turnFailed, ended) =>
                {
                    if (used is not null)
                    {
                        _context.Usage?.Record(new UsageEntry(
                            sessionId, ask.Repository, adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    var byPerson = _context.Processes.WasStopRequested(sessionId);
                    var conclusion = byPerson
                        ? SessionConclusion.Of("stopped", _context.Processes.StopNote(sessionId) ?? Observation.Stopped)
                        : exitCode is int code
                            ? ReadHelper(ask.Ask.Asker, code, turnFailed)
                            : SessionConclusion.Of("failed", Observation.TimedOut(bound));
                    conclusion = _driver.AccountRefused(conclusion, adapter, selection, ended);
                    conclusion = _driver.AccountSignedOut(conclusion, adapter, selection, turnFailed);
                    (conclusion, var limited) = _driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);

                    await service.AdvanceAsync(
                        sessionId, conclusion.State, note: conclusion.Note, ct: ct, limit: limited, parts: conclusion.Parts).ConfigureAwait(false);
                    // Its last words stand as the answer only where its turn completed and it used no tool (design §3).
                    var lastWords = conclusion.State == "completed" ? SessionLastWords.ParkedWords(_context.Events, sessionId, transcript) : null;
                    return Ended(conclusion.State, conclusion.Note, lastWords);
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The person is closing the driver; the record says so rather than sit at "working".
            try
            {
                await service.AdvanceAsync(sessionId, "stopped", Observation.DriverClosed, ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort by construction: the host may already be gone on the same shutdown.
            }

            return Ended("stopped", "the driver was stopped.");
        }
        catch (Exception error)
        {
            // Everything but the shutdown above, a client timeout included, as a reviewer's twin.
            try
            {
                await service.AdvanceAsync(sessionId, "failed", Observation.Failure(error.Message), ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The terminal write is best-effort by construction: the first failure is the report.
            }

            return Ended("failed", error.Message);
        }
        finally
        {
            output?.Close(sessionId);
        }
    }

    /// <summary>
    /// How a helper's turn ended, from its exit and its door (D46 §4): a clean exit is <c>completed</c>, whatever it said; a refused
    /// turn or another exit is <c>failed</c>. Whether it answered is the answer file's, which the settling reads.
    /// </summary>
    internal static SessionConclusion ReadHelper(string asker, int exitCode, string? turnFailed) =>
        turnFailed is { Length: > 0 } failed
            ? SessionConclusion.Of("failed", Observation.Failure(failed))
            : exitCode != 0
                ? SessionConclusion.Of("failed", Noted.Of(NoteCodes.EndedExit, $"it exited with code {exitCode}.", ("exit", exitCode)))
                : SessionConclusion.Of("completed", HelperNotes.Helping(asker));
}
