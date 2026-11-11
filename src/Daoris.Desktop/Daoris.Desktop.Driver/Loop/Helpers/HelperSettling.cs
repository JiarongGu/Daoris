using static Daoris.Driver.DriverHelpers;

namespace Daoris.Driver;

/// <summary>One helper ask taken up: its agent chosen, its pass run, and what the helper said settled and delivered to its asker.</summary>
internal sealed class HelperSettling(DriverHelpers owner, LoopContext context)
{
    private readonly LoopContext _context = context;

    /// <summary>One ask taken up: its agent chosen, its pass run, and what the helper said settled and delivered.</summary>
    internal async Task<LookLine> RunHelperAsync(HelperAsk ask, PriorSession asker, HelperRule rule, HelperFiles files, CancellationToken ct)
    {
        string? tip = null, root = null;
        var folder = asker.Tree is { Length: > 0 } named ? new LinePlaces(_context.Home).Of(named).Folder : null;
        if (folder is not null) (tip, root) = await owner.ReadTreeAsync(folder, ct).ConfigureAwait(false);
        if (tip is null || root is null)
        {
            return await owner.RefuseAsync(ask, HelperWhy.Unreadable, null, files, ct).ConfigureAwait(false);
        }

        // The actor gives each agent its model, and a refusal at the open is asked again inside this call.
        Task<ReviewerChoice> Walk(IReadOnlyList<ReviewerPassed>? passed) =>
            _context.HelperChoices is { } choose
                ? choose(ask, rule, asker, ct)
                : HelperChoice.ChooseAsync(_context.Harnesses, ask, rule, asker.Adapter, _context.Config, asker.Workspace, ct, passed, () => RoleCatalog.ReadHome(_context.Harnesses.Home));
        var choice = await Walk(null).ConfigureAwait(false);
        if (!choice.Chosen)
        {
            // No agent ready: it waits, and the state says why (a cool-off, a sign-in, no other maker), tried again at a later look.
            Wait(files, ask, files.ReadState(ask.Id), choice.Code ?? ReviewerUnavailable.Refused);
            return Helper(LookClass.Retry, $"helper  ask `{ask.Id}` waits: {choice.Sentence}");
        }

        var agent = choice.Reviewer!;

        // The asker's work in progress, captured whole with its manifest at this
        // turn boundary, or refused in its code; its manifest kept before the helper starts, and its commit the copy's.
        var read = await owner.CaptureAsync(folder!, ct).ConfigureAwait(false);
        if (read.Captured is not { } capture)
        {
            return await owner.RefuseAsync(ask, read.Code ?? HelperWhy.Unreadable, null, files, ct, read.Detail).ConfigureAwait(false);
        }

        files.WriteCapture(ask.Id, capture);
        var passAsk = new HelperPassAsk(ask, asker.Repository ?? "", root, capture.Commit)
        {
            Capture = capture,
            Workspace = asker.Workspace,
            Reask = Walk,
            Replaced = next => agent = next.Reviewer!,
            Opened = helper =>
            {
                // 🔴 Started, naming the helper, BEFORE its turn: `helper_answer` finds its ask by this state. A pass whose agent refused the
                // entry's model at the open calls this again with the replacement, so the state names the session that runs.
                files.WriteState(new HelperState(ask.Id, HelperStates.Started, null, helper, DateTimeOffset.UtcNow) { Agent = agent });
                var asked = HelperNotes.Asked(agent, helper);
                _context.Output?.Append(ask.Asker, $"— {asked.Note}");
                _context.Events.Keep(ask.Asker, new SessionEvent { Kind = SessionEventKind.Note, Text = asked.Note, Parts = asked.Parts }, say: null);
                var helping = HelperNotes.Helping(ask.Asker);
                _context.Output?.Append(helper, $"— {helping.Note}");
                _context.Events.Keep(helper, new SessionEvent { Kind = SessionEventKind.Note, Text = helping.Note, Parts = helping.Parts }, say: null);
            },
        };
        var run = await (_context.HelperPasses ?? owner.HelperPassAsync)(passAsk, choice, ct).ConfigureAwait(false);
        if (run.Session is null)
        {
            return await owner.RefuseAsync(ask, run.Code ?? HelperWhy.NotStarted, agent, files, ct).ConfigureAwait(false);
        }

        return await SettleHelperAsync(ask, files, run.State, run.LastWords, ct, run.Limit, run.Transient).ConfigureAwait(false);
    }

    /// <summary>
    /// What the helper said, settled once its turn ended: the answer <c>helper_answer</c> wrote, else its last words bounded as a
    /// parked session's are (only where the turn completed), else, where an account's limit ended it, <c>cooling</c> with that account's
    /// reset, else <c>refused</c> with <c>no-answer</c>; then delivered.
    /// </summary>
    /// <param name="limited">An account's limit ended the helper's turn: its record's flag.</param>
    /// <param name="transient">Its provider kept failing past the retry's bound: refused <see cref="HelperWhy.Transient"/>.</param>
    private async Task<LookLine> SettleHelperAsync(
        HelperAsk ask, HelperFiles files, string? ended, string? lastWords, CancellationToken ct, bool limited = false, bool transient = false)
    {
        var state = files.ReadState(ask.Id);
        if (state is not { State: HelperStates.Started, Helper: { } helper }) return Helper(LookClass.Progress, $"helper  ask `{ask.Id}` is not started: nothing to settle.");

        if (files.ReadAnswer(ask.Id) is { } answer && string.Equals(answer.Helper, helper, StringComparison.Ordinal))
        {
            state = state with { State = HelperStates.Answered, At = DateTimeOffset.UtcNow };
        }
        else if (ended == "completed" && lastWords is { Length: > 0 } words)
        {
            state = state with { State = HelperStates.Answered, At = DateTimeOffset.UtcNow, LastWords = true, Words = words };
        }
        else if (limited)
        {
            // Never refused here, so a lead that waits on it keeps waiting (`HelperWaits.Open` counts it
            // open). The next look runs it again while its asker waits, the walk passing the cooling account, and refuses it once not.
            var until = AccountCooling.OfSession(_context.Home, helper)?.Until;
            files.WriteState(state with { State = HelperStates.Cooling, At = DateTimeOffset.UtcNow, Until = until });
            return Helper(LookClass.Progress,
                $"helper  ask `{ask.Id}` is cooling: helper {helper}'s account hit its limit before it answered; it runs again while session "
                + $"{ask.Asker} waits on it, on another account or once that account is ready{(until is { } ready ? $" ({Moments.Write(ready)})" : "")}.");
        }
        else
        {
            return await owner.RefuseAsync(ask, transient ? HelperWhy.Transient : HelperWhy.NoAnswer, state.Agent, files, ct).ConfigureAwait(false);
        }

        files.WriteState(state);
        return await DeliverHelperAsync(ask, state, files, ct).ConfigureAwait(false)
               ?? Helper(LookClass.Progress, $"helper  ask `{ask.Id}` is answered; the answer reaches session {ask.Asker} at its turn's end, never into a step in flight.");
    }

    /// <summary>
    /// A <c>started</c> ask nothing in this process runs (a restart ended its pass): its helper's record read, and if that ended the
    /// ask is settled from what the helper kept, its last words read from its transcript. A helper still running is left. One the shell's
    /// close or the sweep cut off with no answer kept is approved again, so the look runs it anew with no word from the person.
    /// </summary>
    internal async Task<LookLine?> SettleStartedAsync(HelperAsk ask, HelperState state, HelperFiles files, CancellationToken ct)
    {
        if (state.Helper is not { } helper) return null;
        var record = await _context.Service.RecordAsync(helper, ct).ConfigureAwait(false);
        if (record is { Running: true }) return null;
        if (record is { State: "stopped", Interrupted: true } && files.ReadAnswer(ask.Id) is null)
        {
            files.WriteState(state with { State = HelperStates.Approved, At = DateTimeOffset.UtcNow });
            return Helper(LookClass.Progress,
                $"helper  ask `{ask.Id}` runs again: Daoris restarted while helper {helper} worked, and session {ask.Asker} waits on it.");
        }

        var lastWords = record is { State: "completed" }
            ? SessionLastWords.ParkedWords(_context.Events, helper, DaorisHome.Entry(_context.Home, HomeEntry.Sessions, $"{helper}.log"))
            : null;
        return await SettleHelperAsync(ask, files, record?.State, lastWords, ct, record?.Limit == true).ConfigureAwait(false);
    }

    /// <summary>
    /// Hand an answered ask's answer to the asker, once, through the local host's door, as the opinion's findings are:
    /// a word marked as the helper's, Daoris's words around the claim. A running or parked quest asker takes it at its turn's end,
    /// never into a step in flight, so it waits; one that never goes on is refused with its code. A conversation asker
    /// takes it open between turns, and ended: a shown event is left for the shell's chat runner, which says the word as its next
    /// turn, or goes the ended one on with it.
    /// </summary>
    /// <param name="seen">How the look's own list showed the asker (<see cref="Seen"/>), kept with a hand held; null from a pass's own settling.</param>
    internal async Task<LookLine?> DeliverHelperAsync(HelperAsk ask, HelperState state, HelperFiles files, CancellationToken ct, string? seen = null)
    {
        // An answer kept for the person reaches no part of the line until they pass it. The helper's
        // own conversation holds it, and nothing counts it, since it never reached the lead by itself.
        if (ask.ForPerson && state.Passed is null)
        {
            files.WriteState(state with { Handed = HelperState.KeptForPerson });
            owner.Forget(ask.Id);
            return Helper(LookClass.Progress, $"helper  ask `{ask.Id}` is answered for the person: it reaches session {ask.Asker} only once they pass it to the lead.");
        }

        var service = _context.Service;
        // The host's one door for solicited answers keys the hand and walks the line to the store's bound
        // itself, and the look reads the head as that door does (SolicitedHand).
        // The newest record of the asker's line, as the hand door resolves it, so a left conversation is never
        // reopened and the answer goes to the part that continues it.
        var (asker, record, _, beyond) = Walked(SolicitedHand.Walk(await service.RecordsAsync(ct).ConfigureAwait(false), ask.Asker), ask.Asker);
        if (beyond is not null)
        {
            owner.Forget(ask.Id);
            return await owner.RefuseAsync(ask, HelperWhy.CannotTake, state.Agent, files, ct, beyond).ConfigureAwait(false);
        }

        var never = (asker, record) switch
        {
            (null, _) or (_, null) => HelperWhy.AskerGone,
            ({ Serves: not null } or { Opinion: not null } or { Ask.Length: > 0 }, _) => HelperWhy.AskerUnfit,
            (_, { Serves: not null } or { Opinion: not null } or { Ask.Length: > 0 }) => HelperWhy.AskerUnfit,
            _ when string.Equals(record!.State, "stood-down", StringComparison.OrdinalIgnoreCase) => HelperWhy.CannotTake,
            _ => null,
        };
        if (never is not null)
        {
            owner.Forget(ask.Id);
            return await owner.RefuseAsync(ask, never, state.Agent, files, ct).ConfigureAwait(false);
        }

        var to = record!.Session;

        // Never into a step in flight: a running turn takes another agent's claim at its end, and a park after the person's
        // answer. Said once, and not tried again until the asker moves; the next look after it ended hands it.
        // 🔴 A park the person answered takes it now, beside their answer, so its
        // go-on hands both in one prompt; an unanswered park holds it, a park made before the rule included, since nothing on it proves
        // it asked the person nothing (its question may stand in its closing words alone): the person's answer moves it, never the helper's.
        // A conversation open between turns is `working` and takes it (its runner says it as the next turn); one still starting waits.
        // 🔴 An urgent ask's answer reaches a running asker this loop runs at its next step,
        // where its door takes words then, kept on its record for its run and held in its inbox under the record's id.
        var open = record.Kind == "chat" && string.Equals(record.State, "working", StringComparison.OrdinalIgnoreCase);
        var inbox = owner.NextStepInbox(ask, record);
        var nextStep = inbox is not null;
        if (!open && !nextStep && (record.Running || record is { Parked: true, AnsweredPark: false })) return owner.Held(ask, record, seen);

        // 🔴 One hand per ask: the attempt mark, written before the door, says a hand
        // began; through the one door it is keyed, and an attempt is asked again under the key inside the settlement's horizon, never read
        // back here. Past the horizon it is settled as uncertain for the person, the answer kept in the helper's conversation.
        var now = DateTimeOffset.UtcNow;
        if (state.Attempted is not null && SolicitedHand.Before(state.Attempted, now) == SolicitedStep.Uncertain)
        {
            owner.Forget(ask.Id);
            return await owner.RefuseAsync(ask, HelperWhy.HandUncertain, state.Agent, files, ct).ConfigureAwait(false);
        }

        var text = state.LastWords
            ? state.Words ?? ""
            : files.ReadAnswer(ask.Id)?.Answer ?? "";
        var agent = state.Agent ?? "another agent";
        var composed = HelperWords.Compose(agent, state.Helper!, ask.Question, text, state.LastWords, files.ReadCapture(ask.Id));
        // The attempt mark, before the door (a write that fails hands nothing): kept until the door's answer is written. A retry keeps the
        // first attempt's moment, which the horizon counts from.
        files.WriteState(state with { Attempted = state.Attempted ?? now });
        var answer = await service.Helpers
            .HandAsync(state.Helper!, composed, ct, running: nextStep, level: ask.Urgent ? MessageLevels.Spelled(MessageLevel.Urgent) : null)
            .ConfigureAwait(false);
        // A hand the host refused kept nothing, so its mark goes; it is said once, and asked again only once the look shows
        // its asker moved, never at every look.
        if (answer.Fate == SolicitedFate.Refused)
        {
            files.WriteState(state with { Attempted = null });
            return owner.Unhanded(ask, seen, Helper(LookClass.Retry, $"helper  ask `{ask.Id}` is answered, and waits to be handed to session {to}: {answer.Message}"));
        }

        // 🔴 An answer lost after the door may have kept the word, so the mark stays and the
        // outcome is uncertain until the next look: the one door is asked again under the key, which answers the word waiting or settled. It
        // is never handed again.
        if (answer.Fate == SolicitedFate.Uncertain)
        {
            owner.Forget(ask.Id);
            return Helper(LookClass.Retry,
                $"helper  ask `{ask.Id}`'s hand to session {to} is uncertain: {answer.Message}. The next look asks again under its key, which answers it once.");
        }

        // The host answered the hand settled: its key's word left a record of the asker's line and
        // is kept and dispatched by an earlier hand whose answer this look lost. Nothing is dispatched and no event is recorded, as for a
        // word handed already: the ask is marked handed, never handed again.
        if (answer.Fate == SolicitedFate.Settled)
        {
            files.WriteState(state with { Handed = "settled", Attempted = null });
            owner.Forget(ask.Id);
            return Helper(LookClass.Progress, $"helper  ask `{ask.Id}`'s answer was handed to session {to} already: its word is settled, and it is not handed again.");
        }

        // Dispatched before the state's mark, so a mark that fails to write leaves a word the next look completes, idempotently,
        // rather than one marked handed that nothing here is told of. On the record the one door kept it on, or found it
        // waiting on, which a hand-over may have made another part than the look read; a word already waiting is dispatched as it waits.
        var holder = answer.Holder is { } held && held.Session != record.Session ? held : record;
        // A passed answer goes with the person's press beside it, before the turn that takes it, so the count reads their word.
        if (state.Passed is not null && answer.Fate != SolicitedFate.Waiting)
        {
            _context.Events.Keep(holder.Session, AnswersAloneNotes.Passed(answer.Word is { } passed ? [passed] : []), say: null);
        }

        var atNextStep = owner.Dispatch(
            state, holder, answer.Word, answer.Fate == SolicitedFate.Waiting ? answer.Text ?? composed : composed,
            ReferenceEquals(holder, record) ? inbox : owner.NextStepInbox(ask, holder), _context.Events);
        files.WriteState(state with { Handed = answer.Word ?? "handed", Attempted = null });
        owner.Forget(ask.Id);

        if (answer.Fate == SolicitedFate.Waiting)
        {
            return Helper(LookClass.Progress, $"helper  ask `{ask.Id}` was handed to session {holder.Session} already, as word `{answer.Word}`: it is not handed again.");
        }

        return Helper(LookClass.Progress, atNextStep
            ? $"helper  ask `{ask.Id}` answered by {agent}: its answer reaches session {holder.Session} at its next step, marked as the helper's."
            : $"helper  ask `{ask.Id}` answered by {agent}: its answer is session {holder.Session}'s next turn, marked as the helper's, at the driver's next look.");
    }

    /// <summary>
    /// The asker, the head of its line and the line as the one door walks it (<see cref="SolicitedHand.Walk"/>), with why no part is
    /// handed the answer where the walk read no head: a line that loops, or one past the store's bound.
    /// </summary>
    private static (PriorSession? Asker, PriorSession? Newest, IReadOnlyList<PriorSession> Line, string? Beyond) Walked(SolicitedLine line, string asker) =>
        line.End switch
        {
            SolicitedLineEnd.Cycle => (line.Named, null, line.Records,
                $"session {asker}'s line loops back on a part it already passed, so it has no newest part and none is handed the answer; the helper's conversation keeps it"),
            SolicitedLineEnd.Bound => (line.Named, null, line.Records,
                $"session {asker}'s line runs past {SolicitedHand.Bound} parts, so its newest part is not read and no part it left is handed the answer; the helper's conversation keeps it"),
            _ => (line.Named, line.Head, line.Records, null),
        };
}
