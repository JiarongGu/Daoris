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

    /// <summary>
    /// The workspace named is not one this machine holds (ASKSCOPE1a, D158's ASKSCOPE1 note point 1): Ask Daoris's conversation
    /// is started in a workspace, and keeps it.
    /// </summary>
    WorkspaceUnknown,
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

    /// <summary>
    /// It is a helper's record (MIXAGENT2a-i, D163 point 2): a helper takes one turn, answers a question and is never resumed.
    /// </summary>
    Helper,

    /// <summary>
    /// Taking words back (SAIDBACK1a, D137 §2.4): the word no longer waits on the record, or the record runs again, so its
    /// run may already have read it. An answer, never an error, as a live queue's taken word is (QUEUEEDIT1a, D76).
    /// </summary>
    Taken,

    /// <summary>
    /// The words name an author this build does not know (COWORK1b, <see cref="SaidFrom"/>): kept as the person's, they would be
    /// read as theirs, so nothing is kept.
    /// </summary>
    From,

    /// <summary>
    /// The words name a level this build does not know (COWORK1c, <see cref="SaidLevel"/>): kept, they would reach the session at a
    /// point nobody asked for, so nothing is kept.
    /// </summary>
    Level,
}

/// <summary>What marking words attempted did (COWORK1c): the record as it now stands, and the words named that no longer wait.</summary>
public sealed record SessionAttemptOutcome(SessionSayRefusal Refusal, string Message, Session? Session, IReadOnlyList<string> Unmarked);

/// <param name="Refusal"><see cref="SessionSayRefusal.None"/> when the words were kept.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Session">The session as it now stands, when the words were kept.</param>
/// <param name="Word">The word as it was kept, with its id, when they were; a reword's new word (SAIDBACK1a).</param>
public sealed record SessionSayOutcome(SessionSayRefusal Refusal, string Message, Session? Session, SaidWord? Word = null)
{
    /// <summary>The word a take-back took off the record, as it waited (SAIDBACK1a), when it did.</summary>
    public SaidWord? Taken { get; init; }

    /// <summary>
    /// The say was answered with the word its producer's key already kept (COWORK1b, the message-urgency design §6): nothing was kept
    /// now, so nothing beside the say is kept again either.
    /// </summary>
    public bool Again { get; init; }

    /// <summary>
    /// The words answered a park (ANSWER1b), which its door keeps on the ask at once. False for words to an ended record, which are
    /// kept there once taken, and for words kept for a running record's run (COWORK1c), which its run keeps there as it takes them.
    /// </summary>
    public bool Answers { get; init; }

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
public sealed class SessionLedger(
    QuestStore quests, SessionStore sessions, KnowledgeService? registry = null, AskStore? asks = null)
{
    /// <summary>The go-ahead family over the ledger's stores (PART6).</summary>
    private readonly SessionGoAheads goAheads = new(quests, sessions, asks);

    /// <summary>The set-up step's serve and say family over the ledger's stores (PART6).</summary>
    private readonly SessionReviews reviews = new(quests, sessions);

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
                    harnessVersion, profile, holding, baseCommit: null, inside, ask: ask.Id,
                    entryOrigin: SessionEntries.AskOrigin(ask.Id), entryPart: "intake")
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
    /// Whether a record is Ask Daoris's (HELP1a), by its kind of place: its "repository" is <see cref="HelpRepository"/>. What
    /// keeps it off every remote feed whatever its workspace (ASKSCOPE1a, D158's ASKSCOPE1 note point 2).
    /// </summary>
    public static bool IsHelp(Session session) => string.Equals(session.Repository, HelpRepository, StringComparison.Ordinal);

    /// <summary>Ask Daoris's mode that reads and proposes (D89), and what a help record from before the modes ran under (HELPMODE1a2).</summary>
    public const string HelpRestricted = "restricted";

    /// <summary>Ask Daoris's mode that acts on the machine itself (D183 point 1).</summary>
    public const string HelpFull = "full";

    /// <summary>
    /// The access modes an Ask Daoris record's run may get (HELPMODE1a2, D183 point 3), as the ledger spells them. A twin
    /// (`.claude/knowledge/twins.md`): the driver's <c>HelpModes</c> spells them too, and each side's test holds the spelling.
    /// </summary>
    public static IReadOnlyList<string> HelpModes { get; } = [HelpRestricted, HelpFull];

    /// <summary>The mode <paramref name="mode"/> names in any case, trimmed; null names restricted; any other is refused.</summary>
    /// <exception cref="ArgumentException">A mode the ledger does not know.</exception>
    private static string HelpModeOf(string? mode) =>
        mode is null ? HelpRestricted
            : HelpModes.FirstOrDefault(each => string.Equals(each, mode.Trim(), StringComparison.OrdinalIgnoreCase))
              ?? throw new ArgumentException(
                  $"`{mode}` is no access mode of Ask Daoris's — it runs `{HelpRestricted}` or `{HelpFull}`.", nameof(mode));

    /// <summary>
    /// Record the access mode an Ask Daoris conversation of this machine's goes on under (HELPMODE1a2, D183 point 3): a conversation
    /// going on after the person's mode changed runs under the new one, and its record says so. Its state, its last move and its
    /// revision stay as they are: the mode is a fact about its run, and it never travels.
    /// </summary>
    /// <returns>The record as it now reads; null where no Ask Daoris record of this machine's has that id.</returns>
    /// <exception cref="ArgumentException">A mode the ledger does not know.</exception>
    public async Task<Session?> KeepHelpModeAsync(string id, string mode, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mode);
        var named = HelpModeOf(mode);
        return await sessions.KeepHelpModeAsync(id, named, ct).ConfigureAwait(false)
            ? await sessions.FindAsync(id, ct).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Record the access setup a resumed driven session of this machine's now runs under (AUTONOMY1b2, D184 point 1), where the setup
    /// changed since it started: its record says the setup this run goes on under. Its state, its last move and its revision stay as
    /// they are: the setup is a fact about its run, and it never travels.
    /// </summary>
    /// <returns>The record as it now reads; null where no driven record of this machine's has that id.</returns>
    /// <exception cref="ArgumentException">A setup that is not a short map of words, or none.</exception>
    public async Task<Session?> KeepAccessAsync(string id, IReadOnlyDictionary<string, string> access, CancellationToken ct = default)
    {
        var setup = SessionAccess.Judged(access)
            ?? throw new ArgumentException("access is the setup the session now runs under, and names at least one pause.", nameof(access));
        return await sessions.KeepAccessAsync(id, setup, ct).ConfigureAwait(false)
            ? await sessions.FindAsync(id, ct).ConfigureAwait(false)
            : null;
    }

    /// <summary>The most versions one record keeps (KEEP1b): a session's starts are handed a few; more is no start's list.</summary>
    public const int MostResolved = 64;

    /// <summary>
    /// Record the downloaded versions a start of a record of this machine's was handed (KEEP1b, D195 point 6; the cleanup design §3.5,
    /// Fable F2.5), added to those of its earlier starts: the downloads' rule keeps each while the session runs. Any kind of record, since
    /// every start runs a binary. Its state, its last move and its revision stay: what a start was handed is a fact about its run, and it
    /// never travels.
    /// </summary>
    /// <returns>The record as it now reads; null where no record of this machine's has that id.</returns>
    /// <exception cref="ArgumentException">
    /// None named, more than <see cref="MostResolved"/>, or one that is no <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c>: a path never is.
    /// </exception>
    public async Task<Session?> KeepResolvedAsync(string id, IReadOnlyList<string> versions, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count is 0 or > MostResolved)
        {
            throw new ArgumentException($"resolved names the versions a start was handed, one to {MostResolved} of them.", nameof(versions));
        }

        if (versions.FirstOrDefault(version => !IsVersionKey(version)) is { } wrong)
        {
            throw new ArgumentException(
                $"`{OneLine(Clip(wrong, 80))}` is no downloaded version's key: each is agent|tool/<name>/<version>, and never a path.", nameof(versions));
        }

        var before = await sessions.FindAsync(id, ct).ConfigureAwait(false);
        if (before is null || before.Origin is not null) return null;
        if ((before.Resolved ?? []).Union(versions, StringComparer.Ordinal).Count() > MostResolved)
        {
            throw new ArgumentException($"a record keeps at most {MostResolved} versions its starts were handed.", nameof(versions));
        }

        return await sessions.KeepResolvedAsync(id, versions, ct).ConfigureAwait(false)
            ? await sessions.FindAsync(id, ct).ConfigureAwait(false)
            : null;
    }

    /// <summary>Whether a text is a downloaded version's key: <c>agent</c> or <c>tool</c>, then a name and a version, each a plain segment.</summary>
    private static bool IsVersionKey(string? key)
    {
        if (key is null || key.Length > 200) return false;
        var parts = key.Split('/');
        return parts.Length == 3 && parts[0] is "agent" or "tool"
            && parts.Skip(1).All(part => part.Length > 0 && part is not ("." or "..")
                && part.All(each => char.IsAsciiLetterOrDigit(each) || each is '.' or '-' or '_' or '+' or '@'));
    }

    /// <summary>
    /// Open Ask Daoris's conversation (HELP1a, D89) — a chat about Daoris itself, in the room the
    /// driver keeps for it under its home.
    /// </summary>
    /// <remarks>
    /// <para><b>The intake's open, for a conversation.</b> It serves no quest and no ask, so nothing
    /// plans from it. It belongs to the workspace its window was on when it started (ASKSCOPE1a, D158's ASKSCOPE1 note point
    /// 1), recorded here and never moved; none named is <c>default</c>, as a record from before reads.</para>
    ///
    /// <para><b>One running per workspace</b> (point 3): a second in the same workspace is refused naming the first, and the
    /// driver hands the page the running one rather than asking for another; one in another workspace runs beside it, in the
    /// same room folder (D158's ASKSCOPE1a note, point 4, says why that is safe).</para>
    ///
    /// <para><b>A workspace this machine does not hold is refused</b>, naming those it does: <c>default</c> and each registered
    /// repository's. With no registry to ask, the name is taken as told.</para>
    /// </remarks>
    /// <param name="workspace">The workspace the person's window was on at the press; null is <c>default</c>.</param>
    /// <param name="mode">
    /// The access mode its start got (HELPMODE1a2, D183 point 3), the driver's to say, since it is the half that spawns it; null is
    /// <see cref="HelpRestricted"/>, as a driver from before sends it.
    /// </param>
    /// <exception cref="ArgumentException">No room, or a mode the ledger does not know.</exception>
    public async Task<SessionOpenOutcome> OpenHelpAsync(
        string adapter, string room, DateTimeOffset now,
        string? harnessVersion = null, string? profile = null, string? workspace = null, string? mode = null,
        CancellationToken ct = default)
    {
        var holding = Trees.Normalize(room)
            ?? throw new ArgumentException("Ask Daoris runs in a room — name it", nameof(room));
        var running = HelpModeOf(mode);
        var circle = Workspaces.Normalize(workspace);

        if (registry is not null)
        {
            var held = (await registry.RegistryAsync(ct: ct).ConfigureAwait(false))
                .Select(row => Workspaces.Normalize(row.InWorkspace)).Append(Workspaces.Default)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
            if (!held.Any(each => Workspaces.Same(each, circle)))
            {
                return new SessionOpenOutcome(
                    SessionOpenRefusal.WorkspaceUnknown,
                    $"`{circle}` is not a workspace this machine holds — Ask Daoris's conversation is started in the workspace "
                    + $"its window is on. Held here: {string.Join(", ", held)}.",
                    Session: null);
            }

            // Spelled as the machine holds it, so one workspace is never two by its letters' case.
            circle = held.First(each => Workspaces.Same(each, circle));
        }

        return await sessions.ExclusiveAsync(async inside =>
        {
            var holder = await sessions.RunningInWorkspaceAsync(HelpRepository, circle, parked: false, inside).ConfigureAwait(false);
            if (holder is not null)
            {
                return new SessionOpenOutcome(
                    SessionOpenRefusal.RepositoryBusy,
                    $"Ask Daoris already has a conversation running in `{holder.Workspace}` — `{holder.Id}` "
                    + $"({Spell(holder.State)}). One runs at a time in a workspace; carry that one on, or finish it first.",
                    Session: null);
            }

            // No base commit, for the intake's reason: the room is no repository.
            var session = await sessions
                .CreateAsync(
                    null, HelpRepository, adapter, now, circle, SessionKind.Chat,
                    harnessVersion, profile, holding, baseCommit: null, inside, ownEntry: true, helpMode: running)
                .ConfigureAwait(false);

            return new SessionOpenOutcome(
                SessionOpenRefusal.None,
                $"Ask Daoris `{session.Id}` opened in `{session.Workspace}`, via {adapter}.",
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
    /// <param name="from">The conversation this one continues, handed over (MIXAGENT1a-iii), with <paramref name="fromWhy"/> and <paramref name="fromBy"/>; recorded as told.</param>
    public async Task<SessionOpenOutcome> OpenChatAsync(
        string repository, string adapter, DateTimeOffset now,
        string? harnessVersion = null, string? profile = null, string? tree = null,
        string? baseCommit = null, CancellationToken ct = default,
        string? from = null, string? fromWhy = null, string? fromBy = null)
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

        // SESSONE1a: a conversation is its own line, and one handed over or started from another's words takes that one's (D167 point 2).
        var continuedOrigin = from is not null && await sessions.FindOrMirrorAsync(from, ct).ConfigureAwait(false) is { } continued
            ? await SessionEntries.OriginOfAsync(continued, new StoreEntryFacts(quests, sessions), ct).ConfigureAwait(false)
            : null;

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
                    harnessVersion, profile, holding, baseCommit, inside, from: from, fromWhy: fromWhy, fromBy: fromBy,
                    entryOrigin: continuedOrigin, ownEntry: continuedOrigin is null)
                .ConfigureAwait(false);

            return new SessionOpenOutcome(
                SessionOpenRefusal.None,
                $"Chat `{session.Id}` opened in `{known.Repository}`, via {adapter}.",
                session);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The record with the entry's key it answers (SESSONE1a, D167 point 3): the one stored, else the rule's over the facts held now.
    /// Never written back, so a record from before groups the day a reader asks, and the rule stays in <see cref="SessionEntries"/>.
    /// </summary>
    public async Task<Session> WithEntryAsync(Session session, CancellationToken ct = default) =>
        session.Entry is not null
            ? session
            : session with { Entry = await SessionEntries.KeyAsync(session, new StoreEntryFacts(quests, sessions), ct).ConfigureAwait(false) };

    /// <summary>Each record with the key it answers, as <see cref="WithEntryAsync"/> answers one.</summary>
    public async Task<IReadOnlyList<Session>> WithEntriesAsync(IEnumerable<Session> records, CancellationToken ct = default)
    {
        var answered = new List<Session>();
        foreach (var record in records) answered.Add(await WithEntryAsync(record, ct).ConfigureAwait(false));
        return answered;
    }

    /// <summary>The record of one session, as the store holds it: what a connector's tool judges the session it speaks for by.</summary>
    public Task<Session?> FindAsync(string sessionId, CancellationToken ct = default) => sessions.FindAsync(sessionId, ct);

    /// <summary>
    /// The records as the store lists them, live work first (<see cref="SessionStore.ListAsync"/>): what the channel reads to judge
    /// whether a part of an entry is still here before it says a message was never reached (COWORK1d).
    /// </summary>
    public Task<IReadOnlyList<Session>> ListAsync(
        string? repository = null, bool includeClosed = false, string? workspace = null, CancellationToken ct = default) =>
        sessions.ListAsync(repository, includeClosed, workspace, ct);

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
    public async Task<bool> MarkTookAsync(string sessionId, string questId, QuestTakeBind bind, CancellationToken ct = default) =>
        await MarkTookAsync(sessionId, questId, ct, bind switch
        {
            QuestTakeBind.Expired => "expired",
            QuestTakeBind.Replaced => "replaced",
            QuestTakeBind.Remote => "remote",
            _ => null,
        }).ConfigureAwait(false);

    /// <inheritdoc cref="MarkTookAsync(string, string, QuestTakeBind, CancellationToken)"/>
    public async Task<bool> MarkTookAsync(string sessionId, string questId, CancellationToken ct = default, string? bind = null)
    {
        var session = await sessions.FindAsync(sessionId, ct).ConfigureAwait(false);
        if (session is not { Active: true, Origin: null }) return false;

        var served = session.Quest is { } quest
            ? string.Equals(quest, questId.TrimStart('#'), StringComparison.OrdinalIgnoreCase)
            : session.Kind == SessionKind.Chat;
        // CHATTAKE1: a chat's mark names the quest it took, which is what puts that quest in the chat's line (design §1.2 rule 2).
        return served && await sessions.MarkTookAsync(
            sessionId, session.Quest is null ? questId.TrimStart('#') : null, ct, bind).ConfigureAwait(false);
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
    /// The driver's code for a session the person's reopen of the work its step grew from stopped (FABRIC-CLOSE1c2; its
    /// <c>NoteCodes.EndedSourceReopened</c>, a twin read here by its spelling): the one ending the ledger reads by a code, since
    /// the record's state alone, <c>completed</c>, says the work finished (FABRIC-CLOSE1c26).
    /// </summary>
    internal const string EndedSourceReopened = "ended.source-reopened";

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
    /// <param name="channel">The channel message the words are (SESSONE2e): kept on the word, so its door tells the ask nothing more.</param>
    /// <param name="from">
    /// Their author where that is not the person (COWORK1b, <see cref="SaidFrom"/>): Daoris's own words are kept as Daoris's, never
    /// read as the person's. An author this build does not know is refused before anything is kept.
    /// </param>
    /// <param name="key">
    /// The producer's key (COWORK1b, the message-urgency design §6): where a word with it already waits on the record, nothing more
    /// is kept and the answer is that word, so a producer's retry keeps one word. Once a session took it, the key is the producer's
    /// own to remember (D188's COWORK1b note).
    /// </param>
    /// <param name="level">When its sender asked it to reach a working session (COWORK1c, <see cref="SaidLevel"/>); one this build does not know is refused.</param>
    /// <param name="running">
    /// The driver that runs the session asks for its run (COWORK1c, the message-urgency design §6): a running record keeps the word,
    /// which that driver's one dispatcher hands its run and takes off once on the wire. Without it a running record refuses as before,
    /// since only that driver hands a running session its words.
    /// </param>
    public async Task<SessionSayOutcome> SayAsync(
        string id, string? text, IReadOnlyList<string>? files, DateTimeOffset now, CancellationToken ct = default, string? channel = null,
        string? from = null, string? key = null, string? level = null, bool running = false)
    {
        var words = text?.Trim() ?? "";
        if (words.Length == 0)
        {
            return new(SessionSayRefusal.Empty, "There are no words to keep: the person said nothing.", Session: null);
        }

        if (!SaidFrom.Known(from))
        {
            return new(
                SessionSayRefusal.From,
                $"Words from `{from}` are not kept: this service knows no such author, and kept as the person's they would be read as theirs.",
                Session: null);
        }

        if (!SaidLevel.Known(level))
        {
            return new(
                SessionSayRefusal.Level,
                $"Words at `{level}` are not kept: this service knows the levels {SaidLevel.Ordinary}, {SaidLevel.Urgent} and {SaidLevel.Now}.",
                Session: null);
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

            if (Never(session) is { } never) return never;

            var parked = session.State == SessionState.AwaitingPerson;
            // COWORK1c: a running record keeps words only for its own run, asked by the driver that runs it.
            var forRun = session.Active && !parked;
            if (forRun && !running)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Running,
                    $"Session `{id}` is {Spell(session.State)}: words to a running session reach it through the driver that "
                    + "runs it, not its record.",
                    Session: null);
            }

            // COWORK1b (trap 19): a word with this producer's key already waits, so this say is its producer's retry.
            if (key is not null && session.Said.FirstOrDefault(each => string.Equals(each.Key, key, StringComparison.Ordinal)) is { } waiting)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.None,
                    $"Already kept for session `{id}` under key `{key}`, as word `{waiting.Id}`: nothing more was kept.",
                    session, waiting) { Again = true };
            }

            // A word for a running record's run reopens nothing and answers no park, so no note is written and nothing travels.
            var word = new SaidWord(
                NewWordId(), words, now, Names(files), Reopens: !parked && !forRun, Channel: channel, From: from, Key: key, Level: level);
            var (note, parts) = parked ? AnsweredNote(session, words, from) : (null, null);
            var kept = await sessions
                .KeepSaidAsync(id, word, note, inside, noteParts: parts)
                .ConfigureAwait(false);
            return new SessionSayOutcome(SessionSayRefusal.None, forRun ? ForRunMessage(id, from) : KeptMessage(session, from), kept, word)
            {
                Answers = parked,
            };
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

    /// <summary>
    /// Mark the moment a session's dispatcher first wrote these waiting words to its harness (COWORK1c, the message-urgency design §6),
    /// before the wire: a shell that comes up again never sends a word with an attempt a second time, and says it may have reached the
    /// agent. Each word in its own statement, the first moment kept. A word no longer waiting is answered unmarked, never an error:
    /// it was taken, or taken back, meanwhile. No revision, since the words never travel.
    /// </summary>
    public async Task<SessionAttemptOutcome> MarkAttemptAsync(
        string id, IReadOnlyCollection<string> words, DateTimeOffset at, CancellationToken ct = default)
    {
        if (words.Count == 0)
        {
            return new(SessionSayRefusal.Empty, "Name the words attempted: none were named, so nothing was marked.", Session: null, []);
        }

        if (await sessions.FindAsync(id, ct).ConfigureAwait(false) is not { Origin: null })
        {
            return new(SessionSayRefusal.NotFound, $"No session `{id}` of this machine's.", Session: null, []);
        }

        var unmarked = new List<string>();
        foreach (var word in words)
        {
            if (!await sessions.MarkAttemptAsync(id, word, at, ct).ConfigureAwait(false)) unmarked.Add(word);
        }

        var session = await sessions.FindAsync(id, ct).ConfigureAwait(false);
        return new(
            SessionSayRefusal.None,
            unmarked.Count == 0
                ? $"Marked {words.Count} word(s) on session `{id}` as written to its harness."
                : $"Marked {words.Count - unmarked.Count} word(s) on session `{id}`; {string.Join(", ", unmarked.Select(word => $"`{word}`"))} no longer wait.",
            session, unmarked);
    }

    /// <summary>
    /// Take one of the words waiting on this machine's record back before a session took it, or reword it (SAIDBACK1a, D137
    /// §2.4): with no <paramref name="text"/> the word leaves the record; with one, the old word leaves and the new words are
    /// kept last, under a new id, said now, with the old word's files' names and its <c>reopens</c>. The new words are the
    /// person's whoever said the old. Read, judged and written as one step (REV3), so a refused reword leaves the old word as
    /// it was, and the driver taking the record up cannot read it between the two writes.
    /// </summary>
    /// <remarks>
    /// <para><b>Refused as <see cref="SessionSayRefusal.Taken"/></b>, an answer and never an error: a word no longer on the
    /// record, and any word of a record that runs again (queued, starting, working), whose run may already be building its
    /// prompt from it. A park takes its words back, as it takes them (§2.2). What never goes on refuses as the say door does,
    /// before anything is read of the words.</para>
    ///
    /// <para><b>Nothing is kept on the ask</b>, unlike <see cref="TakeSaidAsync"/>'s door: nothing took the words. A park's
    /// answer line stays on its note, which travelled already; a reworded park's answer adds its own line, as a second
    /// answer does. Order among several waiting words is not kept: the new words wait last.</para>
    /// </remarks>
    /// <returns>The record as it now stands, the word taken off it, and a reword's new word.</returns>
    public async Task<SessionSayOutcome> TakeBackAsync(
        string id, string? word, string? text, DateTimeOffset now, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return new(SessionSayRefusal.Empty, "Name the word to take back: none was named, so nothing was taken back.", Session: null);
        }

        var named = word.Trim();
        var words = text?.Trim();
        if (words is { Length: 0 })
        {
            return new(
                SessionSayRefusal.Empty,
                $"There are no words to put in the place of word `{named}`: it waits as it was. Take it back instead to leave none.",
                Session: null);
        }

        return await sessions.ExclusiveAsync(async inside =>
        {
            var session = await sessions.FindAsync(id, inside).ConfigureAwait(false);
            if (session is null)
            {
                return new SessionSayOutcome(SessionSayRefusal.NotFound, $"No session `{id}`.", Session: null);
            }

            if (Never(session) is { } never) return never;

            var parked = session.State == SessionState.AwaitingPerson;
            if (session.Active && !parked)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Taken,
                    $"Session `{id}` is {Spell(session.State)}: its run may already have taken word `{named}`, so it cannot be taken back.",
                    Session: null);
            }

            if (session.Said.FirstOrDefault(each => string.Equals(each.Id, named, StringComparison.Ordinal)) is not { } old)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.Taken,
                    $"Word `{named}` no longer waits on session `{id}`: it was taken, or taken back, already.",
                    Session: null);
            }

            var left = await sessions.TakeSaidAsync(id, [named], inside).ConfigureAwait(false);
            if (words is null)
            {
                return new SessionSayOutcome(
                    SessionSayRefusal.None,
                    $"Took word `{named}` back from session `{id}`"
                    + (left!.Said.Count == 0 ? ": no words wait on it now." : $": {left.Said.Count} still wait."),
                    left) { Taken = old };
            }

            var reworded = new SaidWord(NewWordId(), words, now, old.Files, old.Reopens);
            var (note, parts) = parked ? AnsweredNote(session, words) : (null, null);
            var kept = await sessions.KeepSaidAsync(id, reworded, note, inside, noteParts: parts).ConfigureAwait(false);
            return new SessionSayOutcome(
                SessionSayRefusal.None,
                $"Reworded word `{named}` on session `{id}`: the new words wait last, as `{reworded.Id}`.",
                kept, reworded) { Taken = old };
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// What never goes on (D137 §2.2, MSG1a), judged before anything is read of the words, for the say door and the take-back
    /// alike: a teammate's record, an intake, a second opinion's reviewer, a helper and a stand-down. Null for any other.
    /// </summary>
    private static SessionSayOutcome? Never(Session session)
    {
        var id = session.Id;
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

        // MIXAGENT2a-i: the helper's twin of the reviewer's refusal above: its answer comes back through its file.
        if (session.Serves is { } serves)
        {
            return new SessionSayOutcome(
                SessionSayRefusal.Helper,
                $"Session `{id}` is a helper for session `{serves}`: it takes one turn and no words, and is never resumed. "
                + "Ask another agent again, with your words, for a fresh one.",
                Session: null);
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

        return null;
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

    /// <summary>
    /// What the person is told their words will do, phrased once here for every door; Daoris's own words are never called theirs
    /// (COWORK1b).
    /// </summary>
    /// <summary>The say's sentence for words kept for a running session's run (COWORK1c).</summary>
    private static string ForRunMessage(string id, string? from) =>
        $"Kept for session `{id}`, which runs: {(from == SaidFrom.Daoris ? "Daoris's words wait" : "your words wait")} on its record until its run takes them.";

    private static string KeptMessage(Session session, string? from = null)
    {
        var whose = from == SaidFrom.Daoris ? "Daoris's words" : "your words";
        return session.State == SessionState.AwaitingPerson && session.Quest is { } quest
            ? $"Kept for session `{session.Id}`: it carries on with `#{quest}` at the driver's next look."
            : session.Kind == SessionKind.Chat
                // A conversation is taken up by its runner the moment it can open, never by the driver's look.
                ? $"Kept for session `{session.Id}`: the same conversation goes on with {whose} as it opens again."
                : $"Kept for session `{session.Id}`: the same session goes on with {whose} at the driver's next look.";
    }

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
    /// their own. Daoris's own words that answer a park (COWORK1b: a reopen, a go-on the person's press made) are a part by
    /// <c>daoris</c>, never the person's.
    /// </remarks>
    internal static (string Note, string Parts) AnsweredNote(Session parked, string said, string? from = null)
    {
        const string Asked = "It stopped to ask the person; its question is in its transcript.";
        var note = (parked.Note ?? Asked) + AnsweredLine(said);
        NoteLine[] answered = [NoteLine.Coded(LedgerNoteCodes.Answered, "Answered:"), NoteLine.Said(said, from ?? "person")];
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
    /// <param name="from">
    /// The session this one carries on from another agent's or another account's (MIXAGENT1a-iii), with <paramref name="fromWhy"/>
    /// and <paramref name="fromBy"/>; recorded as told, since the driver is the half that knows.
    /// </param>
    /// <param name="access">
    /// The access setup the start resolved (AUTONOMY1a, D184 point 1), each pause with its value; recorded as told, since the driver is
    /// the half that resolves it, and judged only as a short map of words (<see cref="SessionAccess.Judged"/>).
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="access"/> is not a short map of words; nothing is opened.</exception>
    public async Task<SessionOpenOutcome> OpenAsync(
        string questId, string adapter, DateTimeOffset now,
        string? harnessVersion = null, string? profile = null, string? tree = null,
        string? baseCommit = null, CancellationToken ct = default,
        string? from = null, string? fromWhy = null, string? fromBy = null,
        IReadOnlyDictionary<string, string>? access = null)
    {
        // The caller's mistake is said before anything is read or held.
        var setup = SessionAccess.Judged(access);
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
        // And a stand-down whose take lost, once the person brought its work forward (SYNCMERGE1b0, D178 point 3): the quest stands
        // taken by the fresh take that settled its conflict, made after the record began. Once, since the session carried on is the
        // newest record from then, and a later one is never older than the take. Read from the log; no record marks it.
        var forward = last is { State: SessionState.StoodDown }
            ? (await quests.ReadTakesAsync(quest.Id, ct).ConfigureAwait(false)).BroughtForwardBy(quests.Machine)
            : null;
        var brought = forward is not null && forward.At > last!.Created;
        // And a session the person's reopen of the work its step grew from stopped (FABRIC-CLOSE1c26, D173's CLOSE1c2 note): it ended
        // completed with its quest still taken, said by the driver's code alone, and their go-on carries it on in its tree.
        var carriesOn = last is { State: SessionState.Failed }
                or { State: SessionState.Completed, Answer: not null }
                or { State: SessionState.Stopped, Interrupted: true }
            || (last is { State: SessionState.Stopped } && tookHere)
            || (last is { State: SessionState.Completed } && NoteParts.Carries(last.NoteParts, EndedSourceReopened))
            || brought;

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
        if (carriesOn && await TakenElsewhereAsync(quest, last!, tookHere, brought, ct).ConfigureAwait(false) is { } elsewhere)
        {
            return new(SessionOpenRefusal.TakenElsewhere, elsewhere, Session: null);
        }

        // The same resolution as a chat's, through the same registry, for the same reason: two doors
        // onto one tree must agree about which tree that is.
        var holding = Trees.Normalize(tree) ?? Trees.Normalize(await RootOfAsync(quest.To, ct).ConfigureAwait(false));

        // SESSONE1a: the line of work the quest belongs to (D167 point 2), read before the lock is taken so it holds nothing while it asks.
        var origin = await SessionEntries.OriginOfQuestAsync(quest, new StoreEntryFacts(quests, sessions), ct).ConfigureAwait(false);

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
                    harnessVersion, profile, holding, baseCommit, inside, from: from, fromWhy: fromWhy, fromBy: fromBy,
                    entryOrigin: origin, access: setup)
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
                    : target == SessionState.AwaitingPerson && WaitedOnHelper(session)
                    ? await BackToPersonAsync(session, note, noteParts, now, inside).ConfigureAwait(false)
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
            // MIXAGENT2a-i: a helper takes one turn and is never resumed either.
            { Serves: { } serves } => $"Session `{id}` is a helper for session `{serves}` — a helper is never resumed, so it does not move.",
            { Said.Count: 0 } =>
                $"Session `{id}` is {Spell(session.State)} — a finished session goes on only with the person's words, and none wait for it.",
            _ => null,
        };
        if (refused is not null)
        {
            return new SessionAdvanceOutcome(SessionAdvanceRefusal.Terminal, refused, Session: null);
        }

        // Ask Daoris's holds its own workspace's one at a time (ASKSCOPE1a), as its start does: every workspace's runs in one room.
        var holder = IsHelp(session)
            ? await sessions.RunningInWorkspaceAsync(session.Repository, session.Workspace, parked: true, inside).ConfigureAwait(false)
            : await sessions.ActiveForAsync(session.Repository, session.Tree, inside).ConfigureAwait(false);
        if (holder is not null)
        {
            return new SessionAdvanceOutcome(SessionAdvanceRefusal.Busy, Busy(session.Repository, holder), Session: null);
        }

        var (went, parts) = WentOnNote(session, note, noteParts, now);
        var moved = await sessions
            .SetStateAsync(id, SessionState.Working, went, evidence, transcript, now, inside, forgive: true, noteParts: parts)
            .ConfigureAwait(false);

        var with = WentOnWith(session) switch
        {
            (Persons: false, Findings: true, _, _) => "another agent's findings",
            (Persons: true, Findings: true, _, _) => "the person's words and another agent's findings",
            (Persons: false, Findings: false, Helper: true, _) => "a helper's answer",
            (Persons: true, Findings: false, Helper: true, _) => "the person's words and a helper's answer",
            (Persons: false, Findings: false, Helper: false, Daoris: true) => "Daoris's words",
            _ => "the person's words",
        };
        return new SessionAdvanceOutcome(
            SessionAdvanceRefusal.None,
            $"Session `{id}` is working again: it goes on from {Spell(session.State)} with {with}.",
            moved);
    }

    /// <summary>
    /// The driver's code for a session that ended its turn on its own open helper ask (HELPERWAIT1; its <c>NoteCodes.EndedAwaitsHelper</c>,
    /// a twin read here by its spelling): completed, since it waits on that helper and not on the person.
    /// </summary>
    internal const string EndedAwaitsHelper = "ended.awaits-helper";

    /// <summary>Whether a finished record ended waiting on its own helper (HELPERWAIT1): completed, by that code.</summary>
    private static bool WaitedOnHelper(Session session) =>
        session.State == SessionState.Completed && NoteParts.Carries(session.NoteParts, EndedAwaitsHelper);

    /// <summary>
    /// The second move out of an ended state (HELPERWAIT1, D131's and D188's notes): a driven session of this machine's that ended waiting on
    /// its own helper parks for the person when that helper ended with no answer, as its turn's end would have parked it, its note the
    /// driver's (the reason, then the park's own line). Called inside <see cref="AdvanceAsync"/>'s step, the record already read.
    /// </summary>
    /// <remarks>
    /// <para><b>Only that ending, and only to a park.</b> The record is still the one conversation (D131 §3), its quest still taken by it,
    /// and nothing ran since it ended; the person's answer resumes that conversation as any park's does. A record words wait on goes on
    /// with them instead; a teammate's, an intake, a pass or a helper, or one with no quest, does not move; and its tree must be free,
    /// since a park holds it (D51).</para>
    ///
    /// <para><b>Its history stays</b> as a park's does: the note is the move's, and the evidence and the transcript are kept.</para>
    /// </remarks>
    private async Task<SessionAdvanceOutcome> BackToPersonAsync(
        Session session, string? note, string? noteParts, DateTimeOffset now, CancellationToken inside)
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

        if (await sessions.ActiveForAsync(session.Repository, session.Tree, inside).ConfigureAwait(false) is { } holder)
        {
            return new SessionAdvanceOutcome(SessionAdvanceRefusal.Busy, Busy(session.Repository, holder), Session: null);
        }

        var moved = await sessions
            .SetStateAsync(id, SessionState.AwaitingPerson, note, evidence: null, transcript: null, now, inside, clearSaid: true, noteParts: noteParts)
            .ConfigureAwait(false);
        return new SessionAdvanceOutcome(
            SessionAdvanceRefusal.None,
            $"Session `{id}` waits on the person again: the helper it waited on ended with no answer.",
            moved);
    }

    /// <summary>
    /// Whose words an ended record goes on with, read from each waiting word's <see cref="SaidWord.By"/> (XAGENT1e2, D155's
    /// XAGENT1e note): the person's, another agent's findings, or both. A record with none reads as the person's, as it did.
    /// </summary>
    /// <remarks>
    /// A helper's answer (MIXAGENT2a-ii, <see cref="SaidWord.Helper"/>) is its own third, never findings: alone, or beside the
    /// person's words. Where findings and a helper's answer wait together the line says the findings, as it always did. Daoris's own
    /// words (COWORK1b, <see cref="SaidWord.From"/>) alone are a fourth, never "your words"; beside another's, the other's line stands.
    /// </remarks>
    private static (bool Persons, bool Findings, bool Helper, bool Daoris) WentOnWith(Session session) =>
        (session.Said.Count == 0 || session.Said.Any(word => word.Persons),
         session.Said.Any(word => word.By is not null),
         session.Said.Any(word => word.Helper is not null),
         session.Said.Any(word => word.From == SaidFrom.Daoris));

    /// <summary>
    /// The note an ended record goes on with (MSG1a, D137 §2.3): what ended it, when it went on, and the note the move passed,
    /// each with its parts (LANG1a, the language design §4 row 66): the record's own, or its note carried whole; the ledger's
    /// line with its moment; the move's own, or its note carried whole.
    /// </summary>
    /// <remarks>
    /// <b>Whose words, from <c>by</c></b> (XAGENT1e2): the person's alone keep <see cref="LedgerNoteCodes.WentOn"/> as it was.
    /// Another agent's findings are never "your words", since the person said nothing; with the person's beside them the line
    /// says both, as the driver's opening line does. Each is a coded line with its moment (<see cref="LedgerNoteCodes.WentOnFindings"/>,
    /// <see cref="LedgerNoteCodes.WentOnBoth"/>) since the page words them (XAGENT1g); a note written before keeps its English.
    /// </remarks>
    internal static (string Note, string Parts) WentOnNote(Session session, string? note, string? noteParts, DateTimeOffset now)
    {
        var at = $"{now.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";
        var (line, code) = WentOnWith(session) switch
        {
            (Persons: false, Findings: true, _, _) => ($"Went on with another agent's findings at {at}.", LedgerNoteCodes.WentOnFindings),
            (Persons: true, Findings: true, _, _) => ($"Went on with your words and another agent's findings at {at}.", LedgerNoteCodes.WentOnBoth),
            (Persons: false, Findings: false, Helper: true, _) => ($"Went on with a helper's answer at {at}.", LedgerNoteCodes.WentOnHelper),
            (Persons: true, Findings: false, Helper: true, _) => ($"Went on with your words and a helper's answer at {at}.", LedgerNoteCodes.WentOnHelperBoth),
            (Persons: false, Findings: false, Helper: false, Daoris: true) => ($"Went on with Daoris's words at {at}.", LedgerNoteCodes.WentOnDaoris),
            _ => ($"Went on with your words at {at}.", LedgerNoteCodes.WentOn),
        };
        var went = string.Join("\n\n", new[] { session.Note, line, note }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var parts = NoteParts.After(
            string.IsNullOrWhiteSpace(session.Note) ? null : session.NoteParts, session.Note,
            [NoteLine.Coded(code, line, ("at", NoteParts.Moment(now)))],
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
    ///
    /// <para><b>A take the person's bring-forward made</b> (SYNCMERGE1b0, <paramref name="broughtForward"/>) is made after the session
    /// ended and marked by no session, and is this machine's own for the session it carries on: the person made it for that work.</para>
    /// </remarks>
    private async Task<string?> TakenElsewhereAsync(Quest quest, Session last, bool tookHere, bool broughtForward, CancellationToken ct)
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

        return !tookHere && !broughtForward && take.At > last.Updated
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

    /// <summary>
    /// The person's answers to the go-aheads a parked session asked are its answer (GOAHEAD2): the answer that leaves none of
    /// them waiting keeps the park's blank answer, as the answer door keeps one given no words (ANSWER1b), so the driver's
    /// next look goes on with that same session, its own conversation resumed and handed the go-aheads' answers after it
    /// (ANSWER1a, KNOWUSE1a's resumed prompt). One of its go-aheads still waiting keeps it parked, and is named.
    /// </summary>
    /// <remarks>
    /// <para><b>Only the sessions that asked this go-ahead</b>, and of those only this machine's driven parks: a session still
    /// working, or one that ended, is handed the answer at its next start as before, and an intake is answered through its
    /// ask.</para>
    ///
    /// <para><b>Once.</b> Only the person's first answer to a go-ahead can be the last a park waited on, so a changed answer
    /// wakes nothing. A park that already holds words goes on with them and is handed these answers beside them, so nothing
    /// is kept twice. Judged and kept under the store's lock, as an answer is (REV3), so the driver taking the park up and
    /// a second door never both read it waiting.</para>
    ///
    /// <para><b>The answers are not written as the person's words</b> (KNOWUSE1a2): the record keeps what the answer door
    /// keeps for none, and the go-aheads' answers reach the session beneath it, quoted as the person gave them.</para>
    /// </remarks>
    /// <param name="answered">What the ask's go-ahead door kept: the go-ahead as it now stands, and the ask with every other.</param>
    public async Task<GoAheadParks> GoOnWithGoAheadsAsync(GoAheadAnswerOutcome answered, DateTimeOffset now, CancellationToken ct = default)
    {
        if (answered is not { Refusal: GoAheadAnswerRefusal.None, WasWaiting: true, GoAhead: { } goAhead, Ask: { } ask })
        {
            return GoAheadParks.None;
        }

        var goesOn = new List<string>();
        var waits = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        foreach (var asker in goAhead.Asked.Select(request => request.Session).Distinct(StringComparer.Ordinal))
        {
            var open = ask.GoAheads
                .Where(held => held.Answer is null && held.Asked.Any(request => request.Session == asker))
                .Select(held => held.Number)
                .ToList();
            var parked = await sessions.ExclusiveAsync(async inside =>
            {
                var session = await sessions.FindAsync(asker, inside).ConfigureAwait(false);
                if (session is not { State: SessionState.AwaitingPerson, Origin: null, Quest: not null, Ask: null }) return false;
                if (open.Count == 0 && session.Said.Count == 0) await KeepAnswerAsync(session, CarryOn, now, inside).ConfigureAwait(false);
                return true;
            }, ct).ConfigureAwait(false);

            if (!parked) continue;
            if (open.Count > 0) waits[asker] = open;
            else goesOn.Add(asker);
        }

        return new(goesOn, waits);
    }

    /// <summary>Ask the person for a go-ahead for a session; see <see cref="SessionGoAheads.AskGoAheadAsync"/> (KNOWUSE1a).</summary>
    public Task<GoAheadOutcome> AskGoAheadAsync(
        string sessionId, string? kind, string? on, string? act, string? why, DateTimeOffset now, CancellationToken ct = default) =>
        goAheads.AskGoAheadAsync(sessionId, kind, on, act, why, now, ct);

    /// <summary>Keep the folder a set-up step's session asks to serve; see <see cref="SessionReviews.ServeAsync"/> (REVIEWENV1b).</summary>
    public Task<ReviewSayOutcome> ServeAsync(
        string? sessionId, string? folder, string? address, DateTimeOffset now, CancellationToken ct = default) =>
        reviews.ServeAsync(sessionId, folder, address, now, ct);

    /// <summary>Keep a set-up a set-up step's session says; see <see cref="SessionReviews.ReadyAsync"/> (REVIEWENV1b).</summary>
    public Task<ReviewSayOutcome> ReadyAsync(
        string? sessionId, string? look, string? shows, string? again, string? run, DateTimeOffset now, CancellationToken ct = default) =>
        reviews.ReadyAsync(sessionId, look, shows, again, run, now, ct);
}
