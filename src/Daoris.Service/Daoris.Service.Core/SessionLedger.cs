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

    /// <summary>No ask under that id — an intake answers an ask, so there has to be one (D65 §1b).</summary>
    AskNotFound,

    /// <summary>
    /// The ask is already answered — published, closed, or served by an intake before. One intake per
    /// ask: a second would be the loop retrying a harness forever on a question it could not settle.
    /// </summary>
    AskAnswered,
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
public sealed class SessionLedger(
    QuestStore quests, SessionStore sessions, KnowledgeService? registry = null, AskStore? asks = null)
{
    /// <summary>
    /// Open an INTAKE for an ask (D65 §1b) — a conversation Daoris opens in a room it owns, which
    /// decides from the circle's declarations, publishes the quests itself, and asks the person where
    /// the declarations do not settle it.
    /// </summary>
    /// <remarks>
    /// <para><b>Its own open, not a chat's.</b> A chat names a registered repository and takes its
    /// circle from the row; the room is no repository, so the circle comes from the ASK — derived,
    /// never passed beside it, for the same reason a driven session's comes from its quest.</para>
    ///
    /// <para><b>Recorded as a chat</b>, serving no quest, in "repository" <c>ask #id</c> — the sender its
    /// quests are published by. Every build already reads a chat as a session nothing plans from.</para>
    ///
    /// <para><b>The room's lock is the process, not the record</b>: one intake RUNNING per room, and a
    /// parked one — which has asked the person and ended — leaves it to the next ask
    /// (<see cref="SessionStore.RunningInTreeAsync"/>).</para>
    /// </remarks>
    public async Task<SessionOpenOutcome> OpenIntakeAsync(
        string askId, string adapter, string room, DateTimeOffset now,
        string? harnessVersion = null, string? profile = null, CancellationToken ct = default)
    {
        var ask = asks is null ? null : await asks.FindAsync(askId, ct).ConfigureAwait(false);
        if (ask is null)
        {
            return new(SessionOpenRefusal.AskNotFound, $"No ask `#{askId.TrimStart('#')}`.", Session: null);
        }

        var served = ask.Intake is { } earlier
            ? $"intake session `{earlier}` already served it"
            : ask.State switch
            {
                AskState.Published => $"it already became {string.Join(", ", ask.Quests.Select(q => $"`#{q}`"))}",
                AskState.Closed => $"it is closed ({ask.Note})",
                _ => null,
            };
        if (served is not null)
        {
            return new(
                SessionOpenRefusal.AskAnswered,
                $"Ask `#{ask.Id}` is not an intake's to answer — {served}. Publish it with a receiver you "
                + "name, or close it.",
                Session: null);
        }

        var holding = Trees.Normalize(room)
            ?? throw new ArgumentException("an intake runs in a room — name it", nameof(room));

        // The check and the write as one step, across hosts (REV3): see SessionStore.ExclusiveAsync.
        return await sessions.ExclusiveAsync(async inside =>
        {
            var running = await sessions.RunningInTreeAsync(holding, inside).ConfigureAwait(false);
            if (running is not null)
            {
                return new SessionOpenOutcome(
                    SessionOpenRefusal.RepositoryBusy,
                    $"The intake room for `{ask.Workspace}` already has a running session — `{running.Id}` "
                    + $"({Spell(running.State)}{(running.Ask is { } other ? $", ask `#{other}`" : "")}). One intake runs "
                    + "per workspace at a time; the next ask is taken when it ends.",
                    Session: null);
            }

            // No base commit, deliberately: the room is no repository, and git asked about it would walk
            // UP and answer for whatever checkout the home sits in.
            var session = await sessions
                .CreateAsync(
                    null, AskDesk.SenderOf(ask.Id), adapter, now, ask.Workspace, SessionKind.Chat,
                    harnessVersion, profile, holding, baseCommit: null, inside, ask: ask.Id)
                .ConfigureAwait(false);
            await asks!.RecordIntakeAsync(ask.Id, session.Id, now, inside).ConfigureAwait(false);

            return new SessionOpenOutcome(
                SessionOpenRefusal.None,
                $"Intake `{session.Id}` opened for ask `#{ask.Id}` in `{ask.Workspace}`, via {adapter}.",
                session);
        }, ct).ConfigureAwait(false);
    }

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
        string? baseCommit = null, CancellationToken ct = default)
    {
        var known = registry is null
            ? null
            : (await registry.RegistryAsync(ct: ct).ConfigureAwait(false))
                .Named(repository);

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

        return await sessions.ExclusiveAsync(async inside =>
        {
            var active = await sessions.ActiveForAsync(known.Repository, holding, inside).ConfigureAwait(false);
            if (active is not null)
            {
                return new SessionOpenOutcome(
                    SessionOpenRefusal.RepositoryBusy,
                    Busy(known.Repository, active) + " That is true of a conversation exactly as it is of "
                    + "driven work.",
                    Session: null);
            }

            var session = await sessions
                .CreateAsync(
                    null, known.Repository, adapter, now, known.InWorkspace, SessionKind.Chat,
                    harnessVersion, profile, holding, baseCommit, inside)
                .ConfigureAwait(false);

            return new SessionOpenOutcome(
                SessionOpenRefusal.None,
                $"Chat `{session.Id}` opened in `{known.Repository}`, via {adapter}.",
                session);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The session's own connector took its quest (STANDDOWN2): recorded on the session, so its end
    /// can tell "it holds the quest and stopped" from "somebody else had it". Only the session's own
    /// quest, only while it runs, and only this machine's record — anything else changes nothing.
    /// </summary>
    public async Task<bool> MarkTookAsync(string sessionId, string questId, CancellationToken ct = default)
    {
        var session = await sessions.FindAsync(sessionId, ct).ConfigureAwait(false);
        if (session is not { Active: true, Origin: null, Quest: { } quest }
            || !string.Equals(quest, questId.TrimStart('#'), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return await sessions.MarkTookAsync(sessionId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The person answers a driven session that parked to ask them (STANDDOWN2): its record ends
    /// `completed` with their words kept, and the quest it holds is carried on in the same tree at
    /// the driver's next tick, handed what they said. An intake is answered through its ask instead.
    /// </summary>
    /// <returns>The session as it now stands, or a refusal in words a person can act on.</returns>
    public async Task<SessionAdvanceOutcome> AnswerAsync(
        string id, string? answer, DateTimeOffset now, CancellationToken ct = default)
    {
        var session = await sessions.FindAsync(id, ct).ConfigureAwait(false);
        if (session is null || session.Origin is not null)
        {
            return new(SessionAdvanceRefusal.NotFound, $"No session `{id}` of this machine's.", Session: null);
        }

        if (session.State != SessionState.AwaitingPerson || session.Quest is null)
        {
            return new(
                SessionAdvanceRefusal.InvalidMove,
                session.Ask is not null
                    ? $"Session `{id}` is an intake — answer its ask `#{session.Ask}` instead: publish it or close it."
                    : $"Session `{id}` is {Session.Spell(session.State)}, not waiting on you — there is nothing to answer.",
                Session: null);
        }

        var said = string.IsNullOrWhiteSpace(answer) ? "carry on." : answer.Trim();
        await sessions.SetAnswerAsync(id, said, ct).ConfigureAwait(false);
        // The note keeps what it asked beside what it was told: the session that carries the quest on is
        // handed this record, and an answer without its question is half a conversation.
        var moved = await AdvanceAsync(
            id, "completed", $"{session.Note ?? "It stopped to ask the person; its question is in its transcript."}\n\nAnswered: {said}",
            evidence: null, transcript: null, now, ct).ConfigureAwait(false);
        return moved.Refusal == SessionAdvanceRefusal.None
            ? moved with { Message = $"Answered session `{id}`: `#{session.Quest}` is carried on in its tree at the driver's next tick." }
            : moved;
    }

    /// <summary>
    /// Queue a session for an open quest. Refuses an unknown or non-open quest, and a repository that
    /// already has an active session.
    /// </summary>
    public async Task<SessionOpenOutcome> OpenAsync(
        string questId, string adapter, DateTimeOffset now,
        string? harnessVersion = null, string? profile = null, string? tree = null,
        string? baseCommit = null, CancellationToken ct = default)
    {
        var quest = await quests.FindAsync(questId.TrimStart('#'), ct).ConfigureAwait(false);
        if (quest is null)
        {
            return new(
                SessionOpenRefusal.QuestNotFound,
                $"No quest `#{questId.TrimStart('#')}`. Ids come from `quest_list`.",
                Session: null);
        }

        // A resume (D79): a taken quest whose taker waited on a question, now answered, is its taker's
        // to carry on — the one taken quest a session may start on.
        var question = quest is { Status: QuestStatus.Taken, Awaits: { } awaits }
            ? await quests.FindAsync(awaits, ct).ConfigureAwait(false)
            : null;
        var resumes = question is { Status: QuestStatus.Done or QuestStatus.Declined };

        // A carry-on (D80): a taken quest that is not waiting, whose last session on THIS machine
        // failed — timed out, refused, crashed — before closing it. The take is this machine's and its
        // tree holds the work. The driver's strikes bound how often; a stand-down or a teammate's
        // record never counts, because either means the take is somebody else's.
        // And the person's answer to one that parked to ask them (STANDDOWN2): its record ended
        // `completed` with their words kept, and the quest it held is carried on the same way.
        var carriesOn = quest is { Status: QuestStatus.Taken } && question is null
            && await sessions.LastOwnForQuestAsync(quest.Id, ct).ConfigureAwait(false)
                is { State: SessionState.Failed } or { State: SessionState.Completed, Answer: not null };

        if (quest.Status != QuestStatus.Open && !resumes && !carriesOn)
        {
            // Taken means someone — a session, a person, another machine — already has it, and Done or
            // Declined means the work is over. Either way there is nothing here for a new session to do.
            return new(
                SessionOpenRefusal.QuestNotOpen,
                question is not null
                    ? $"Quest `#{quest.Id}` waits on `#{question.Id}`, which is still {question.Status} — it resumes once that is answered."
                    : $"Quest `#{quest.Id}` is {quest.Status} — a session starts only on an open quest.",
                Session: null);
        }

        // The same resolution as a chat's, through the same registry, for the same reason: two doors
        // onto one tree must agree about which tree that is.
        var holding = Trees.Normalize(tree) ?? Trees.Normalize(await RootOfAsync(quest.To, ct).ConfigureAwait(false));

        return await sessions.ExclusiveAsync(async inside =>
        {
            var active = await sessions.ActiveForAsync(quest.To, holding, inside).ConfigureAwait(false);
            if (active is not null)
            {
                return new SessionOpenOutcome(SessionOpenRefusal.RepositoryBusy, Busy(quest.To, active), Session: null);
            }

            // The record's circle is the quest's circle — derived, never passed beside it, so a record can
            // never be filed under a workspace its quest does not belong to (D48 §4).
            var session = await sessions
                .CreateAsync(
                    quest.Id, quest.To, adapter, now, quest.Workspace, SessionKind.Driven,
                    harnessVersion, profile, holding, baseCommit, inside)
                .ConfigureAwait(false);

            return new SessionOpenOutcome(
                SessionOpenRefusal.None,
                $"Session `{session.Id}` queued for quest `#{quest.Id}` in `{quest.To}`, via {adapter}.",
                session);
        }, ct).ConfigureAwait(false);
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

        // Read, judged and written as one step (REV3): the driver's conclusion and a person's stop,
        // arriving from two hosts, could both read the session active, and the second moved a finished one.
        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionAdvanceOutcome(SessionAdvanceRefusal.NotFound, $"No session `{id}`.", Session: null);
            }

            if (!session.Active)
            {
                return new SessionAdvanceOutcome(
                    SessionAdvanceRefusal.Terminal,
                    $"Session `{session.Id}` is {Spell(session.State)} — a finished session does not move.",
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

            var moved = await sessions.SetStateAsync(id, target.Value, note, evidence, transcript, now, inside)
                .ConfigureAwait(false);

            return new SessionAdvanceOutcome(
                SessionAdvanceRefusal.None,
                $"Session `{moved!.Id}` is now {Spell(moved.State)}.",
                moved);
        }, ct).ConfigureAwait(false);
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
            .Named(repository)
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
