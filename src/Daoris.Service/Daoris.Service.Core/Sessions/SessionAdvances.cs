using System.Globalization;

namespace Daoris.Knowledge;

/// <summary>
/// What may move where: a session's lifecycle (the states each may become), the one move out of an ended state (a go-on with the person's
/// words waiting) and the second (a park after a helper that answered nothing), each judged and written as one step.
/// </summary>
internal sealed class SessionAdvances(QuestStore quests, SessionStore sessions, AskStore? asks, SessionHolders holders)
{
    private static string Spell(SessionState state) => Session.Spell(state);

    private const string AdvanceTargets =
        "starting, working, awaiting-person, completed, declined, stood-down, failed, stopped";

    /// <summary>
    /// What each live state may become. Forward only, and every live state can be stopped — the person's
    /// hand is never refused.
    /// </summary>
    private static IReadOnlyList<SessionState> Allowed(SessionState from) => from switch
    {
        SessionState.Queued =>
            [SessionState.Starting, SessionState.StoodDown, SessionState.Failed, SessionState.Stopped],
        SessionState.Starting =>
            [SessionState.Working, SessionState.StoodDown, SessionState.Failed, SessionState.Stopped],
        SessionState.Working =>
            [SessionState.AwaitingPerson, SessionState.Completed, SessionState.Declined,
             SessionState.StoodDown, SessionState.Failed, SessionState.Stopped],
        SessionState.AwaitingPerson =>
            [SessionState.Working, SessionState.Completed, SessionState.Declined, SessionState.Stopped],
        _ => [],
    };

    /// <summary>
    /// Shared with every other door through <see cref="Session.TryParse"/> — the strict one. A bare
    /// Enum.TryParse also accepts numeric strings, turning "99" into an undefined state that escapes
    /// the unknown-state branch.
    /// </summary>
    private static SessionState? Parse(string state) =>
        Session.TryParse(state, out var parsed) ? parsed : null;

    /// <summary>
    /// Move a session, attaching what the move carries. States arrive as text because they travel over
    /// HTTP in the design's spelling — `awaiting-person`, `stood-down` — and a door should accept what
    /// it prints. A finished session does not move, but for one move: to working, with the person's words
    /// waiting on a record of this machine's.
    /// </summary>
    /// <param name="interrupted">
    /// That a stop is not the person's: the orphan sweep's, or the driver's shutdown. Only a move
    /// to <c>stopped</c> may say so, since it says whose decision a stop is.
    /// </param>
    /// <param name="limit">
    /// That an account's limit refused the turn, as the driver read it from the door's
    /// failure. Only a move to <c>failed</c> may say so, since it says why a turn failed.
    /// </param>
    /// <param name="noteParts">
    /// The note's parts beside it, as <see cref="NoteParts.Normalize"/> keeps them; only with a note.
    /// A note moved without them clears the record's.
    /// </param>
    /// <param name="expected">
    /// The state the record must be in for the move to be made, compared inside the step: a finish under
    /// the standing confirmation sends <c>awaiting-person</c>, so a session resumed between the verb's read and the move is not ended.
    /// Null moves from any state the move allows, as before.
    /// </param>
    /// <param name="unbindAsk">
    /// That the intake's ask is let go with its end: the ask's binding cleared in the
    /// same step, only where it still names this session, so the next look may give the ask to another intake. Only a move to <c>failed</c>
    /// may say so; the record stays, still naming its ask.
    /// </param>
    /// <param name="expect">
    /// The supervisor's words a move to working carries: the exact ids of the
    /// <see cref="SaidFrom.Supervisor"/> words its go-on admitted. The move is refused <see cref="SessionAdvanceRefusal.WordsChanged"/>
    /// where the record's waiting supervisor's words are not exactly these, and absent reads as none, so a mover that names none never
    /// carries one. Naming any needs the record parked.
    /// </param>
    /// <param name="turns">
    /// The turn lines of each session a completion releases memory for, by session: its
    /// rows name the models of the turn that holds their instant, and <c>unknown</c> where none is handed.
    /// </param>
    public async Task<SessionAdvanceOutcome> AdvanceAsync(
        string id, string state, string? note, string? evidence, string? transcript,
        DateTimeOffset now, CancellationToken ct = default, bool interrupted = false, bool limit = false, string? noteParts = null,
        string? expected = null, bool unbindAsk = false, IReadOnlyCollection<string>? expect = null,
        IReadOnlyDictionary<string, MemoryTurns>? turns = null, IReadOnlyList<string>? holdsPlace = null)
    {
        var target = Parse(state);
        if (target is null or SessionState.Queued)
        {
            // Queued is where a session begins, never where it returns — a fresh attempt is a new record.
            return new(
                SessionAdvanceRefusal.UnknownState,
                $"Unknown state '{state}' — one of: {AdvanceTargets}.",
                Session: null);
        }

        var from = expected is null ? null : Parse(expected);
        if (expected is not null && from is null)
        {
            return new(
                SessionAdvanceRefusal.UnknownState,
                $"Unknown expected state '{expected}' — one of: queued, {AdvanceTargets}.",
                Session: null);
        }

        if (interrupted && target != SessionState.Stopped)
        {
            return new(
                SessionAdvanceRefusal.InvalidMove,
                $"Only a move to stopped can be interrupted — it says a stop was not the person's, and "
                + $"session `{id}` was asked to move to {Spell(target.Value)}.",
                Session: null);
        }

        if (limit && target != SessionState.Failed)
        {
            return new(
                SessionAdvanceRefusal.InvalidMove,
                $"Only a move to failed can say an account's limit — it says why a turn failed, and "
                + $"session `{id}` was asked to move to {Spell(target.Value)}.",
                Session: null);
        }

        if (unbindAsk && target != SessionState.Failed)
        {
            return new(
                SessionAdvanceRefusal.InvalidMove,
                $"Only a move to failed can let an intake's ask go — it frees the ask for the next intake, and "
                + $"session `{id}` was asked to move to {Spell(target.Value)}.",
                Session: null);
        }

        if (holdsPlace is not null && target is SessionState.Starting or SessionState.Working)
        {
            return new(
                SessionAdvanceRefusal.InvalidMove,
                $"Only a move out of a running state can mark the uncommitted paths a line keeps its place with, and session `{id}` was "
                + $"asked to move to {Spell(target.Value)}.",
                Session: null);
        }

        // A completion in the main tree is its session's commit point, a move into working takes the place's lease there and a park
        // there takes none: the root is read before the step, as an open reads it.
        var mainTree = target is SessionState.Completed or SessionState.Working or SessionState.AwaitingPerson
                       && await sessions.FindAsync(id, ct).ConfigureAwait(false) is { } named
            ? Trees.Normalize(await holders.RootOfAsync(named.Repository, ct).ConfigureAwait(false))
            : null;

        // Read, judged and written as one step: the driver's conclusion and a person's stop,
        // arriving from two hosts, could both read the session active, and the second moved a finished one.
        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionAdvanceOutcome(SessionAdvanceRefusal.NotFound, $"No session `{id}`.", Session: null);
            }

            if ((!session.Active || session.State == SessionState.AwaitingPerson) && target != session.State
                && await sessions.Opens.LeftForAsync(id, inside).ConfigureAwait(false) is { } destination)
                return new SessionAdvanceOutcome(SessionAdvanceRefusal.Terminal,
                    $"Session `{id}` continued in `{destination}`; its old record does not move.", Session: null);

            if (target == SessionState.Working && Admits(session, expect) is { } changed) return changed;

            if (!session.Active)
            {
                return target == SessionState.Working && from is null
                    ? await GoOnAsync(session, note, noteParts, evidence, transcript, now, mainTree, inside).ConfigureAwait(false)
                    : target == SessionState.AwaitingPerson && SessionHolders.WaitedOnHelper(session)
                    ? await BackToPersonAsync(session, note, noteParts, now, mainTree, inside).ConfigureAwait(false)
                    : new SessionAdvanceOutcome(
                        SessionAdvanceRefusal.Terminal,
                        $"Session `{session.Id}` is {Spell(session.State)} — a finished session does not move.",
                        Session: null);
            }

            if (from is { } wanted && session.State != wanted)
            {
                return new SessionAdvanceOutcome(
                    SessionAdvanceRefusal.InvalidMove,
                    $"Session `{session.Id}` is {Spell(session.State)}, not {Spell(wanted)} as this move expected, so it was not moved.",
                    Session: null);
            }

            if (!Allowed(session.State).Contains(target.Value))
            {
                return new SessionAdvanceOutcome(
                    SessionAdvanceRefusal.InvalidMove,
                    $"Session `{session.Id}` cannot move {Spell(session.State)} → {Spell(target.Value)}. "
                    + $"From {Spell(session.State)}: {string.Join(", ", Allowed(session.State).Select(Spell))}.",
                    Session: null);
            }

            // A park going on in the place itself takes the writer's lease, as an open does; a pass takes none.
            if (session.State == SessionState.AwaitingPerson && target == SessionState.Working && Writes(session)
                && SessionHolders.InPlace(session.Tree, mainTree)
                && await holders.PlaceHolderAsync(session.Repository, mainTree, inside, except: id).ConfigureAwait(false) is { } leased)
            {
                return new SessionAdvanceOutcome(SessionAdvanceRefusal.Busy, SessionPlaceLease.Busy(session.Repository, leased), Session: null);
            }

            // A session that parks again asks anew: the answer it went on with is not this park's.
            var moved = await sessions.SetStateAsync(
                    id, target.Value, note, evidence, transcript, now, inside, interrupted, limit,
                    clearSaid: target == SessionState.AwaitingPerson, noteParts: noteParts)
                .ConfigureAwait(false);

            // A running state holds the place by itself, so its mark goes; the move out of one writes the paths it keeps, or none.
            if (target is SessionState.Starting or SessionState.Working
                || session.State is SessionState.Queued or SessionState.Starting or SessionState.Working)
            {
                var kept = target is SessionState.Starting or SessionState.Working ? null : holdsPlace;
                await holders.MarkPlaceAsync(id, kept, inside).ConfigureAwait(false);
                moved = moved! with { HoldsPlace = kept is { Count: > 0 } ? kept : null };
            }
            // An ask whose earlier intake already ended transient stays bound to this failed record, so the person hears of it.
            if (unbindAsk && moved!.Ask is { } ask && asks is not null
                && !await IntakeTransients.SecondAsync(sessions, moved, inside).ConfigureAwait(false))
            {
                await asks.UnbindIntakeAsync(ask, moved.Id, now, inside).ConfigureAwait(false);
            }

            // The models of what it staged are named from the turn lines handed as it ends, whatever releases the rows later.
            if (turns is not null && target is not (SessionState.Starting or SessionState.Working or SessionState.AwaitingPerson))
            {
                await sessions.Memory.StampAsync(id, turns, inside).ConfigureAwait(false);
            }

            // Its memory is released with the record, and carries what the sessions it continues staged; a tree's waits for its post.
            if (target == SessionState.Completed && (session.Tree is null || Trees.Normalize(session.Tree) == mainTree))
            {
                await sessions.Memory.ReleaseAsync([id], predecessors: true, turns, inside).ConfigureAwait(false);
            }

            return new SessionAdvanceOutcome(
                SessionAdvanceRefusal.None,
                $"Session `{moved!.Id}` is now {Spell(moved.State)}.",
                moved);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Whether a record writes in its place and so takes its lease: a pass and an intake write nothing there.</summary>
    private static bool Writes(Session session) =>
        session is { Ask: null, Opinion: null, Serves: null, Decides: null } && !SessionLedger.IsHelp(session);

    /// <summary>
    /// Why a move to working may not carry the record's supervisor's words, or null: those waiting must be
    /// exactly <paramref name="expect"/>, and naming any needs a park.
    /// </summary>
    private static SessionAdvanceOutcome? Admits(Session session, IReadOnlyCollection<string>? expect)
    {
        var named = expect ?? [];
        var waiting = session.Said.Where(word => word.Supervisors).Select(word => word.Id).ToHashSet(StringComparer.Ordinal);
        if (!waiting.SetEquals(named))
        {
            return new SessionAdvanceOutcome(
                SessionAdvanceRefusal.WordsChanged,
                $"Session `{session.Id}`'s waiting supervisor's words are not the ones this move admits, so it was not moved.",
                Session: null);
        }

        return named.Count > 0 && session.State != SessionState.AwaitingPerson
            ? new SessionAdvanceOutcome(
                SessionAdvanceRefusal.InvalidMove,
                $"Session `{session.Id}` is {Spell(session.State)}, not parked: the supervisor's answer goes on only from a park, so it was not moved.",
                Session: null)
            : null;
    }

    /// <summary>
    /// The one move out of an ended state: an ended record of this machine's goes on to working with the
    /// person's words waiting, because one record stands for one harness conversation and the conversation goes
    /// on. Called inside <see cref="AdvanceAsync"/>'s step, the record already read.
    /// </summary>
    /// <remarks>
    /// <para><b>Nothing else leaves an ended state.</b> A teammate's record, a stand-down, an intake, and a record with no
    /// words waiting each stay as they ended; and the tree it worked in must be free, since one session per working tree
    /// holds for going on as for a start.</para>
    ///
    /// <para><b>Its history stays.</b> The note keeps what ended it and says when it went on, a note passed with the
    /// move after that; the evidence stays, so the review counts from the record's own base. What ended it is forgiven
    /// (a stop the sweep made, a limit's failure), so it says nothing of the run that follows. The words stay until that
    /// run takes them.</para>
    /// </remarks>
    private async Task<SessionAdvanceOutcome> GoOnAsync(
        Session session, string? note, string? noteParts, string? evidence, string? transcript, DateTimeOffset now, string? root,
        CancellationToken inside)
    {
        var id = session.Id;
        var refused = session switch
        {
            { Origin: { } origin } => $"Session `{id}` ran on `{origin}`; a finished record of another machine's does not move here.",
            { State: SessionState.StoodDown } => $"Session `{id}` stood down — a stand-down has nothing to go on with, so it does not move.",
            { Ask: { } ask } => $"Session `{id}` is an intake — it is answered through its ask `#{ask}`, so it does not move.",
            // A pass is one turn, read afresh each time, so a reviewer's record never goes on.
            { Opinion: { } opinion } => $"Session `{id}` read the work for second opinion `{opinion}` — a pass is never resumed, so it does not move.",
            // A helper takes one turn and is never resumed either.
            { Serves: { } serves } => $"Session `{id}` is a helper for session `{serves}` — a helper is never resumed, so it does not move.",
            { Said.Count: 0 } =>
                $"Session `{id}` is {Spell(session.State)} — a finished session goes on only with the person's words, and none wait for it.",
            _ => null,
        };
        if (refused is not null)
        {
            return new SessionAdvanceOutcome(SessionAdvanceRefusal.Terminal, refused, Session: null);
        }

        // Ask Daoris's goes on beside any other; a repository's in the place itself takes the writer's lease, as an open does.
        if (!SessionLedger.IsHelp(session) && SessionHolders.InPlace(session.Tree, root)
            && await holders.PlaceHolderAsync(session.Repository, root, inside, except: session.Id).ConfigureAwait(false) is { } leased)
        {
            return new SessionAdvanceOutcome(SessionAdvanceRefusal.Busy, SessionPlaceLease.Busy(session.Repository, leased), Session: null);
        }

        var holder = SessionLedger.IsHelp(session) || SessionHolders.InPlace(session.Tree, root)
            ? null
            : await holders.HolderAsync(session.Repository, session.Tree, inside, except: session.Id).ConfigureAwait(false);
        if (holder is not null)
        {
            return new SessionAdvanceOutcome(SessionAdvanceRefusal.Busy, SessionHolders.Busy(session.Repository, holder), Session: null);
        }

        var (went, parts) = WentOnNote(session, note, noteParts, now);
        var moved = await sessions
            .SetStateAsync(id, SessionState.Working, went, evidence, transcript, now, inside, forgive: true, noteParts: parts)
            .ConfigureAwait(false);
        await holders.MarkPlaceAsync(id, null, inside).ConfigureAwait(false);
        moved = moved! with { HoldsPlace = null };

        var with = WentOnWith(session) switch
        {
            (Persons: false, Findings: true, _, _, _) => "another agent's findings",
            (Persons: true, Findings: true, _, _, _) => "the person's words and another agent's findings",
            (Persons: false, Findings: false, Helper: true, _, _) => "a helper's answer",
            (Persons: true, Findings: false, Helper: true, _, _) => "the person's words and a helper's answer",
            (Persons: false, Findings: false, Helper: false, Daoris: true, _) => "Daoris's words",
            (Persons: false, Findings: false, Helper: false, Daoris: false, Supervisor: true) => "the supervisor's answer",
            _ => "the person's words",
        };
        return new SessionAdvanceOutcome(
            SessionAdvanceRefusal.None,
            $"Session `{id}` is working again: it goes on from {Spell(session.State)} with {with}.",
            moved);
    }

    /// <summary>
    /// The second move out of an ended state: a driven session of this machine's that ended waiting on
    /// its own helper parks for the person when that helper ended with no answer, as its turn's end would have parked it, its note the
    /// driver's (the reason, then the park's own line). Called inside <see cref="AdvanceAsync"/>'s step, the record already read.
    /// </summary>
    /// <remarks>
    /// <para><b>Only that ending, and only to a park.</b> The record is still the one conversation, its quest still taken by it,
    /// and nothing ran since it ended; the person's answer resumes that conversation as any park's does. A record words wait on goes on
    /// with them instead; a teammate's, an intake, a pass or a helper, or one with no quest, does not move; and its tree must be free,
    /// since a park holds it.</para>
    ///
    /// <para><b>Its history stays</b> as a park's does: the note is the move's, and the evidence and the transcript are kept.</para>
    /// </remarks>
    private async Task<SessionAdvanceOutcome> BackToPersonAsync(
        Session session, string? note, string? noteParts, DateTimeOffset now, string? root, CancellationToken inside)
    {
        var id = session.Id;
        var refused = session switch
        {
            { Origin: { } origin } => $"Session `{id}` ran on `{origin}`; a finished record of another machine's does not move here.",
            { Ask: not null } or { Opinion: not null } or { Serves: not null } or { Quest: null } =>
                $"Session `{id}` holds no quest of its own to wait on the person for, so it does not move.",
            { Said.Count: > 0 } => $"Session `{id}` has words waiting: it goes on with them, and does not park.",
            _ => null,
        };
        if (refused is not null) return new SessionAdvanceOutcome(SessionAdvanceRefusal.Terminal, refused, Session: null);

        // A park holds its quest: one closed or taken elsewhere since it ended has nothing to wait on the person for.
        if (await quests.FindAsync(session.Quest!, inside).ConfigureAwait(false) is not { Status: QuestStatus.Taken })
        {
            return new SessionAdvanceOutcome(
                SessionAdvanceRefusal.Terminal, $"Session `{id}`'s quest `#{session.Quest}` is no longer taken, so it has nothing to park for.", Session: null);
        }

        // A park in the place itself takes no lease; one in a copy keeps that copy's tree lock.
        if (!SessionHolders.InPlace(session.Tree, root)
            && await holders.HolderAsync(session.Repository, session.Tree, inside, except: session.Id).ConfigureAwait(false) is { } holder)
        {
            return new SessionAdvanceOutcome(SessionAdvanceRefusal.Busy, SessionHolders.Busy(session.Repository, holder), Session: null);
        }

        // Nothing waits on it (refused above), and this move never clears a word kept meanwhile, as a park's own end would.
        var moved = await sessions
            .SetStateAsync(id, SessionState.AwaitingPerson, note, evidence: null, transcript: null, now, inside, clearSaid: false, noteParts: noteParts)
            .ConfigureAwait(false);
        return new SessionAdvanceOutcome(
            SessionAdvanceRefusal.None,
            $"Session `{id}` waits on the person again: the helper it waited on ended with no answer.",
            moved);
    }

    /// <summary>
    /// Whose words an ended record goes on with, read from each waiting word's <see cref="SaidWord.By"/>:
    /// the person's, another agent's findings, or both. A record with none reads as the person's.
    /// </summary>
    /// <remarks>
    /// A helper's answer (<see cref="SaidWord.Helper"/>) is its own third, never findings: alone, or beside the
    /// person's words. Where findings and a helper's answer wait together the line says the findings. Daoris's own
    /// words (<see cref="SaidWord.From"/>) alone are a fourth, never "your words"; beside another's, the other's line stands.
    /// </remarks>
    private static (bool Persons, bool Findings, bool Helper, bool Daoris, bool Supervisor) WentOnWith(Session session) =>
        (session.Said.Count == 0 || session.Said.Any(word => word.Persons),
         session.Said.Any(word => word.By is not null),
         session.Said.Any(word => word.Helper is not null),
         session.Said.Any(word => word.From == SaidFrom.Daoris),
         session.Said.Any(word => word.Supervisors));

    /// <summary>
    /// The note an ended record goes on with: what ended it, when it went on, and the note the move passed,
    /// each with its parts: the record's own, or its note carried whole; the ledger's
    /// line with its moment; the move's own, or its note carried whole.
    /// </summary>
    /// <remarks>
    /// <b>Whose words, from <c>by</c></b>: the person's alone keep <see cref="LedgerNoteCodes.WentOn"/>.
    /// Another agent's findings are never "your words", since the person said nothing; with the person's beside them the line
    /// says both, as the driver's opening line does. Each is a coded line with its moment (<see cref="LedgerNoteCodes.WentOnFindings"/>,
    /// <see cref="LedgerNoteCodes.WentOnBoth"/>) since the page words them; a note written before keeps its English.
    /// </remarks>
    internal static (string Note, string Parts) WentOnNote(Session session, string? note, string? noteParts, DateTimeOffset now)
    {
        var at = $"{now.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";
        var (line, code) = WentOnWith(session) switch
        {
            (Persons: false, Findings: true, _, _, _) => ($"Went on with another agent's findings at {at}.", LedgerNoteCodes.WentOnFindings),
            (Persons: true, Findings: true, _, _, _) => ($"Went on with your words and another agent's findings at {at}.", LedgerNoteCodes.WentOnBoth),
            (Persons: false, Findings: false, Helper: true, _, _) => ($"Went on with a helper's answer at {at}.", LedgerNoteCodes.WentOnHelper),
            (Persons: true, Findings: false, Helper: true, _, _) => ($"Went on with your words and a helper's answer at {at}.", LedgerNoteCodes.WentOnHelperBoth),
            (Persons: false, Findings: false, Helper: false, Daoris: true, _) => ($"Went on with Daoris's words at {at}.", LedgerNoteCodes.WentOnDaoris),
            // The supervisor's answer alone is never "your words".
            (Persons: false, Findings: false, Helper: false, Daoris: false, Supervisor: true) =>
                ($"Went on with the supervisor's answer at {at}.", LedgerNoteCodes.WentOnSupervisor),
            _ => ($"Went on with your words at {at}.", LedgerNoteCodes.WentOn),
        };
        var went = string.Join("\n\n", new[] { session.Note, line, note }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var parts = NoteParts.After(
            string.IsNullOrWhiteSpace(session.Note) ? null : session.NoteParts, session.Note,
            [NoteLine.Coded(code, line, ("at", NoteParts.Moment(now)))],
            string.IsNullOrWhiteSpace(note) ? null : noteParts, note);
        return (went, parts);
    }
}
