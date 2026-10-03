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

/// <summary>Why a person's word was not kept on an ask (DRIFT1a) — or <see cref="None"/> when it was.</summary>
public enum AskWordRefusal
{
    None,

    /// <summary>No words: nothing was said.</summary>
    Empty,

    /// <summary>No session under that id of this machine's.</summary>
    NotFound,

    /// <summary>
    /// The session is on no ask held here — a quest a repository asked, or a conversation on none — so
    /// there is no ask to keep the words on. Not an error: its own record holds what was said.
    /// </summary>
    NoAsk,
}

/// <param name="Refusal"><see cref="AskWordRefusal.None"/> when the word was kept.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Ask">The ask it was kept on, when it was.</param>
/// <param name="Word">The word as it was kept, when it was.</param>
public sealed record AskWordOutcome(AskWordRefusal Refusal, string Message, string? Ask = null, AskWord? Word = null);

/// <summary>
/// Why a session record was not deleted — or <see cref="None"/> when it was, or would be (SESSUX1f, D126 §5.4). The
/// record's half only: whether its tree or a landing is still on the machine is the driver's to judge.
/// </summary>
public enum SessionDeleteRefusal
{
    None,

    /// <summary>No record under that id.</summary>
    NotFound,

    /// <summary>A teammate's record (SYNC4): it ran on their machine, and the record is theirs.</summary>
    NotOurs,

    /// <summary>It still runs or waits: stopped first, it ends.</summary>
    Live,

    /// <summary>It served a quest, and is that work's record: the strikes and the carry-ons are read from it (D58, D80).</summary>
    ServedQuest,

    /// <summary>An ask names it as its intake, or a quest was published by it (SESS1).</summary>
    Named,

    /// <summary>It went up to a workspace's remote, and a session record does not travel as a deletion.</summary>
    OnRemote,
}

/// <param name="Refusal"><see cref="SessionDeleteRefusal.None"/> when it was deleted, or would be.</param>
/// <param name="Message">The full answer, phrased once here.</param>
/// <param name="Session">The record judged, when there is one.</param>
public sealed record SessionDeleteOutcome(SessionDeleteRefusal Refusal, string Message, Session? Session)
{
    /// <summary>The quest it served, or the quest it published, where that refused it.</summary>
    public string? Quest { get; init; }

    /// <summary>The ask that names it as its intake, where that refused it.</summary>
    public string? Ask { get; init; }

    /// <summary>The machine a teammate's record ran on, where that refused it.</summary>
    public string? Origin { get; init; }

    /// <summary>The workspace whose remote holds it, where that refused it.</summary>
    public string? Workspace { get; init; }
}

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
public sealed partial class SessionLedger(
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
        var stored = asks is null ? null : await asks.FindAsync(askId, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return new(SessionOpenRefusal.AskNotFound, $"No ask `#{askId.TrimStart('#')}`.", Session: null);
        }

        // As it stands, the way the desk reads it (USE1c, D95): an ask whose every quest was deleted is a
        // proposal again, and the loop, which reads the desk, would otherwise ask for it on every tick.
        var ask = AskDesk.Standing(
            stored, await quests.FromAsync(AskDesk.SenderOf(stored.Id), ct: ct).ConfigureAwait(false));

        var served = ask.Intake is { } earlier
            ? $"intake session `{earlier}` already served it"
            : ask.State switch
            {
                AskState.Published or AskState.Done => $"it already became {string.Join(", ", ask.Quests.Select(q => $"`#{q}`"))}",
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
    /// The "repository" an Ask Daoris session is recorded in (HELP1a, D89). No repository can be
    /// called this — a colon is in no folder name — so it never collides with a registered one.
    /// </summary>
    /// <remarks>A twin (`.claude/knowledge/twins.md`): the driver's <c>HelpRoom.Repository</c> and the
    /// page's <c>HELP_REPOSITORY</c> spell it too, and each side's test holds the spelling.</remarks>
    public const string HelpRepository = "daoris:help";

    /// <summary>
    /// Open Ask Daoris's conversation (HELP1a, D89) — a chat about Daoris itself, in the room the
    /// driver keeps for it under its home.
    /// </summary>
    /// <remarks>
    /// <para><b>The intake's open, for a conversation.</b> It serves no quest and no ask, so nothing
    /// plans from it, and it belongs to no workspace: it is about the whole machine.</para>
    ///
    /// <para><b>One running per room</b>, which is one per machine: a second is refused naming the
    /// first, and the driver hands the page the running one rather than asking for another.</para>
    /// </remarks>
    public async Task<SessionOpenOutcome> OpenHelpAsync(
        string adapter, string room, DateTimeOffset now,
        string? harnessVersion = null, string? profile = null, CancellationToken ct = default)
    {
        var holding = Trees.Normalize(room)
            ?? throw new ArgumentException("Ask Daoris runs in a room — name it", nameof(room));

        return await sessions.ExclusiveAsync(async inside =>
        {
            var running = await sessions.RunningInTreeAsync(holding, inside).ConfigureAwait(false);
            if (running is not null)
            {
                return new SessionOpenOutcome(
                    SessionOpenRefusal.RepositoryBusy,
                    $"Ask Daoris already has a conversation running — `{running.Id}` ({Spell(running.State)}). "
                    + "One runs at a time; carry that one on, or finish it first.",
                    Session: null);
            }

            // No base commit, for the intake's reason: the room is no repository.
            var session = await sessions
                .CreateAsync(
                    null, HelpRepository, adapter, now, workspace: null, SessionKind.Chat,
                    harnessVersion, profile, holding, baseCommit: null, inside)
                .ConfigureAwait(false);

            return new SessionOpenOutcome(
                SessionOpenRefusal.None,
                $"Ask Daoris `{session.Id}` opened, via {adapter}.",
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
                + "in. `daoris connect` from inside it, or add it from Repositories.",
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
    /// The person answers a driven session that parked to ask them (STANDDOWN2): the record stays
    /// `awaiting-person` with their words kept and said on its note, and the driver goes on with it at
    /// its next look (ANSWER1b, D131) — the session's own conversation resumed with the answer where it
    /// can, else the park ended and its quest carried on in a new session in the same tree, handed what
    /// they said. A second answer before then replaces the first. An intake is answered through its ask.
    /// </summary>
    /// <remarks>
    /// <b>The answer keeps the park</b> because one record stands for one harness conversation (D131 §3):
    /// a record the answer ended could not reopen, since a finished record does not move.
    /// </remarks>
    /// <returns>The session as it now stands, or a refusal in words a person can act on.</returns>
    public async Task<SessionAdvanceOutcome> AnswerAsync(
        string id, string? answer, DateTimeOffset now, CancellationToken ct = default)
    {
        var said = string.IsNullOrWhiteSpace(answer) ? "carry on." : answer.Trim();

        // Read, judged and written as one step (REV3): the driver taking the park up and a second answer,
        // arriving from two hosts, could both read it parked, and the answer land on a record going on.
        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is null || session.Origin is not null)
            {
                return new SessionAdvanceOutcome(SessionAdvanceRefusal.NotFound, $"No session `{id}` of this machine's.", Session: null);
            }

            if (session.State != SessionState.AwaitingPerson || session.Quest is null)
            {
                return new SessionAdvanceOutcome(
                    SessionAdvanceRefusal.InvalidMove,
                    session.Ask is not null
                        ? $"Session `{id}` is an intake — answer its ask `#{session.Ask}` instead: publish it or close it."
                        : $"Session `{id}` is {Session.Spell(session.State)}, not waiting on you — there is nothing to answer.",
                    Session: null);
            }

            var answered = await sessions.AnswerAsync(id, said, AnsweredNote(session, said), now, inside).ConfigureAwait(false);
            return new SessionAdvanceOutcome(
                SessionAdvanceRefusal.None,
                $"Answered session `{id}`: it carries on with `#{session.Quest}` at the driver's next look.",
                answered);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The note an answer leaves (ANSWER1b): what the session asked, then what it was told, since the conversation that
    /// goes on and a session that carries the quest on are both read from this record, and an answer without its
    /// question is half a conversation. A second answer replaces the first one's line, so the note says what the record holds.
    /// </summary>
    private static string AnsweredNote(Session parked, string said)
    {
        var asked = parked.Note ?? "It stopped to ask the person; its question is in its transcript.";
        if (parked.Answer is { } earlier && asked.EndsWith(AnsweredLine(earlier), StringComparison.Ordinal))
        {
            asked = asked[..^AnsweredLine(earlier).Length];
        }

        return asked + AnsweredLine(said);
    }

    private static string AnsweredLine(string said) => $"\n\nAnswered: {said}";

    /// <summary>
    /// Keep what the person said to a session on the ask its work is for (DRIFT1a, D133 §1): verbatim, with
    /// when, the session it was said to and the quest that session works. Beside <see cref="AnswerAsync"/>,
    /// never inside it: the answer door calls both, and what an answer does to a parked session is not this
    /// method's to decide.
    /// </summary>
    /// <remarks>
    /// <para><b>The ask is derived, never passed.</b> An intake names its ask; a driven session's quest was
    /// asked by one when its sender is <c>ask #id</c>, a chain step included. A quest one repository asked
    /// of another, and a conversation on no quest, are on no ask, and the answer says so rather than filing
    /// the words under an ask they were not given on.</para>
    ///
    /// <para><b>In any state.</b> An answer is kept after the answer moved the record, and a message while
    /// the session runs; a record of another machine's is not this machine's person talking.</para>
    /// </remarks>
    /// <exception cref="ArgumentException">For <see cref="AskWordKind.Asked"/>: the ask's own sentence is its record's.</exception>
    public async Task<AskWordOutcome> KeepOnAskAsync(
        string sessionId, AskWordKind kind, string? words, DateTimeOffset now, CancellationToken ct = default)
    {
        if (kind == AskWordKind.Asked)
        {
            throw new ArgumentException("the ask's own sentence is kept when it is asked, never by a session", nameof(kind));
        }

        var text = words?.Trim() ?? "";
        if (text.Length == 0)
        {
            return new(AskWordRefusal.Empty, "There are no words to keep: the person said nothing.");
        }

        var session = await sessions.FindAsync(sessionId, ct).ConfigureAwait(false);
        if (session is null || session.Origin is not null)
        {
            return new(AskWordRefusal.NotFound, $"No session `{sessionId}` of this machine's.");
        }

        const string Own = "so nothing was kept on one; its own record holds what was said.";
        var quest = session.Quest is { } questId ? await quests.FindAsync(questId, ct).ConfigureAwait(false) : null;
        var askId = session.Ask ?? AskDesk.AskOf(quest?.From);
        if (askId is null)
        {
            return new(AskWordRefusal.NoAsk, (session.Quest, quest) switch
            {
                (null, _) => $"Session `{session.Id}` is a conversation on no ask, {Own}",
                (_, null) => $"Session `{session.Id}` works quest `#{session.Quest}`, which is not held here, {Own}",
                _ => $"Session `{session.Id}` works quest `#{quest.Id}`, which `{quest.From}` asked rather than an ask, {Own}",
            });
        }

        var word = new AskWord(kind, text, now, session.Id, session.Quest);
        if (asks is null || !await asks.RecordWordAsync(askId, word, now, ct).ConfigureAwait(false))
        {
            return new(AskWordRefusal.NoAsk, $"Session `{session.Id}` is on ask `#{askId}`, which is not held here, {Own}");
        }

        return new(
            AskWordRefusal.None,
            $"Kept on ask `#{askId}`, as said to session `{session.Id}`"
            + (session.Quest is { } on ? $" on quest `#{on}`." : "."),
            askId, word);
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
        // And the person's answer to one that parked to ask them (STANDDOWN2), where the driver could not
        // go on with that record itself: it ended the park `completed` with their words kept, saying why
        // (D131 §2), and the quest it held is carried on the same way. A park still parked holds its quest.
        // And a stop that was not the person's (D104): the sweep found nothing running it, or the driver
        // shut down under it.
        // And the person's own stop, of a take a session here made (SESSUX1b2): the driver holds the quest
        // until Try again releases it (D126 §3.3), so an open reaching here is the release. A stop before
        // the take leaves none here, and the quest taken since is somebody else's, as after a stand-down.
        var last = quest is { Status: QuestStatus.Taken } && question is null
            ? await sessions.LastOwnForQuestAsync(quest.Id, ct).ConfigureAwait(false)
            : null;
        var carriesOn = last is { State: SessionState.Failed }
                or { State: SessionState.Completed, Answer: not null }
                or { State: SessionState.Stopped, Interrupted: true }
            || (last is { State: SessionState.Stopped } && await sessions.TookHereAsync(quest.Id, ct).ConfigureAwait(false));

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
    /// <param name="interrupted">
    /// That a stop was not the person's (D104): the orphan sweep's, or the driver's shutdown. Only a move
    /// to <c>stopped</c> may say so, since it says whose decision a stop was.
    /// </param>
    /// <param name="limit">
    /// That an account's limit refused the turn (TOOL4c, D125 §5.2), as the driver read it from the door's
    /// failure. Only a move to <c>failed</c> may say so, since it says why a turn failed.
    /// </param>
    public async Task<SessionAdvanceOutcome> AdvanceAsync(
        string id, string state, string? note, string? evidence, string? transcript,
        DateTimeOffset now, CancellationToken ct = default, bool interrupted = false, bool limit = false)
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

            // A session that parks again asks anew (ANSWER1b, D131 §5): the answer it went on with is not this park's.
            var moved = await sessions.SetStateAsync(
                    id, target.Value, note, evidence, transcript, now, inside, interrupted, limit,
                    clearAnswer: target == SessionState.AwaitingPerson)
                .ConfigureAwait(false);

            return new SessionAdvanceOutcome(
                SessionAdvanceRefusal.None,
                $"Session `{moved!.Id}` is now {Spell(moved.State)}.",
                moved);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether this session's record may be deleted (SESSUX1f, D126 §5.4), and if not why, in words a person can act on.
    /// Deletes nothing: the driver asks it for a refusal before judging its own half, the tree and a landing.
    /// </summary>
    public async Task<SessionDeleteOutcome> JudgeDeleteAsync(string id, CancellationToken ct = default)
    {
        var session = await sessions.FindAsync(id, ct).ConfigureAwait(false);
        return await JudgeAsync(id, session, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete a conversation's record that served no quest (SESSUX1f, D126 §5.4): ended, this machine's, named by no ask's
    /// intake and no quest's publisher, and held by no remote. Judged and removed as one step (REV3), so a record that
    /// moved since the driver asked is judged as it now stands.
    /// </summary>
    /// <remarks>
    /// <b>Why so narrow.</b> A session that served a quest is that work's record: which agent, which version, which
    /// account and which tree did it (D49 §4), and the quest's strikes and carry-ons are read from it (D58, D80). Archive is
    /// how such a session is cleared. A record pushed to a remote would leave the team's copy, since a session record does
    /// not travel as a deletion.
    /// </remarks>
    public async Task<SessionDeleteOutcome> DeleteAsync(string id, CancellationToken ct = default) =>
        await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            var judged = await JudgeAsync(id, session, inside).ConfigureAwait(false);
            if (judged.Refusal != SessionDeleteRefusal.None) return judged;

            await sessions.DeleteAsync(id, inside).ConfigureAwait(false);
            return judged with { Message = $"Deleted session `{id}`: its record is gone from this machine." };
        }, ct).ConfigureAwait(false);

    /// <summary>
    /// Which of these records <see cref="DeleteAsync"/> would delete, by the same rule (D95's way for quests): a list's
    /// answer says it per record, so a page offers *Delete…* only where the ledger would take it.
    /// </summary>
    public async Task<IReadOnlySet<string>> DeletableAsync(IEnumerable<Session> candidates, CancellationToken ct = default)
    {
        var named = await NamesAsync(ct).ConfigureAwait(false);
        return candidates
            .Where(session => Judge(session, named).Refusal == SessionDeleteRefusal.None)
            .Select(session => session.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<SessionDeleteOutcome> JudgeAsync(string id, Session? session, CancellationToken ct) =>
        session is null
            ? new(SessionDeleteRefusal.NotFound, $"No session `{id}`.", Session: null)
            : Judge(session, await NamesAsync(ct).ConfigureAwait(false));

    /// <summary>
    /// What names a session: each ask's intake, and each quest's publishing session (SESS1). Read whole, since a delete is
    /// rare and a list asks once for all its records.
    /// </summary>
    private async Task<(IReadOnlyList<Ask> Asks, IReadOnlyList<Quest> Quests)> NamesAsync(CancellationToken ct) =>
        (asks is null ? [] : await asks.ListAsync(includeClosed: true, ct: ct).ConfigureAwait(false),
         await quests.ListAsync(includeClosed: true, ct: ct).ConfigureAwait(false));

    /// <summary>
    /// The record's half of §5.4, in the order a person meets it: whose it is, whether it still runs, whether it served a
    /// quest, what names it, and whether the team holds a copy.
    /// </summary>
    private static SessionDeleteOutcome Judge(Session session, (IReadOnlyList<Ask> Asks, IReadOnlyList<Quest> Quests) named)
    {
        var id = session.Id;
        if (session.Origin is { } origin)
        {
            return new(SessionDeleteRefusal.NotOurs, $"Session `{id}` ran on `{origin}`; its record is theirs — archive it here instead.", session)
            {
                Origin = origin,
            };
        }

        if (session.Active)
        {
            return new(SessionDeleteRefusal.Live, $"Session `{id}` is still running; stop it first.", session);
        }

        if (session.Quest is { } served)
        {
            return new(
                SessionDeleteRefusal.ServedQuest,
                $"Session `{id}` worked on `#{served}`, and its record is that work's; archive it instead.",
                session) { Quest = served };
        }

        var intakeOf = named.Asks.FirstOrDefault(ask =>
            string.Equals(ask.Intake, id, StringComparison.Ordinal)
            || (session.Ask is { } asked && string.Equals(ask.Id, asked, StringComparison.OrdinalIgnoreCase)));
        if (intakeOf is not null)
        {
            return new(SessionDeleteRefusal.Named, $"Session `{id}` is named by ask `#{intakeOf.Id}` as its intake.", session)
            {
                Ask = intakeOf.Id,
            };
        }

        var publishedHere = named.Quests.FirstOrDefault(quest => string.Equals(quest.PublishedBy, id, StringComparison.Ordinal));
        if (publishedHere is not null)
        {
            return new(SessionDeleteRefusal.Named, $"Session `{id}` published `#{publishedHere.Id}`, which names it.", session)
            {
                Quest = publishedHere.Id,
            };
        }

        if (session.Pushed)
        {
            return new(
                SessionDeleteRefusal.OnRemote,
                $"The remote for `{session.Workspace}` holds session `{id}`; a delete here would not reach the team's copy, so "
                + "archive it instead.",
                session) { Workspace = session.Workspace };
        }

        return new(SessionDeleteRefusal.None, $"Session `{id}` may be deleted.", session);
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
