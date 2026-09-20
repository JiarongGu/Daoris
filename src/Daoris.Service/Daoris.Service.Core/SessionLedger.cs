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

    /// <summary>
    /// No such repository on this machine's registry. A chat runs IN a repository, so there has to be
    /// one — and a chat is the one way in that names a repository directly rather than inheriting it
    /// from a quest, which is why only this path can fail that way.
    /// </summary>
    RepositoryUnknown,
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
/// <para><b>One active session per working TREE</b> (D51). Two agents in one working tree corrupt each
/// other's git state — that is the reason, and it does not move. What moved is the other half: a
/// repository has as many trees as it has, so the lock keys on the tree rather than on the name of the
/// repository that owns it. A parked session still holds its tree: the person clearing it is the flow
/// control, not an inconvenience to route around.</para>
///
/// <para><b>An unstated tree resolves through the registry</b>, at both doors — which is what makes two
/// sessions in one tree collide by construction rather than by string comparison, and what keeps
/// behaviour identical for every caller that has not learned about trees yet.</para>
/// </remarks>
public sealed class SessionLedger(QuestStore quests, SessionStore sessions, KnowledgeService? registry = null)
{
    /// <summary>
    /// Open a chat in a repository (D49 §3) — a person-initiated session serving no quest yet.
    /// </summary>
    /// <remarks>
    /// <para><b>The same lock, for the same reason.</b> One active session per repository holds for
    /// chats exactly as for driven work: two agents in one working tree corrupt each other's git
    /// state regardless of who is typing. The refusal names the session that holds it.</para>
    ///
    /// <para><b>No quest, and no quest required.</b> The point of a chat is starting work that is not
    /// yet shaped as an ask; a chat may take a quest mid-conversation through its own connector, or
    /// end by publishing several. The quest system is where the work lands, not the toll to begin.</para>
    ///
    /// <para><b>The circle comes from the repository</b>, because there is no quest to take it from —
    /// the registry row is the machine's own wiring (D48 §2), and it is the only honest source.</para>
    /// </remarks>
    public async Task<SessionOpenOutcome> OpenChatAsync(
        string repository, string adapter, DateTimeOffset now,
        string? harnessVersion = null, string? profile = null, string? tree = null,
        CancellationToken ct = default)
    {
        var known = registry is null
            ? null
            : (await registry.RegistryAsync(ct: ct).ConfigureAwait(false))
                .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));

        if (known is null)
        {
            return new(
                SessionOpenRefusal.RepositoryUnknown,
                $"`{repository}` is not registered on this machine, so there is no working tree to talk "
                + "in. `daoris connect` from inside it, or add it from Projects.",
                Session: null);
        }

        // Unstated means the registered root — this repository's main tree (D51). Resolved here rather
        // than taken on trust, so a caller that names the root and one that says nothing land on the
        // same key instead of holding one tree twice.
        var holding = Trees.Normalize(tree) ?? Trees.Normalize(known.Root);

        var active = await sessions.ActiveForAsync(known.Repository, holding, ct).ConfigureAwait(false);
        if (active is not null)
        {
            return new(
                SessionOpenRefusal.RepositoryBusy,
                Busy(known.Repository, active) + " That is true of a conversation exactly as it is of "
                + "driven work.",
                Session: null);
        }

        var session = await sessions
            .CreateAsync(
                null, known.Repository, adapter, now, known.InWorkspace, SessionKind.Chat,
                harnessVersion, profile, holding, ct)
            .ConfigureAwait(false);

        return new(
            SessionOpenRefusal.None,
            $"Chat `{session.Id}` opened in `{known.Repository}`, via {adapter}.",
            session);
    }

    /// <summary>
    /// Queue a session for an open quest. Refuses an unknown or non-open quest, and a repository that
    /// already has an active session.
    /// </summary>
    public async Task<SessionOpenOutcome> OpenAsync(
        string questId, string adapter, DateTimeOffset now,
        string? harnessVersion = null, string? profile = null, string? tree = null,
        CancellationToken ct = default)
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

        // The same resolution as a chat's, through the same registry, for the same reason: two doors
        // onto one tree must agree about which tree that is.
        var holding = Trees.Normalize(tree) ?? Trees.Normalize(await RootOfAsync(quest.To, ct).ConfigureAwait(false));

        var active = await sessions.ActiveForAsync(quest.To, holding, ct).ConfigureAwait(false);
        if (active is not null)
        {
            return new(SessionOpenRefusal.RepositoryBusy, Busy(quest.To, active), Session: null);
        }

        // The record's circle is the quest's circle — derived, never passed beside it, so a record can
        // never be filed under a workspace its quest does not belong to (D48 §4).
        var session = await sessions
            .CreateAsync(
                quest.Id, quest.To, adapter, now, quest.Workspace, SessionKind.Driven,
                harnessVersion, profile, holding, ct)
            .ConfigureAwait(false);

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
    /// The busy sentence, phrased once for both doors (D51).
    /// </summary>
    /// <remarks>
    /// <b>It names the SESSION holding the tree, never the tree's PATH.</b> A path is machine-local
    /// material (D47 §4) and a refusal is the one surface with no strip on it: it is composed here and
    /// rendered verbatim wherever it lands. The session id is the actionable half anyway — it is what
    /// a person stops, and what the record they should read is filed under.
    /// </remarks>
    private static string Busy(string repository, Session active) =>
        $"`{repository}`'s working tree already has an active session — `{active.Id}` "
        + $"({Spell(active.State)}{(active.Quest is null ? ", a chat" : $", quest `#{active.Quest}`")}). "
        + "One session per working tree: two agents in one tree corrupt each other's git state.";

    /// <summary>
    /// Where this repository's main tree is, as this machine's registry knows it — null when nothing
    /// is registered, which the lock reads conservatively rather than as "any tree is free".
    /// </summary>
    private async Task<string?> RootOfAsync(string repository, CancellationToken ct)
    {
        if (registry is null) return null;

        return (await registry.RegistryAsync(ct: ct).ConfigureAwait(false))
            .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase))
            ?.Root;
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

    /// <summary>The public spelling, shared with every door through <see cref="Session.Spell"/>.</summary>
    private static string Spell(SessionState state) => Session.Spell(state);

    /// <summary>
    /// Shared with every other door through <see cref="Session.TryParse"/> — the strict one. A bare
    /// Enum.TryParse also accepts numeric strings, turning "99" into an undefined state that escapes
    /// the unknown-state branch.
    /// </summary>
    private static SessionState? Parse(string state) =>
        Session.TryParse(state, out var parsed) ? parsed : null;
}
