using System.Globalization;

namespace Daoris.Knowledge;

/// <summary>Why a session was not opened — or <see cref="None"/> when it was.</summary>
public enum SessionOpenRefusal
{
    None,

    /// <summary>No quest under that id.</summary>
    QuestNotFound,

    /// <summary>The quest is not open. Taken is the mutex between the driver and outside work (D46).</summary>
    QuestNotOpen,

    /// <summary>
    /// A carry-on (D80) over a take that is not this machine's (CARRY2): another machine's by the quest's log, or one made
    /// here after this machine's last session on the quest ended. A carry-on goes on with this machine's take, and there
    /// is none to go on with.
    /// </summary>
    TakenElsewhere,

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

    /// <summary>
    /// The session is finished, and a finished session is a record. Records do not move — but for the one move out of an
    /// ended state, to working with the person's words waiting (MSG1a, D137 §2.3).
    /// </summary>
    Terminal,

    /// <summary>A move the lifecycle does not allow.</summary>
    InvalidMove,

    /// <summary>
    /// An ended record would go on in a tree another session now holds (MSG1a): one session per working tree (D51)
    /// holds for going on as for a start.
    /// </summary>
    Busy,
}

/// <summary>Why the person's words were not kept for a session to go on with (MSG1a, D137 §5.3) — or <see cref="None"/>.</summary>
public enum SessionSayRefusal
{
    None,

    /// <summary>No words: nothing was said. Or, taking words, none were named.</summary>
    Empty,

    /// <summary>No session under that id.</summary>
    NotFound,

    /// <summary>A teammate's record (D47 §6): its process and its conversation are on their machine and account.</summary>
    NotOurs,

    /// <summary>An intake, which is answered through its ask (INT4h).</summary>
    Intake,

    /// <summary>It stood down: its quest is someone else's, so it has nothing to go on with.</summary>
    StoodDown,

    /// <summary>It runs: words to a running session reach it through the driver that runs it (D136), not its record.</summary>
    Running,

    /// <summary>
    /// It read another session's work for a second opinion (XAGENT1c, D155 point 7): a pass is one turn, takes no words, and
    /// is never resumed. The person asks again, with their words, instead.
    /// </summary>
    Opinion,
}

/// <param name="Refusal"><see cref="SessionSayRefusal.None"/> when the words were kept.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Session">The session as it now stands, when the words were kept.</param>
/// <param name="Word">The word as it was kept, with its id, when they were.</param>
public sealed record SessionSayOutcome(SessionSayRefusal Refusal, string Message, Session? Session, SaidWord? Word = null)
{
    /// <summary>The quest of a session that stood down, where that refused the words.</summary>
    public string? Quest { get; init; }

    /// <summary>The ask an intake answers, where that refused the words.</summary>
    public string? Ask { get; init; }

    /// <summary>The machine a teammate's record ran on, where that refused the words.</summary>
    public string? Origin { get; init; }

    /// <summary>The second opinion a reviewer's record read the work for, where that refused the words.</summary>
    public string? Opinion { get; init; }
}

/// <param name="Refusal"><see cref="SessionSayRefusal.None"/> when the words were taken off the record.</param>
/// <param name="Message">The whole answer, phrased once here.</param>
/// <param name="Session">The session as it now stands, when they were.</param>
/// <param name="Taken">The words taken, in the order they were said: those named that the record held.</param>
public sealed record SessionTakeOutcome(SessionSayRefusal Refusal, string Message, Session? Session, IReadOnlyList<SaidWord> Taken);

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

    /// <summary>
    /// It served a quest, and is that work's record: the strikes and the carry-ons are read from it (D58, D80). A chat that
    /// took one through its own connector served it too (CHATTAKE1).
    /// </summary>
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
    /// <summary>
    /// The quest it served, or the quest it published, where that refused it. Null for a chat that took one, whose record
    /// says it took a quest but not which (CHATTAKE1).
    /// </summary>
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
    /// A chat has no quest of its own, so whatever it takes is the work it served (CHATTAKE1).
    /// </summary>
    /// <remarks>
    /// <b>A chat's take is marked too</b>, or its record reads as serving no quest and a delete removes that work's record
    /// (D126 §5.4). Every reader that asks a take of a quest keys on the record's quest, which a chat's is not, so the mark
    /// changes no carry-on and no stand-down.
    /// </remarks>
    public async Task<bool> MarkTookAsync(string sessionId, string questId, CancellationToken ct = default)
    {
        var session = await sessions.FindAsync(sessionId, ct).ConfigureAwait(false);
        if (session is not { Active: true, Origin: null }) return false;

        var served = session.Quest is { } quest
            ? string.Equals(quest, questId.TrimStart('#'), StringComparison.OrdinalIgnoreCase)
            : session.Kind == SessionKind.Chat;
        return served && await sessions.MarkTookAsync(sessionId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The person answers a driven session that parked to ask them (STANDDOWN2): the record stays
    /// `awaiting-person` with their words kept and said on its note, and the driver goes on with it at
    /// its next look (ANSWER1b, D131) — the session's own conversation resumed with the answer where it
    /// can, else the park ended and its quest carried on in a new session in the same tree, handed what
    /// they said. A second answer before then joins the first (MSG1a, D137 §2.4). An intake is answered
    /// through its ask. This is the parked case of <see cref="SayAsync"/>, kept for its door and its words.
    /// </summary>
    /// <remarks>
    /// <b>The answer keeps the park</b> because one record stands for one harness conversation (D131 §3).
    /// </remarks>
    /// <returns>The session as it now stands, or a refusal in words a person can act on.</returns>
    public async Task<SessionAdvanceOutcome> AnswerAsync(
        string id, string? answer, DateTimeOffset now, CancellationToken ct = default)
    {
        var said = string.IsNullOrWhiteSpace(answer) ? CarryOn : answer.Trim();

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
                        ? IntakeRefusal(id, session.Ask)
                        : $"Session `{id}` is {Session.Spell(session.State)}, not waiting on you — there is nothing to answer.",
                    Session: null);
            }

            var answered = await KeepAnswerAsync(session, said, now, inside).ConfigureAwait(false);
            return new SessionAdvanceOutcome(
                SessionAdvanceRefusal.None,
                $"Answered session `{id}`: it carries on with `#{session.Quest}` at the driver's next look.",
                answered);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>The answer a park keeps where the person gave no words of their own (ANSWER1b): what it goes on with.</summary>
    private const string CarryOn = "carry on.";

    /// <summary>
    /// Keep <paramref name="said"/> as a park's answer, joining any before it (MSG1a), with its line on the note: inside the
    /// store's lock, by a caller that found the record parked there.
    /// </summary>
    private Task<Session?> KeepAnswerAsync(Session parked, string said, DateTimeOffset now, CancellationToken inside)
    {
        var word = new SaidWord(NewWordId(), said, now, []);
        var (note, parts) = AnsweredNote(parked, said);
        return sessions.KeepSaidAsync(parked.Id, word, note, inside, noteParts: parts);
    }

    /// <summary>
    /// Keep what the person said to a parked or ended session of this machine's, for it to go on with (MSG1a, D137 §2.2,
    /// §5.3): verbatim, in order after the words already waiting, with its own id, when, and its files' names. The record
    /// does not move. A park takes the words as its answer, said on its note as an answer is; an ended record keeps them
    /// for the driver's next look, which takes it out of its ended state with them (<see cref="AdvanceAsync"/>).
    /// </summary>
    /// <remarks>
    /// <para><b>What never goes on is refused, before anything is kept</b>: a teammate's record, whose process and
    /// conversation are on their machine; an intake, answered through its ask; a session that stood down, whose quest is
    /// someone else's. So is a running session, which hears words through the driver that runs it (D136), never its
    /// record.</para>
    ///
    /// <para><b>The words are this machine's alone</b>, like a transcript: on an ended record nothing that travels changes,
    /// so no revision is written. A park's answer line is on its note, which travels, as ANSWER1b's always was.</para>
    ///
    /// <para><b>Keeping them on the ask is the door's</b> (DRIFT1a), beside this and never inside it: a park's answer at
    /// once, words to an ended record once the session took them (<see cref="TakeSaidAsync"/>).</para>
    /// </remarks>
    /// <param name="files">What the person gave with the words: only each one's name is kept, never where it is.</param>
    public async Task<SessionSayOutcome> SayAsync(
        string id, string? text, IReadOnlyList<string>? files, DateTimeOffset now, CancellationToken ct = default)
    {
        var words = text?.Trim() ?? "";
        if (words.Length == 0)
        {
            return new(SessionSayRefusal.Empty, "There are no words to keep: the person said nothing.", Session: null);
        }

        // Read, judged and written as one step (REV3): the driver taking the record up and the words arriving could
        // otherwise both read it waiting, and the words land on a record already going on.
        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionSayOutcome(SessionSayRefusal.NotFound, $"No session `{id}`.", Session: null);
            }

            if (session.Origin is { } origin)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.NotOurs, $"Session `{id}` ran on `{origin}`, where its conversation is; it cannot go on here.",
                    Session: null) { Origin = origin };
            }

            if (session.Ask is { } ask)
            {
                return new SessionSayOutcome(SessionSayRefusal.Intake, IntakeRefusal(id, ask), Session: null) { Ask = ask };
            }

            if (session.Opinion is { } opinion)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Opinion,
                    $"Session `{id}` read another session's work for second opinion `{opinion}`: a pass takes one turn and no words, "
                    + "and is never resumed. Ask again, with your words, for a fresh one.",
                    Session: null) { Opinion = opinion };
            }

            if (session.State == SessionState.StoodDown)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.StoodDown,
                    session.Quest is { } theirs
                        ? $"Session `{id}` stood down: `#{theirs}` is someone else's, so it has nothing to go on with."
                        : $"Session `{id}` stood down, so it has nothing to go on with.",
                    Session: null) { Quest = session.Quest };
            }

            var parked = session.State == SessionState.AwaitingPerson;
            if (session.Active && !parked)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Running,
                    $"Session `{id}` is {Spell(session.State)}: words to a running session reach it through the driver that "
                    + "runs it, not its record.",
                    Session: null);
            }

            var word = new SaidWord(NewWordId(), words, now, Names(files), Reopens: !parked);
            var (note, parts) = parked ? AnsweredNote(session, words) : (null, null);
            var kept = await sessions
                .KeepSaidAsync(id, word, note, inside, noteParts: parts)
                .ConfigureAwait(false);
            return new SessionSayOutcome(SessionSayRefusal.None, KeptMessage(session), kept, word);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Take the person's words off this machine's record once a session took them (MSG1a, D137 §2.4): the resumed run's
    /// first prompt went, or <paramref name="by"/>, the session a fallback handed them to, took them. By their ids, so a
    /// word said after the driver read the record stays for the next run; an id the record does not hold is passed over.
    /// </summary>
    /// <param name="by">The session that took them where it is not this one; it must be this machine's too.</param>
    /// <returns>The record as it now stands and the words taken, for the door to keep on the ask those said after it ended.</returns>
    public async Task<SessionTakeOutcome> TakeSaidAsync(
        string id, IReadOnlyCollection<string> ids, string? by, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return new(SessionSayRefusal.Empty, "Name the words taken: none were named, so nothing was taken.", Session: null, []);
        }

        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionTakeOutcome(SessionSayRefusal.NotFound, $"No session `{id}`.", Session: null, []);
            }

            if (session.Origin is { } origin)
            {
                return new SessionTakeOutcome(
                    SessionSayRefusal.NotOurs, $"Session `{id}` ran on `{origin}`; no words wait on it here.", Session: null, []);
            }

            if (by is not null && !string.Equals(by, id, StringComparison.Ordinal)
                && await sessions.FindAsync(by, inside).ConfigureAwait(false) is not { Origin: null })
            {
                return new SessionTakeOutcome(
                    SessionSayRefusal.NotFound, $"No session `{by}` of this machine's took them.", Session: null, []);
            }

            var taken = session.Said.Where(word => ids.Contains(word.Id, StringComparer.Ordinal)).ToList();
            var left = await sessions.TakeSaidAsync(id, ids, inside).ConfigureAwait(false);
            return new SessionTakeOutcome(
                SessionSayRefusal.None,
                taken.Count == 0
                    ? $"None of those words wait on session `{id}`; nothing was taken."
                    : $"Took {taken.Count} of the person's words off session `{id}`"
                      + (by is null || by == id ? "." : $": session `{by}` took them."),
                left, taken);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>A new word's id: random, as a session's is, and never <see cref="SessionStore.AnswerWordId"/>.</summary>
    private static string NewWordId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Each file's name alone (MSG1a, D137 §2.4): what is kept of a file given with the words is what a person reads, never
    /// where it is on this machine. Read after either separator, whatever machine it was written on.
    /// </summary>
    private static IReadOnlyList<string> Names(IReadOnlyList<string>? files) =>
        (files ?? [])
            .Select(file => file?.Trim() ?? "")
            .Select(file => file[(file.LastIndexOfAny(['/', '\\']) + 1)..])
            .Where(name => name.Length > 0)
            .ToList();

    /// <summary>What the person is told their words will do, phrased once here for every door.</summary>
    private static string KeptMessage(Session session) =>
        session.State == SessionState.AwaitingPerson && session.Quest is { } quest
            ? $"Kept for session `{session.Id}`: it carries on with `#{quest}` at the driver's next look."
            : session.Kind == SessionKind.Chat
                // A conversation is taken up by its runner the moment it can open, never by the driver's look.
                ? $"Kept for session `{session.Id}`: the same conversation goes on with your words as it opens again."
                : $"Kept for session `{session.Id}`: the same session goes on with your words at the driver's next look.";

    private static string IntakeRefusal(string id, string ask) =>
        $"Session `{id}` is an intake — answer its ask `#{ask}` instead: publish it or close it.";

    /// <summary>
    /// The note an answer leaves (ANSWER1b): what the session asked, then what it was told, since the conversation that
    /// goes on and a session that carries the quest on are both read from this record, and an answer without its
    /// question is half a conversation. A second answer adds its own line after the first (MSG1a), so the note says
    /// what the record holds.
    /// </summary>
    /// <remarks>
    /// Its lines with their codes beside the English (LANG1a, the language design §4 rows 64–65): the record's parts, or its
    /// note carried whole, or the ledger's own line where it had none; then <c>Answered:</c> and the person's words, a part of
    /// their own.
    /// </remarks>
    internal static (string Note, string Parts) AnsweredNote(Session parked, string said)
    {
        const string Asked = "It stopped to ask the person; its question is in its transcript.";
        var note = (parked.Note ?? Asked) + AnsweredLine(said);
        NoteLine[] answered = [NoteLine.Coded(LedgerNoteCodes.Answered, "Answered:"), NoteLine.Said(said, "person")];
        var parts = parked.Note is null
            ? NoteParts.After(null, null, [NoteLine.Coded(LedgerNoteCodes.Parked, Asked), .. answered])
            : NoteParts.After(parked.NoteParts, parked.Note, answered);
        return (note, parts);
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
        var tookHere = last is not null && await sessions.TookHereAsync(quest.Id, ct).ConfigureAwait(false);
        var carriesOn = last is { State: SessionState.Failed }
                or { State: SessionState.Completed, Answer: not null }
                or { State: SessionState.Stopped, Interrupted: true }
            || (last is { State: SessionState.Stopped } && tookHere);

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

        // A carry-on goes on with this machine's take, so it asks whose the take is (CARRY2). D80 asked none, so a
        // session that failed before its take carried on over whoever took the quest since.
        if (carriesOn && await TakenElsewhereAsync(quest, last!, tookHere, ct).ConfigureAwait(false) is { } elsewhere)
        {
            return new(SessionOpenRefusal.TakenElsewhere, elsewhere, Session: null);
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
    /// it prints. A finished session does not move, but for one move: to working, with the person's words
    /// waiting on a record of this machine's (MSG1a, D137 §2.3).
    /// </summary>
    /// <param name="interrupted">
    /// That a stop was not the person's (D104): the orphan sweep's, or the driver's shutdown. Only a move
    /// to <c>stopped</c> may say so, since it says whose decision a stop was.
    /// </param>
    /// <param name="limit">
    /// That an account's limit refused the turn (TOOL4c, D125 §5.2), as the driver read it from the door's
    /// failure. Only a move to <c>failed</c> may say so, since it says why a turn failed.
    /// </param>
    /// <param name="noteParts">
    /// The note's parts beside it (LANG1a, D142 point 2), as <see cref="NoteParts.Normalize"/> keeps them; only with a note.
    /// A note moved without them clears the record's.
    /// </param>
    public async Task<SessionAdvanceOutcome> AdvanceAsync(
        string id, string state, string? note, string? evidence, string? transcript,
        DateTimeOffset now, CancellationToken ct = default, bool interrupted = false, bool limit = false, string? noteParts = null)
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
                return target == SessionState.Working
                    ? await GoOnAsync(session, note, noteParts, evidence, transcript, now, inside).ConfigureAwait(false)
                    : new SessionAdvanceOutcome(
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
                    clearSaid: target == SessionState.AwaitingPerson, noteParts: noteParts)
                .ConfigureAwait(false);

            return new SessionAdvanceOutcome(
                SessionAdvanceRefusal.None,
                $"Session `{moved!.Id}` is now {Spell(moved.State)}.",
                moved);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The one move out of an ended state (MSG1a, D137 §2.3): an ended record of this machine's goes on to working with the
    /// person's words waiting, because one record stands for one harness conversation (D131 §3) and the conversation goes
    /// on. Called inside <see cref="AdvanceAsync"/>'s step, the record already read.
    /// </summary>
    /// <remarks>
    /// <para><b>Nothing else leaves an ended state.</b> A teammate's record, a stand-down, an intake, and a record with no
    /// words waiting each stay as they ended; and the tree it worked in must be free, since one session per working tree
    /// (D51) holds for going on as for a start.</para>
    ///
    /// <para><b>Its history stays.</b> The note keeps what ended it and says when it went on, a note passed with the
    /// move after that; the evidence stays, so the review counts from the record's own base. What ended it is forgiven
    /// (a stop the sweep made, a limit's failure), so it says nothing of the run that follows. The words stay until that
    /// run takes them (<see cref="TakeSaidAsync"/>).</para>
    /// </remarks>
    private async Task<SessionAdvanceOutcome> GoOnAsync(
        Session session, string? note, string? noteParts, string? evidence, string? transcript, DateTimeOffset now, CancellationToken inside)
    {
        var id = session.Id;
        var refused = session switch
        {
            { Origin: { } origin } => $"Session `{id}` ran on `{origin}`; a finished record of another machine's does not move here.",
            { State: SessionState.StoodDown } => $"Session `{id}` stood down — a stand-down has nothing to go on with, so it does not move.",
            { Ask: { } ask } => $"Session `{id}` is an intake — it is answered through its ask `#{ask}`, so it does not move.",
            // XAGENT1c: a pass is one turn, read afresh each time (design §4), so a reviewer's record never goes on.
            { Opinion: { } opinion } => $"Session `{id}` read the work for second opinion `{opinion}` — a pass is never resumed, so it does not move.",
            { Said.Count: 0 } =>
                $"Session `{id}` is {Spell(session.State)} — a finished session goes on only with the person's words, and none wait for it.",
            _ => null,
        };
        if (refused is not null)
        {
            return new SessionAdvanceOutcome(SessionAdvanceRefusal.Terminal, refused, Session: null);
        }

        if (await sessions.ActiveForAsync(session.Repository, session.Tree, inside).ConfigureAwait(false) is { } holder)
        {
            return new SessionAdvanceOutcome(SessionAdvanceRefusal.Busy, Busy(session.Repository, holder), Session: null);
        }

        var (went, parts) = WentOnNote(session, note, noteParts, now);
        var moved = await sessions
            .SetStateAsync(id, SessionState.Working, went, evidence, transcript, now, inside, forgive: true, noteParts: parts)
            .ConfigureAwait(false);

        return new SessionAdvanceOutcome(
            SessionAdvanceRefusal.None,
            $"Session `{id}` is working again: it goes on from {Spell(session.State)} with the person's words.",
            moved);
    }

    /// <summary>
    /// The note an ended record goes on with (MSG1a, D137 §2.3): what ended it, when it went on, and the note the move passed,
    /// each with its parts (LANG1a, the language design §4 row 66): the record's own, or its note carried whole; the ledger's
    /// line with its moment; the move's own, or its note carried whole.
    /// </summary>
    internal static (string Note, string Parts) WentOnNote(Session session, string? note, string? noteParts, DateTimeOffset now)
    {
        var line = $"Went on with your words at {now.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC.";
        var went = string.Join("\n\n", new[] { session.Note, line, note }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var parts = NoteParts.After(
            string.IsNullOrWhiteSpace(session.Note) ? null : session.NoteParts, session.Note,
            [NoteLine.Coded(LedgerNoteCodes.WentOn, line, ("at", NoteParts.Moment(now)))],
            string.IsNullOrWhiteSpace(note) ? null : noteParts, note);
        return (went, parts);
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

        // A chat's take names no quest on its record (CHATTAKE1), so its sentence says the take without one.
        if (session.Took)
        {
            return new(
                SessionDeleteRefusal.ServedQuest,
                $"Session `{id}` took a quest, and its record is that work's; archive it instead.",
                session);
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
    /// Whose a carry-on's take is (CARRY2): the refusal's sentence when it is not this machine's, else null.
    /// </summary>
    /// <remarks>
    /// <para><b>The quest's log names the machine that took it</b> (D68 §2): a take another machine made is theirs,
    /// whichever session here failed or stopped, and so is one that beat this machine's own take to the remote (D68 §5).
    /// Never by the clock, which is another machine's there. Named by a teammate's record on the quest where one came,
    /// since the log's machine is an id no person reads, else as another machine (D132's words).</para>
    ///
    /// <para><b>A take made here after this machine's last session on the quest ended</b> is not that session's, and no
    /// session here marked it (STANDDOWN2): a chat's connector took it, whose mark names no quest (CHATTAKE1), or work
    /// outside Daoris did. Both times are this machine's own clock. A take with no mark made before the session ended
    /// is still carried on, as D80 did: the HTTP door marks none, and a take from before STANDDOWN2 has none.</para>
    /// </remarks>
    private async Task<string?> TakenElsewhereAsync(Quest quest, Session last, bool tookHere, CancellationToken ct)
    {
        var take = (await quests.HistoryAsync(quest.Id, ct).ConfigureAwait(false))
            .LastOrDefault(operation => operation.Kind == QuestOperationKind.Taken);
        if (take is null) return null;

        var over = $"the take is theirs, so session `{last.Id}` is not carried on over it.";
        if (!string.Equals(take.Machine, quests.Machine, StringComparison.Ordinal))
        {
            return await sessions.LastTeammateForQuestAsync(quest.Id, ct).ConfigureAwait(false) is { } teammate
                ? $"Quest `#{quest.Id}` is taken on `{teammate.Origin}`, by session `{teammate.Id}`: {over}"
                : $"Quest `#{quest.Id}` is taken on another machine: {over}";
        }

        return !tookHere && take.At > last.Updated
            ? $"Quest `#{quest.Id}` was taken here after session `{last.Id}` ended, by a chat or by work outside Daoris: {over}"
            : null;
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
