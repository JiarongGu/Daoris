using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The judgement half of the session system: when a session may open, and what may move where. The
/// store holds state; this decides — one implementation for every door, for the same reason
/// <see cref="QuestExchange"/> exists. The ledger is the root that wires: each responsibility is a type of its own
/// (<see cref="SessionOpening"/>, <see cref="SessionAdvances"/>, <see cref="SessionSays"/>, <see cref="SessionSaidMoves"/>,
/// <see cref="SessionAskWords"/>, <see cref="SessionKeeps"/>, <see cref="SessionDeletes"/>), and each door here is one line to it.
/// </summary>
/// <remarks>
/// <para><b>This ledger never writes quest state.</b> The spawned session claims its own quest through
/// its own connector, as the repository's own agent — which is what keeps driven and outside work
/// indistinguishable at the quest layer, and what keeps outside development first-class. The ledger
/// only reads the quest, to refuse starting work someone else already has.</para>
///
/// <para><b>One active session per working TREE</b>. Two agents in one working tree corrupt each
/// other's git state — that is the reason. The other half is that a
/// repository has as many trees as it has, so the lock keys on the tree rather than on the name of the
/// repository that owns it. A parked session still holds its tree: the person clearing it is the flow
/// control, not an inconvenience to route around.</para>
/// <para><b>An unstated tree resolves through the registry</b>, at both doors — which is what makes two
/// sessions in one tree collide by construction rather than by string comparison, and what keeps
/// behaviour identical for every caller that has not learned about trees yet.</para>
/// </remarks>
/// <param name="home">
/// This machine's home, where a park's question is filed (<see cref="QuestionBox"/>), or null on a host with none: a supervisor's word is
/// then refused <see cref="SessionSayRefusal.NoHome"/>, since its question cannot be checked.
/// </param>
/// <param name="opinions">
/// The second opinions, where findings an open moved to a new part are followed: the part that holds them answers them.
/// </param>
public sealed class SessionLedger(
    QuestStore quests, SessionStore sessions, KnowledgeService? registry = null, AskStore? asks = null, string? home = null,
    OpinionStore? opinions = null)
{
    private readonly SessionGoAheads goAheads = new(quests, sessions, asks);
    private readonly SessionReviews reviews = new(quests, sessions);
    private readonly SessionOpening opening = new(quests, sessions, registry, asks, opinions);
    private readonly SessionAdvances advances = new(quests, sessions, asks, new SessionHolders(quests, sessions, registry));
    private readonly SessionSays says = new(quests, sessions, asks, home);
    private readonly SessionSaidMoves saidMoves = new(quests, sessions);
    private readonly SessionAskWords askWords = new(quests, sessions, asks);
    private readonly SessionKeeps keeps = new(sessions);
    private readonly SessionDeletes deletes = new(quests, sessions, asks);

    /// <summary>The memory coordinator's log of record, where a session's connector stages its changes.</summary>
    public MemoryOpStore Memory => sessions.Memory;

    /// <summary>
    /// The "repository" an Ask Daoris session is recorded in. No repository can be
    /// called this — a colon is in no folder name — so it never collides with a registered one.
    /// </summary>
    /// <remarks>A twin (`.claude/knowledge/twins.md`): the driver's <c>HelpRoom.Repository</c> and the
    /// page's <c>HELP_REPOSITORY</c> spell it too, and each side's test holds the spelling.</remarks>
    public const string HelpRepository = "daoris:help";

    /// <summary>
    /// Whether a record is Ask Daoris's, by its kind of place: its "repository" is <see cref="HelpRepository"/>. What
    /// keeps it off every remote feed whatever its workspace.
    /// </summary>
    public static bool IsHelp(Session session) => string.Equals(session.Repository, HelpRepository, StringComparison.Ordinal);

    /// <summary>Ask Daoris's mode that reads and proposes, and what a help record from before the modes ran under.</summary>
    public const string HelpRestricted = "restricted";

    /// <summary>Ask Daoris's mode that acts on the machine itself.</summary>
    public const string HelpFull = "full";

    /// <summary>
    /// The access modes an Ask Daoris record's run may get, as the ledger spells them. A twin
    /// (`.claude/knowledge/twins.md`): the driver's <c>HelpModes</c> spells them too, and each side's test holds the spelling.
    /// </summary>
    public static IReadOnlyList<string> HelpModes { get; } = [HelpRestricted, HelpFull];

    /// <summary>The mode <paramref name="mode"/> names in any case, trimmed; null names restricted; any other is refused.</summary>
    /// <exception cref="ArgumentException">A mode the ledger does not know.</exception>
    internal static string HelpModeOf(string? mode) =>
        mode is null ? HelpRestricted
            : HelpModes.FirstOrDefault(each => string.Equals(each, mode.Trim(), StringComparison.OrdinalIgnoreCase))
              ?? throw new ArgumentException(
                  $"`{mode}` is no access mode of Ask Daoris's — it runs `{HelpRestricted}` or `{HelpFull}`.", nameof(mode));

    /// <summary>The most versions one record keeps: a session's starts get a few; more is no start's list.</summary>
    public const int MostResolved = 64;

    /// <summary>Open an intake for an ask; see <see cref="SessionOpening.OpenIntakeAsync"/>.</summary>
    public Task<SessionOpenOutcome> OpenIntakeAsync(
        string askId, string adapter, string room, DateTimeOffset now,
        string? doorVersion = null, string? profile = null, CancellationToken ct = default, string? actor = null) =>
        opening.OpenIntakeAsync(askId, adapter, room, now, doorVersion, profile, ct, actor);

    /// <summary>Open Ask Daoris's conversation; see <see cref="SessionOpening.OpenHelpAsync"/>.</summary>
    public Task<SessionOpenOutcome> OpenHelpAsync(
        string adapter, string room, DateTimeOffset now,
        string? doorVersion = null, string? profile = null, string? workspace = null, string? mode = null,
        CancellationToken ct = default, string? actor = null) =>
        opening.OpenHelpAsync(adapter, room, now, doorVersion, profile, workspace, mode, ct, actor);

    /// <summary>Open a chat in a repository; see <see cref="SessionOpening.OpenChatAsync"/>.</summary>
    public Task<SessionOpenOutcome> OpenChatAsync(
        string repository, string adapter, DateTimeOffset now,
        string? doorVersion = null, string? profile = null, string? tree = null,
        string? baseCommit = null, CancellationToken ct = default,
        string? from = null, string? fromWhy = null, string? fromBy = null, string? actor = null, string? opens = null,
        OpenMoves? moves = null) =>
        opening.OpenChatAsync(repository, adapter, now, doorVersion, profile, tree, baseCommit, ct, from, fromWhy, fromBy, actor, opens, moves);

    /// <summary>Queue a session for an open quest; see <see cref="SessionOpening.OpenAsync"/>.</summary>
    public Task<SessionOpenOutcome> OpenAsync(
        string questId, string adapter, DateTimeOffset now,
        string? doorVersion = null, string? profile = null, string? tree = null,
        string? baseCommit = null, CancellationToken ct = default,
        string? from = null, string? fromWhy = null, string? fromBy = null,
        IReadOnlyDictionary<string, string>? access = null, string? actor = null, string? opens = null, OpenMoves? moves = null) =>
        opening.OpenAsync(questId, adapter, now, doorVersion, profile, tree, baseCommit, ct, from, fromWhy, fromBy, access, actor, opens, moves);

    /// <summary>Record the access mode an Ask Daoris conversation goes on under (HELPMODE1a2); see <see cref="SessionKeeps.KeepHelpModeAsync"/>.</summary>
    /// <exception cref="ArgumentException">A mode the ledger does not know.</exception>
    public Task<Session?> KeepHelpModeAsync(string id, string mode, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mode);
        return keeps.KeepHelpModeAsync(id, HelpModeOf(mode), ct);
    }

    /// <summary>Record the access setup a resumed driven session now runs under (AUTONOMY1b2); see <see cref="SessionKeeps.KeepAccessAsync"/>.</summary>
    public Task<Session?> KeepAccessAsync(string id, IReadOnlyDictionary<string, string> access, CancellationToken ct = default) =>
        keeps.KeepAccessAsync(id, access, ct);

    /// <summary>Record a record's actor provenance; see <see cref="SessionKeeps.KeepActorAsync"/>.</summary>
    public Task<Session?> KeepActorAsync(string id, string? choice, JsonElement? asked, JsonElement? changed, JsonElement? ran,
        JsonElement? chosen = null, CancellationToken ct = default) =>
        keeps.KeepActorAsync(id, choice, asked, changed, ran, chosen, ct);

    /// <summary>Record the downloaded versions a start gets; see <see cref="SessionKeeps.KeepResolvedAsync"/>.</summary>
    public Task<Session?> KeepResolvedAsync(string id, IReadOnlyList<string> versions, CancellationToken ct = default) =>
        keeps.KeepResolvedAsync(id, versions, ct);

    /// <summary>The record with the entry's key it answers: the one stored, else the rule's (<see cref="SessionEntries"/>), never written back.</summary>
    public Task<Session> WithEntryAsync(Session session, CancellationToken ct = default) =>
        WithEntryAsync(session, new StoreEntryFacts(quests, sessions), ct);

    private static async Task<Session> WithEntryAsync(Session session, ISessionEntryFacts facts, CancellationToken ct) =>
        session.Entry is not null ? session : session with { Entry = await SessionEntries.KeyAsync(session, facts, ct).ConfigureAwait(false) };

    /// <summary>Each record with the key it answers, as <see cref="WithEntryAsync(Session, CancellationToken)"/> answers one.</summary>
    public Task<IReadOnlyList<Session>> WithEntriesAsync(IEnumerable<Session> records, CancellationToken ct = default) =>
        WithEntriesAsync(records, new StoreEntryFacts(quests, sessions), ct);

    /// <summary>Each record with the key it answers, its facts read for all of them at once (<see cref="ListedFacts"/>).</summary>
    public async Task<IReadOnlyList<Session>> WithEntriesAsync(IEnumerable<Session> records, ISessionEntryFacts facts, CancellationToken ct = default)
    {
        var answered = new List<Session>();
        foreach (var record in records) answered.Add(await WithEntryAsync(record, facts, ct).ConfigureAwait(false));
        return answered;
    }

    /// <summary>The record of one session, as the store holds it.</summary>
    public Task<Session?> FindAsync(string sessionId, CancellationToken ct = default) => sessions.FindAsync(sessionId, ct);

    /// <summary>The line <paramref name="named"/> is on, walked to its head.</summary>
    public Task<LineWalk> WalkLineAsync(Session named, CancellationToken ct = default) => Lines.WalkAsync(sessions, named, ct);

    /// <summary>
    /// The records as the store lists them, live work first (<see cref="SessionStore.ListAsync"/>): what the channel reads to judge
    /// whether a part of an entry is still here before it says a message is unreached.
    /// </summary>
    public Task<IReadOnlyList<Session>> ListAsync(
        string? repository = null, bool includeClosed = false, string? workspace = null, CancellationToken ct = default) =>
        sessions.ListAsync(repository, includeClosed, workspace, ct);

    /// <summary>
    /// The session's own connector took its quest: recorded on the session, so its end
    /// can tell "it holds the quest and stopped" from "somebody else had it". Only the session's own
    /// quest, only while it runs, and only this machine's record — anything else changes nothing.
    /// A chat has no quest of its own, so whatever it takes is the work it served.
    /// </summary>
    /// <remarks>
    /// <b>A chat's take is marked too</b>, or its record reads as serving no quest and a delete removes that work's record.
    /// Every reader that asks a take of a quest keys on the record's quest, which a chat's is not, so the mark
    /// changes no carry-on and no stand-down.
    /// </remarks>
    public async Task<bool> MarkTookAsync(string sessionId, string questId, CancellationToken ct = default)
    {
        var session = await sessions.FindAsync(sessionId, ct).ConfigureAwait(false);
        if (session is not { Active: true, Origin: null }) return false;

        var served = session.Quest is { } quest
            ? string.Equals(quest, questId.TrimStart('#'), StringComparison.OrdinalIgnoreCase)
            : session.Kind == SessionKind.Chat;
        // A chat's mark names the quest it took, which is what puts that quest in the chat's line.
        return served && await sessions.MarkTookAsync(
            sessionId, session.Quest is null ? questId.TrimStart('#') : null, ct).ConfigureAwait(false);
    }

    /// <summary>Keep what the person said to a parked or ended session for it to go on with; see <see cref="SessionSays.SayAsync"/>.</summary>
    public Task<SessionSayOutcome> SayAsync(
        string id, string? text, IReadOnlyList<string>? files, DateTimeOffset now, CancellationToken ct = default, string? channel = null,
        string? from = null, string? key = null, string? level = null, bool running = false,
        string? decision = null, string? question = null, string? choice = null, bool settled = false, bool ending = false) =>
        says.SayAsync(id, text, files, now, ct, channel, from, key, level, running, decision, question, choice, settled, ending);

    /// <summary>Put a park the driver took up back as it stood, less the supervisor's word; see <see cref="SessionSays.RestoreParkAsync"/>.</summary>
    public Task<SessionAdvanceOutcome> RestoreParkAsync(
        string id, IReadOnlyCollection<string> takeBack, string? note, string? noteParts, DateTimeOffset now, CancellationToken ct = default) =>
        says.RestoreParkAsync(id, takeBack, note, noteParts, now, ct);

    /// <summary>Take the person's words off the record once a session took them; see <see cref="SessionSaidMoves.TakeSaidAsync"/>.</summary>
    public Task<SessionTakeOutcome> TakeSaidAsync(
        string id, IReadOnlyCollection<string> ids, string? by, CancellationToken ct = default) =>
        saidMoves.TakeSaidAsync(id, ids, by, ct);

    /// <summary>Mark when a dispatcher first wrote words to its harness; see <see cref="SessionSaidMoves.MarkAttemptAsync"/>.</summary>
    public Task<SessionAttemptOutcome> MarkAttemptAsync(
        string id, IReadOnlyCollection<string> words, DateTimeOffset at, CancellationToken ct = default) =>
        saidMoves.MarkAttemptAsync(id, words, at, ct);

    /// <summary>Take a waiting word back, or reword it; see <see cref="SessionSaidMoves.TakeBackAsync"/>.</summary>
    public Task<SessionSayOutcome> TakeBackAsync(
        string id, string? word, string? text, DateTimeOffset now, CancellationToken ct = default) =>
        saidMoves.TakeBackAsync(id, word, text, now, ct);

    /// <summary>Move one of the person's words to the head of its line; see <see cref="SessionSaidMoves.MoveSaidAsync"/>.</summary>
    public Task<SessionSayOutcome> MoveSaidAsync(string id, string? word, string? to, DateTimeOffset now, CancellationToken ct = default) =>
        saidMoves.MoveSaidAsync(id, word, to, now, ct);

    /// <summary>Keep what the person said to a session on the ask its work is for; see <see cref="SessionAskWords.KeepOnAskAsync"/>.</summary>
    public Task<AskWordOutcome> KeepOnAskAsync(
        string sessionId, AskWordKind kind, string? words, DateTimeOffset now, CancellationToken ct = default) =>
        askWords.KeepOnAskAsync(sessionId, kind, words, now, ct);

    /// <summary>Move a session; see <see cref="SessionAdvances.AdvanceAsync"/>, which says what each argument asks.</summary>
    public Task<SessionAdvanceOutcome> AdvanceAsync(
        string id, string state, string? note, string? evidence, string? transcript,
        DateTimeOffset now, CancellationToken ct = default, bool interrupted = false, bool limit = false, string? noteParts = null,
        string? expected = null, bool unbindAsk = false, IReadOnlyCollection<string>? expect = null,
        IReadOnlyDictionary<string, MemoryTurns>? turns = null, IReadOnlyList<string>? holdsPlace = null) =>
        advances.AdvanceAsync(id, state, note, evidence, transcript, now, ct, interrupted, limit, noteParts, expected, unbindAsk, expect, turns, holdsPlace);

    /// <summary>Clear a parked line's mark: all at the person's press, or the <paramref name="paths"/> a discard took out; null where no record of this machine's has the id.</summary>
    public async Task<Session?> ReleasePlaceAsync(string id, IReadOnlyList<string>? paths = null, CancellationToken ct = default) =>
        await new SessionPlaceLease(sessions).ReleaseAsync(id, paths, ct).ConfigureAwait(false)
            ? await sessions.FindAsync(id, ct).ConfigureAwait(false)
            : null;

    /// <summary>Whether this session's record may be deleted; see <see cref="SessionDeletes.JudgeDeleteAsync"/>.</summary>
    public Task<SessionDeleteOutcome> JudgeDeleteAsync(string id, CancellationToken ct = default) => deletes.JudgeDeleteAsync(id, ct);

    /// <summary>Delete a conversation's record that served no quest; see <see cref="SessionDeletes.DeleteAsync"/>.</summary>
    public Task<SessionDeleteOutcome> DeleteAsync(string id, CancellationToken ct = default) => deletes.DeleteAsync(id, ct);

    /// <summary>Which of these records <see cref="DeleteAsync"/> would delete, by the same rule; see <see cref="SessionDeletes.DeletableAsync"/>.</summary>
    public Task<IReadOnlySet<string>> DeletableAsync(IEnumerable<Session> candidates, CancellationToken ct = default) =>
        deletes.DeletableAsync(candidates, ct);

    /// <summary>Go on with the parks whose go-aheads the person answered; see <see cref="SessionGoAheads.GoOnWithGoAheadsAsync"/>.</summary>
    public Task<GoAheadParks> GoOnWithGoAheadsAsync(GoAheadAnswerOutcome answered, DateTimeOffset now, CancellationToken ct = default) =>
        goAheads.GoOnWithGoAheadsAsync(answered, now, ct);

    /// <summary>Ask the person for a go-ahead for a session; see <see cref="SessionGoAheads.AskGoAheadAsync"/>.</summary>
    public Task<GoAheadOutcome> AskGoAheadAsync(
        string sessionId, string? kind, string? on, string? act, string? why, DateTimeOffset now, CancellationToken ct = default) =>
        goAheads.AskGoAheadAsync(sessionId, kind, on, act, why, now, ct);

    /// <summary>Keep the folder a set-up step's session asks to serve; see <see cref="SessionReviews.ServeAsync"/>.</summary>
    public Task<ReviewSayOutcome> ServeAsync(
        string? sessionId, string? folder, string? address, DateTimeOffset now, CancellationToken ct = default) =>
        reviews.ServeAsync(sessionId, folder, address, now, ct);

    /// <summary>Keep a set-up a set-up step's session says; see <see cref="SessionReviews.ReadyAsync"/>.</summary>
    public Task<ReviewSayOutcome> ReadyAsync(
        string? sessionId, string? look, string? shows, string? again, string? run, DateTimeOffset now, CancellationToken ct = default) =>
        reviews.ReadyAsync(sessionId, look, shows, again, run, now, ct);
}
