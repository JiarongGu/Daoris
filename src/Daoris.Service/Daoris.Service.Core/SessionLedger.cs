namespace Daoris.Knowledge;

/// <summary>Why a session was not opened — or <see cref="None"/> when it was.</summary>
public enum SessionOpenRefusal
{
    None,

    /// <summary>No quest under that id.</summary>
    QuestNotFound,

    /// <summary>The quest is not open. Taken is the mutex between the driver and outside work (D46).</summary>
    QuestNotOpen,

    /// <summary>The repository already has an active session — the working tree is the unit of exclusion.</summary>
    RepositoryBusy,
}

/// <param name="Refusal"><see cref="SessionOpenRefusal.None"/> when a session was queued.</param>
/// <param name="Message">The full answer, phrased once here so no two doors can drift apart on it.</param>
/// <param name="Session">The queued session, when there is one.</param>
public sealed record SessionOpenOutcome(SessionOpenRefusal Refusal, string Message, Session? Session);

/// <summary>Why a session did not move — or <see cref="None"/> when it did.</summary>
public enum SessionAdvanceRefusal
{
    None,

    /// <summary>Not a state this ledger knows.</summary>
    UnknownState,

    /// <summary>No session under that id.</summary>
    NotFound,

    /// <summary>The session is finished, and a finished session is a record. Records do not move.</summary>
    Terminal,

    /// <summary>A move the lifecycle does not allow.</summary>
    InvalidMove,
}

/// <param name="Refusal"><see cref="SessionAdvanceRefusal.None"/> when the state moved.</param>
/// <param name="Message">The full answer, phrased once here.</param>
/// <param name="Session">The session as it now stands, when it moved.</param>
public sealed record SessionAdvanceOutcome(SessionAdvanceRefusal Refusal, string Message, Session? Session);

/// <summary>
/// The judgement half of the session system: when a session may open, and what may move where. The
/// store holds state; this decides — one implementation for every door, for the same reason
/// <see cref="QuestExchange"/> exists.
/// </summary>
/// <remarks>
/// <para><b>This ledger never writes quest state.</b> The spawned session claims its own quest through
/// its own connector, as the repository's own agent (D46) — which is what keeps driven and outside work
/// indistinguishable at the quest layer, and what keeps outside development first-class. The ledger
/// only reads the quest, to refuse starting work someone else already has.</para>
///
/// <para><b>One active session per repository.</b> Two agents in one working tree corrupt each other's
/// git state, so the tree is the unit of exclusion. A parked session still holds its repository: the
/// person clearing it is the flow control, not an inconvenience to route around.</para>
/// </remarks>
public sealed class SessionLedger(QuestStore quests, SessionStore sessions)
{
    /// <summary>
    /// Queue a session for an open quest. Refuses an unknown or non-open quest, and a repository that
    /// already has an active session.
    /// </summary>
    public async Task<SessionOpenOutcome> OpenAsync(
        string questId, string adapter, DateTimeOffset now, CancellationToken ct = default)
    {
        var quest = await quests.FindAsync(questId.TrimStart('#'), ct).ConfigureAwait(false);
        if (quest is null)
        {
            return new(
                SessionOpenRefusal.QuestNotFound,
                $"No quest `#{questId.TrimStart('#')}`. Ids come from `quest_list`.",
                Session: null);
        }

        if (quest.Status != QuestStatus.Open)
        {
            // Taken means someone — a session, a person, another machine — already has it, and Done or
            // Declined means the work is over. Either way there is nothing here for a new session to do.
            return new(
                SessionOpenRefusal.QuestNotOpen,
                $"Quest `#{quest.Id}` is {quest.Status} — a session starts only on an open quest.",
                Session: null);
        }

        var active = await sessions.ActiveForAsync(quest.To, ct).ConfigureAwait(false);
        if (active is not null)
        {
            return new(
                SessionOpenRefusal.RepositoryBusy,
                $"`{quest.To}` already has an active session — `{active.Id}` ({Spell(active.State)}, "
                + $"quest `#{active.Quest}`). One session per repository: the working tree is the unit "
                + "of exclusion.",
                Session: null);
        }

        var session = await sessions.CreateAsync(quest.Id, quest.To, adapter, now, ct).ConfigureAwait(false);

        return new(
            SessionOpenRefusal.None,
            $"Session `{session.Id}` queued for quest `#{quest.Id}` in `{quest.To}`, via {adapter}.",
            session);
    }

    /// <summary>
    /// Move a session, attaching what the move carries. States arrive as text because they travel over
    /// HTTP in the design's spelling — `awaiting-person`, `stood-down` — and a door should accept what
    /// it prints.
    /// </summary>
    public async Task<SessionAdvanceOutcome> AdvanceAsync(
        string id, string state, string? note, string? evidence, string? transcript,
        DateTimeOffset now, CancellationToken ct = default)
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

        var session = await sessions.FindAsync(id, ct).ConfigureAwait(false);
        if (session is null)
        {
            return new(SessionAdvanceRefusal.NotFound, $"No session `{id}`.", Session: null);
        }

        if (!session.Active)
        {
            return new(
                SessionAdvanceRefusal.Terminal,
                $"Session `{session.Id}` is {Spell(session.State)} — a finished session does not move.",
                Session: null);
        }

        if (!Allowed(session.State).Contains(target.Value))
        {
            return new(
                SessionAdvanceRefusal.InvalidMove,
                $"Session `{session.Id}` cannot move {Spell(session.State)} → {Spell(target.Value)}. "
                + $"From {Spell(session.State)}: {string.Join(", ", Allowed(session.State).Select(Spell))}.",
                Session: null);
        }

        var moved = await sessions.SetStateAsync(id, target.Value, note, evidence, transcript, now, ct)
            .ConfigureAwait(false);

        return new(
            SessionAdvanceRefusal.None,
            $"Session `{moved!.Id}` is now {Spell(moved.State)}.",
            moved);
    }

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

    private const string AdvanceTargets =
        "starting, working, awaiting-person, completed, declined, stood-down, failed, stopped";

    /// <summary>The design's spelling, used in every message and accepted back by <see cref="Parse"/>.</summary>
    private static string Spell(SessionState state) => state switch
    {
        SessionState.AwaitingPerson => "awaiting-person",
        SessionState.StoodDown => "stood-down",
        _ => state.ToString().ToLowerInvariant(),
    };

    private static SessionState? Parse(string state) =>
        Enum.TryParse<SessionState>(state.Replace("-", ""), ignoreCase: true, out var parsed) ? parsed : null;
}
