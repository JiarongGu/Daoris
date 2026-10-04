namespace Daoris.Driver;

/// <summary>A quest as the service answered it — enough to decide on, and enough to compose a target from.</summary>
public sealed record QuestView(string Id, string From, string To, string Title, string Body, string Status)
{
    /// <summary>
    /// What a list calls it, as the service answered (SESSUX1j): its publisher's short title, or the name the service read
    /// from its words. Null from a host before the field, which names it by its title.
    /// </summary>
    public string? Short { get; init; }

    /// <summary>The quest's name where one is shown (SESSUX1j): its short title, else its title.</summary>
    public string Name => Short is { Length: > 0 } named ? named : Title;

    /// <summary>Addresses the quest carries — a ticket, a page — handed to the session as given.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Files the quest carries, each with where this machine keeps it, or null when it does not.</summary>
    public IReadOnlyList<QuestFileView> Attachments { get; init; } = [];

    /// <summary>What closing this done will publish next (D65 §4) — the service's to do, never the driver's.</summary>
    public IReadOnlyList<QuestStepView> Then { get; init; } = [];

    /// <summary>The quest whose close published this one, when it is a step of a chain.</summary>
    public string? Parent { get; init; }

    /// <summary>
    /// The question its taker waits on (D79) — another quest, asked of the repository that knows — or
    /// null. A taken quest carrying one is its taker's to resume once the question closes.
    /// </summary>
    public string? Awaits { get; init; }

    /// <summary>
    /// The session whose connector published it (SESS1), as the machine that ran it spells it, or null: a person's publish,
    /// or a chain's step. How a question a session asked joins that session's work (PAUSE1a, <see cref="AskWork"/>).
    /// </summary>
    public string? PublishedBy { get; init; }

    /// <summary>What its close said — a done's note or a decline's reason. The answer a waiting session resumes with.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// The circle it is held in (D48), as the service answered it, or the default where a host answers none: which
    /// workspace's pass carries an abandon's decline of it (PAUSE1d, D132 §3.4 step 4).
    /// </summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    /// <summary>
    /// Whether the service would delete it (D95): its own reading, the one the drawer's Delete is shown by.
    /// Absent is false — a host from before the delete door offers none.
    /// </summary>
    public bool Deletable { get; init; }

    /// <summary>
    /// The lanes of <see cref="To"/> it addresses (D115 §2.2), as the service answered them; empty for a
    /// quest to the whole repository. Carried, and not yet planned on: the lane locks are DEV6's.
    /// </summary>
    public IReadOnlyList<string> Lanes { get; init; } = [];

    /// <summary>Whom it asks as a person reads it: `repository`, or `repository:lane+lane` (the service's `QuestAddress.Spell`).</summary>
    public string Address => Lanes.Count == 0 ? To : $"{To}:{string.Join('+', Lanes)}";

    /// <summary>
    /// What the person requires of it (DRIFT1c, D133 §3), each their words and its check, in the service's order — the
    /// numbers its done answers by (DRIFT1d). Empty for a quest that names none, and from a host before requirements.
    /// </summary>
    public IReadOnlyList<QuestRequirementView> Requirements { get; init; } = [];

    /// <summary>
    /// Whether a departure holds it for the person's yes (DRIFT1d, D133 §4): closed done departing from what they required,
    /// not yet accepted. The service lists it among the open, so a quest waiting on it waits. Absent is false.
    /// </summary>
    public bool Held { get; init; }

    /// <summary>
    /// How its done answered each requirement (DRIFT1d, D133 §4), by number, as the service answers them: what an accept
    /// shows the person before their yes (DRIFT1d2). Empty for a quest no done answered, and from a host before answers.
    /// </summary>
    public IReadOnlyList<QuestAnswerView> Answers { get; init; } = [];
}

/// <summary>One thing the person requires of a quest (DRIFT1c), as the service answers it: their words, and the check that proves them.</summary>
public sealed record QuestRequirementView(string Quote, string Check);

/// <summary>
/// How a done answered one requirement (DRIFT1d), as the service answers it: its number, and <paramref name="Met"/> with
/// how its check was met, or <paramref name="Departed"/> with the reason and <paramref name="Quote"/>, the person's words
/// it turns on.
/// </summary>
public sealed record QuestAnswerView(int Requirement, string? Met, string? Departed, string? Quote);

/// <summary>The session this machine last ran on a quest, and the tree it ran in (D79, D80).</summary>
/// <param name="Session">Its record's id.</param>
/// <param name="Tree">Where it ran — its own tree where the repository opted in, the root otherwise; null when unsaid.</param>
/// <param name="State">How its record ended — `failed` is a cut-off, which a later session carries on (D80).</param>
/// <param name="Note">What its record said about that ending — the words a session carrying on is told.</param>
/// <param name="Repository">Where it ran — whether a chain's next step can build on its tree (CHAIN2).</param>
/// <param name="Answer">The person's answer when it parked to ask them (STANDDOWN2) — what a carry-on is handed.</param>
/// <param name="Interrupted">
/// A `stopped` ending that was not the person's (D104): the orphan sweep's, or the driver's shutdown — a
/// cut-off, carried on like a `failed` one.
/// </param>
public sealed record PriorSession(
    string Session, string? Tree, string State = "", string? Note = null, string? Repository = null,
    string? Answer = null, bool Interrupted = false)
{
    /// <summary>
    /// Whether it ended because the person stopped it (D104): <c>stopped</c>, and not by the sweep or a shutdown. Such a stop
    /// holds its quest until the person releases it (SESSUX1b, D126 §3.3).
    /// </summary>
    public bool PersonStopped => string.Equals(State, "stopped", StringComparison.OrdinalIgnoreCase) && !Interrupted;

    /// <summary>
    /// The account it ran on, as its record names it, or null for the tool's own home (D49 §4): what a carry-on is
    /// compared with, to say whether it runs on another account (TOOL4f, D125 §3.5). Served on loopback alone, which is
    /// where this machine's driver reads its own records.
    /// </summary>
    public string? Profile { get; init; }

    /// <summary>Its record says an account's limit made its failure (TOOL4c): what a carry-on is told after one (TOOL4f).</summary>
    public bool Limit { get; init; }

    /// <summary>The adapter its record opened on (ANSWER1a, D131 §1): an answer resumes its conversation only on the same one.</summary>
    public string? Adapter { get; init; }

    /// <summary>The harness version its record opened on: a resumed run says so where the version moved since (D131 §1).</summary>
    public string? HarnessVersion { get; init; }

    /// <summary>
    /// The commit its tree stood at when it opened (SURF6): a resumed run's evidence is counted from it, so the review's
    /// range is the whole session's (ANSWER1a, D131 §3). Served on loopback, beside the tree.
    /// </summary>
    public string? BaseCommit { get; init; }

    /// <summary>
    /// A park the person answered, still parked (D131 §1): its own record goes on, and its harness conversation resumes
    /// where it can. An answer a service from before ANSWER1b took has already ended the record, and is a carry-on.
    /// </summary>
    public bool AnsweredPark => string.Equals(State, "awaiting-person", StringComparison.OrdinalIgnoreCase) && Answer is not null;

    /// <summary>
    /// The person's words waiting on its record for it to go on with (MSG1a, D137 §2.4), in the order said; null where the
    /// host answers no <c>said</c>, one from before MSG1a, and empty where nothing waits.
    /// </summary>
    public IReadOnlyList<SaidWordView>? Said { get; init; }

    /// <summary>The ask an intake answers (D65 §1b), or null for every other record: an intake never goes on (D137 §2.2).</summary>
    public string? Ask { get; init; }

    /// <summary>
    /// <c>driven</c> or <c>chat</c>, as the record says: an ended chat the person wrote to goes on through the chat runner
    /// (MSG1c), never the planner, which plans a quest's sessions.
    /// </summary>
    public string Kind { get; init; } = "driven";

    /// <summary>
    /// Its note's lines, each by its code with its values (LANG1a, D142 point 2): what a line the driver adds to its note carries
    /// on. Null for a record from before parts, whose note is then carried whole.
    /// </summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>Its note as composed: its parts, or its English whole as one part from before them.</summary>
    public Noted AsNoted() => Noted.From(Note, NoteParts);

    /// <summary>Waiting on the person (D83): <c>awaiting-person</c>, the one state a record goes on from without leaving an ended one.</summary>
    public bool Parked => string.Equals(State, "awaiting-person", StringComparison.OrdinalIgnoreCase);

    /// <summary>A record that came down from the team, keyed <c>origin/id</c> (D47 §6): never this machine's to go on with.</summary>
    public bool Teammate => Session.Contains('/');

    /// <summary>
    /// Whether the person's words wait for it to go on with (MSG1b, D137 §2.2): its <c>said</c> holds any, parked or ended;
    /// from a host before <c>said</c>, an answered park is the one case that waits.
    /// </summary>
    public bool WordsWaiting => Said is { } said ? said.Count > 0 : AnsweredPark;

    /// <summary>The words it goes on with, in order: each of <see cref="Said"/>, or a host's answer from before <c>said</c>.</summary>
    public IReadOnlyList<SaidWordView> Waiting =>
        Said is { } said ? said
        : Answer is { } answer ? [new SaidWordView(null, answer, DateTimeOffset.MinValue, [], false)]
        : [];
}

/// <summary>
/// One of the person's words waiting on a record (MSG1a, D137 §2.4), as the service answers it to this machine: its id,
/// which the record's events say again where the session took it, the words, when, its files' names, and whether it was
/// said after the record ended.
/// </summary>
/// <param name="Id">Its id on the record; null for an answer a host from before <c>said</c> kept, which has none.</param>
public sealed record SaidWordView(string? Id, string Text, DateTimeOffset At, IReadOnlyList<string> Files, bool Reopens);

/// <summary>One step of a chain, as the service answered it.</summary>
public sealed record QuestStepView(string To, string Title, string Body);

/// <summary>A file a quest carries, as the local service answered it.</summary>
/// <param name="Name">The file's own name.</param>
/// <param name="Sha256">Its content's hash.</param>
/// <param name="Bytes">Its size.</param>
/// <param name="Path">
/// Where THIS machine keeps it — told by the service, never derived here: the layout under the home is
/// the service's, and the driver's home is not always the service's (it is wherever `driver.json`
/// lives). Null is honest and common: a quest mirrored from another machine names files whose bytes
/// stayed where it was published.
/// </param>
public sealed record QuestFileView(string Name, string Sha256, long Bytes, string? Path);

/// <summary>A registration as the service answered it. The root is present only from a local service.</summary>
/// <param name="Workspace">
/// The circle this repository is wired into on THIS machine (D48 §2) — the registry row, which is the
/// only honest source for it. The toolchain reads it to pick a workspace's credential profile
/// (D49 §4): a work account for the work circle, a personal one at home.
/// </param>
public sealed record RepoView(
    string Repository, bool Adopted, string? Root, string Workspace = RemoteTarget.DefaultWorkspace);

public static class Repositories
{
    /// <summary>
    /// What the registry holds, as one string — equal when the same repositories stand in the same
    /// circles at the same roots, whatever the order. The shell forwards a tick to the page when this
    /// changes (FG4), as it does for <see cref="Asks.Signature"/>: a folder imported from a terminal
    /// moves nothing else a tick reports, and the page said *no workspace yet* until a reload.
    /// </summary>
    public static string Signature(IEnumerable<RepoView> registry) =>
        string.Join("\n", registry
            .Select(repo => $"{repo.Repository}\t{repo.Adopted}\t{repo.Workspace}\t{repo.Root}")
            .OrderBy(line => line, StringComparer.Ordinal));
}

/// <summary>An ACTIVE session as the service answered it — closed ones never reach the planner.</summary>
/// <param name="State">
/// Where the record stands. The planner has never needed it — "is this repository busy" is the only
/// question it asks — but <see cref="AttentionWatch"/> does, because a park is a state change nothing
/// local performs and is therefore only visible by looking (SURF5b).
/// </param>
/// <param name="Note">What the session said about that state, where it said anything.</param>
public sealed record SessionView(
    string Id, string Repository, string State = "", string? Note = null)
{
    /// <summary>
    /// The ask an INTAKE answers (D65 §1b), or null for every other session. Its "repository" is
    /// <c>ask #id</c>, which no quest names — so the planner never finds it busy with anything.
    /// </summary>
    public string? Ask { get; init; }

    /// <summary>
    /// The tree it holds (D51), or null when unsaid — where a repository opens a tree per session, the
    /// tree is the lock the planner asks about (PAR1).
    /// </summary>
    public string? Tree { get; init; }

    /// <summary>
    /// The quest it serves, or null for a conversation's or an intake's. A session outlives the look that
    /// started it (DEV3), so the next look can find its quest still open, and must not start it again.
    /// </summary>
    public string? Quest { get; init; }

    /// <summary>Its note's lines by code (LANG1a), handed on wherever its note is; null for a record from before parts.</summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }
}

/// <summary>
/// One of this machine's session records as the goal's walk counts it (TOOL6b, D130 §4.2, §16.2): the adapter it ran on,
/// the account (null for the tool's own sign-in), when it was opened, and whether it runs now. Read on loopback alone,
/// where the record names its account (D47 §4); a teammate's record is not this machine's.
/// </summary>
public sealed record SessionStarted(string Adapter, string? Profile, DateTimeOffset? Created, bool Running);

public static class ActiveSessions
{
    /// <summary>
    /// Which sessions are active and where each stands, as one string — equal when the same ones stand
    /// in the same states, whatever the order. The shell forwards a tick to the page when this changes
    /// (UX5 U13), as it does for <see cref="Asks.Signature"/>: a conversation started or ended from the
    /// main window moves nothing else a tick reports, so the other windows never heard of it.
    /// </summary>
    public static string Signature(IEnumerable<SessionView> sessions) =>
        string.Join("\n", sessions
            .Select(session => $"{session.Id}\t{session.State}")
            .OrderBy(line => line, StringComparer.Ordinal));
}

/// <summary>Everything a tick's decisions are made from, fetched once so the plan is coherent.</summary>
/// <param name="Strikes">
/// How many sessions have <b>failed</b> on each quest, by quest id — <b>derived</b> from the session
/// records this machine already wrote, never a tally the driver keeps (DRV6). Only `failed` counts, and a
/// stop that was not the person's (D104): a stand-down means somebody else got there first, a decline is
/// a real answer, and the person's stop was theirs. A failure an account's limit made is not counted
/// either (D125 §5.2): the account cools, and the quest waits for it. A quest nobody has failed is simply absent.
/// </param>
public sealed record Snapshot(
    IReadOnlyList<QuestView> Quests,
    IReadOnlyList<RepoView> Repositories,
    IReadOnlyList<SessionView> Active,
    IReadOnlyDictionary<string, int>? Strikes = null)
{
    public IReadOnlyDictionary<string, int> Strikes { get; init; } =
        Strikes ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The session THIS machine last ran on each quest, by quest id — derived from the same records as
    /// the strikes, never kept (D79). It is how a waiting quest is known to be this machine's to resume,
    /// and where: a taken quest no session here ran is somebody else's take.
    /// </summary>
    public IReadOnlyDictionary<string, PriorSession> LastRun { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every record this machine keeps, as the goal's walk counts them (TOOL6b): which account each ran on, when, and
    /// whether it runs now — derived from the same records, never kept. The roster reads it at each look.
    /// </summary>
    public IReadOnlyList<SessionStarted> Started { get; init; } = [];

    /// <summary>
    /// The quests of every paused work on this machine, each against the pause that holds it (PAUSE1b, D132 point 3): the
    /// look computes it from <c>driver.json</c>'s pauses and <see cref="AskWork"/> (<see cref="PausedWork.LookAsync"/>), as it
    /// derives <see cref="LastRun"/>, so the planner never holds a second copy of the work's rule. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, PausedBy> Paused { get; init; } =
        new Dictionary<string, PausedBy>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The closed quests whose last session here has the person's words waiting (MSG1b, D137 §2.2): no open list holds them,
    /// and the session goes on all the same, its quest staying as it closed. Absent is none.
    /// </summary>
    public IReadOnlyList<QuestView> Closed { get; init; } = [];

    /// <summary>
    /// The marks of words a record could not go on with, by session (<see cref="GoOnMarks"/>): read at each look, so the
    /// planner leaves those words waiting instead of trying them again. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, GoOnMark> Unable { get; init; } =
        new Dictionary<string, GoOnMark>(StringComparer.OrdinalIgnoreCase);
}

public enum StartVerdict
{
    /// <summary>Start a session for this quest, now.</summary>
    Start,

    /// <summary>The receiver has not opted into driving on this machine — the person's choice (D46 §2).</summary>
    NotDrivable,

    /// <summary>The person holds this repository: drivable, and nothing new starts there until they resume it (D46 §3).</summary>
    Held,

    /// <summary>
    /// Nothing here could answer it: the receiver is not registered on this machine, or it has not
    /// adopted and either has no root or would be carried by the pipe door, which has no connector to
    /// hand it (D70). The name predates D70, and the reason says which.
    /// </summary>
    NotAdopted,

    /// <summary>No filesystem root is known — nowhere to spawn. `connect` from the repository fixes it.</summary>
    NoRoot,

    /// <summary>An active session (or an older quest this tick) holds the repository, or the quest itself (DEV3).</summary>
    RepositoryBusy,

    /// <summary>The concurrency cap is spent.</summary>
    AtCapacity,

    /// <summary>
    /// Enough sessions have failed on this quest that trying again is spending an account rather than
    /// making progress (DRV6). The person restarts it deliberately.
    /// </summary>
    Exhausted,

    /// <summary>
    /// Planned to start, and held at SPAWN by what only the spawn can see — a dirty tree, an absent
    /// or logged-out harness, a tree that would not grow, a trust flag never given. The reason is the
    /// hold's own sentence, which names the fix. Never produced by the planner: it is what a tick
    /// reports when what happened differs from what was decided, so "sitting" still says why.
    /// </summary>
    Blocked,

    /// <summary>
    /// Taken, and waiting on a question asked of another repository (D79). It resumes, in the same
    /// tree, once that quest is answered — nothing for the person to do.
    /// </summary>
    Waiting,

    /// <summary>
    /// Held by the person's stop (SESSUX1b, D126 §3.3): its last session here ended because the person stopped it, open or
    /// taken, and nothing starts it on this machine until they release that stop with *Try again*
    /// (<see cref="DriverConfig.Released"/>). A stop is not a strike (D58), and another machine may still take an open one.
    /// </summary>
    Stopped,

    /// <summary>
    /// In the work of an ask or a quest the person paused on this machine (PAUSE1b, D132 points 2–3): nothing of it starts
    /// here until *Resume*, which releases the stops the pause made. The reason the quest sits before every other, a
    /// person's stop included, so a release of a stop starts nothing while it holds. A pause is this machine's: another
    /// machine may still take an open one, and a taken one stays taken here, its take, tree and strikes kept.
    /// </summary>
    Paused,
}

/// <param name="Quest">The quest considered.</param>
/// <param name="Verdict"><see cref="StartVerdict.Start"/>, or why not.</param>
/// <param name="Reason">The sentence a person reads. "Sitting" must always say why (D46 §3).</param>
/// <param name="Root">Where a start would spawn — carried so the executor never re-derives it.</param>
/// <param name="Workspace">The receiver's circle, carried for the same reason — and read by the toolchain.</param>
public sealed record Consideration(
    QuestView Quest, StartVerdict Verdict, string Reason, string? Root = null, string? Workspace = null)
{
    /// <summary>
    /// For a start that RESUMES a waiting quest (D79): the session that asked and waited, whose tree
    /// the resume carries on in. Null for every first start.
    /// </summary>
    public PriorSession? Resumes { get; init; }

    /// <summary>
    /// For a chain's next step in the same repository (CHAIN2): the step before's last run, whose
    /// branch this step's tree grows from, so it sees the unmerged work it builds on or checks.
    /// </summary>
    public PriorSession? BuildsOn { get; init; }

    /// <summary>
    /// For a quest the person's stop holds (<see cref="StartVerdict.Stopped"/>): the session they stopped, which *Try
    /// again* names to release it (SESSUX1b). Null for every other verdict.
    /// </summary>
    public PriorSession? HeldBy { get; init; }

    /// <summary>
    /// For a quest a pause holds (<see cref="StartVerdict.Paused"/>): whose pause, an ask's or the quest's own, which
    /// *Resume* names (PAUSE1b), so the page says the sentence from facts as it says a stop's from <see cref="HeldBy"/>.
    /// Null for every other verdict.
    /// </summary>
    public PausedBy? PausedBy { get; init; }

    /// <summary>
    /// For a start the person's words make (MSG1b, D137 §2.2): <see cref="Resumes"/> is the record they wait on, parked or
    /// ended, which goes on in its own conversation where it can, and else hands them on.
    /// </summary>
    public bool GoesOn { get; init; }

    /// <summary>
    /// For a start held at spawn (<see cref="StartVerdict.Blocked"/>): the accounts it passed not signed in (TOOL6g), whether
    /// it then waited on a cooling one or on none, so the page says each and its sign-in from facts. Null for every other.
    /// </summary>
    public SignedOutAccounts? SignedOut { get; init; }
}

public static class Considerations
{
    /// <summary>
    /// What a set of considerations SAYS, as one string — equal when the same quests carry the same
    /// verdicts and reasons, whatever the order. The shell forwards a tick to the page when this
    /// changes: a page that shows why each quest is sitting needs the change, and nothing else,
    /// because every tick it receives refetches four queries.
    /// </summary>
    public static string Signature(IEnumerable<Consideration> considered) =>
        string.Join("\n", considered
            .Select(c => $"{c.Quest.Id}\t{c.Verdict}\t{c.Reason}")
            .OrderBy(line => line, StringComparer.Ordinal));

    /// <summary>
    /// The plan as it turned out: every start that was held at spawn becomes
    /// <see cref="StartVerdict.Blocked"/> with the hold's own sentence, and everything else is the
    /// plan's word. The plan is not edited — what was decided and what happened are two records.
    /// </summary>
    /// <remarks>
    /// 🔴 Found on the deployed application: the quest it held every tick — trust flag never given —
    /// read <c>Start</c> in every consideration, because the hold lived only in the event line. The
    /// Overview, asked to say why each quest sits, had nothing to say under the one that mattered.
    /// </remarks>
    /// <param name="signedOut">The accounts each held start passed not signed in (TOOL6g), carried beside its hold.</param>
    public static IReadOnlyList<Consideration> Blocked(
        IReadOnlyList<Consideration> plan, IReadOnlyDictionary<string, string> heldAt,
        IReadOnlyDictionary<string, SignedOutAccounts>? signedOut = null) =>
        plan.Select(c =>
                c.Verdict == StartVerdict.Start && heldAt.TryGetValue(c.Quest.Id, out var why)
                    ? c with { Verdict = StartVerdict.Blocked, Reason = why, SignedOut = signedOut?.GetValueOrDefault(c.Quest.Id) }
                    : c)
            .ToList();
}

/// <summary>
/// The decision half of a tick: which open quests start, and why every other one is sitting.
/// </summary>
/// <remarks>
/// <para><b>Pure, deliberately.</b> Plan and apply are separate functions — the CLI's own convention,
/// held here for the same reason: a decision that can be asserted without spawning anything stays
/// tested, and a printed plan is a driver that can explain itself.</para>
///
/// <para><b>The plan never writes quest state and never outranks the ledger.</b> The service re-judges
/// every open through <c>SessionLedger</c>; this planner exists to avoid asking for work the ledger
/// would refuse, and to give every refusal a sentence before it happens.</para>
/// </remarks>
public static class Planner
{
    /// <param name="door">
    /// Which door this machine's starts ride (D53): the configured adapter's wire. It decides whether a
    /// repository that registered without adopting can be driven (D70). Defaults to the pipe, the door
    /// with the stricter requirement, so a caller that does not know cannot start more than it should.
    /// </param>
    public static IReadOnlyList<Consideration> Plan(
        Snapshot snapshot, DriverConfig config, SessionWire door = SessionWire.Pipe)
    {
        var considerations = new List<Consideration>();
        // 🔴 Grouped, never keyed straight off the list: two active sessions in one repository is a
        // state D51 allows (a conversation in the checkout, another in a tree of its own), and a
        // dictionary built from the list threw on the second — every tick failed while both were open.
        // Either blocks the repository; the first is the one the reason names.
        var blockedBy = snapshot.Active
            .GroupBy(s => s.Repository, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
        var startedThisTick = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var slots = config.Cap - snapshot.Active.Count;

        // The service already orders open-oldest-first; keeping its order is what makes "oldest starts
        // first" one implementation rather than two that drift. 🔴 But a record the person wrote to goes first (MSG1b, D137
        // §2.2): a reopen goes before every start the driver planned itself, so the quests whose last session here has words
        // waiting are planned ahead of the rest, the open list's in its order and then the closed ones no list holds.
        var writtenTo = snapshot.Quests.Concat(snapshot.Closed)
            .Where(WrittenTo)
            .DistinctBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var quest in writtenTo.Concat(snapshot.Quests.Where(quest => !WrittenTo(quest))))
        {
            // 🔴 A pause holds every quest of its work this planner would plan (PAUSE1b, D132 point 3), before every other reason,
            // the person's stop included: Resume is the one press that moves it, so a released stop starts nothing meanwhile.
            // First, too, so a paused quest spends no slot and no repository's turn, and the next starts where it would have.
            // Words written to its session wait for Resume too (D137 §2.2).
            if (snapshot.Paused.TryGetValue(quest.Id, out var pause) && Plans(quest))
            {
                considerations.Add(Paused(quest, pause));
                continue;
            }

            // The person wrote to its last session here, parked or ended (MSG1b, D137 §2.2): that record goes on with their
            // words. Before the person's stop, which the words release as Try again does: the same session goes on with them.
            if (WrittenTo(quest))
            {
                considerations.Add(GoOn(quest, snapshot.LastRun[quest.Id]));
                continue;
            }

            // 🔴 A person's stop holds its quest, open or taken, until they release that stop (SESSUX1b, D126 §3.3). Before
            // everything else, since it is the one reason the quest sits that the person must act on: a taken one was never
            // looked at again and sat with no sentence, and an open one was planned again at the next look (M3).
            if (quest.Status is "Open" or "Taken"
                && snapshot.LastRun.TryGetValue(quest.Id, out var last)
                && last.PersonStopped
                && !config.Releases(quest.Id, last.Session))
            {
                considerations.Add(Stopped(quest, last));
                continue;
            }

            if (quest.Status == "Open")
            {
                var considered = Consider(quest);
                // A chain's next step in the repository its parent ran in builds on that run (CHAIN2).
                considerations.Add(considered.Verdict == StartVerdict.Start
                                   && quest.Parent is { } parent
                                   && snapshot.LastRun.TryGetValue(parent, out var before)
                                   && string.Equals(before.Repository, quest.To, StringComparison.OrdinalIgnoreCase)
                    ? considered with { BuildsOn = before }
                    : considered);
            }
            else if (quest is { Status: "Taken", Awaits: { Length: > 0 } awaits }
                     && snapshot.LastRun.TryGetValue(quest.Id, out var prior))
            {
                considerations.Add(Resume(quest, awaits, prior));
            }
            else if (quest is { Status: "Taken", Awaits: null or "" }
                     && snapshot.LastRun.TryGetValue(quest.Id, out var cutOff)
                     && (string.Equals(cutOff.State, "failed", StringComparison.OrdinalIgnoreCase)
                         // A stop that was not the person's — the sweep's or a shutdown's (D104).
                         || cutOff is { State: "stopped", Interrupted: true }
                         // The person's own stop, once they released it (SESSUX1b): a held one never reaches here.
                         || cutOff.PersonStopped
                         // The person answered a session that parked to ask them (STANDDOWN2).
                         || cutOff is { State: "completed", Answer: not null }))
            {
                considerations.Add(CarryOn(quest, cutOff));
            }
        }

        return considerations;

        // Whether the loop below says anything of this quest: every open one, and a taken one whose last session here is
        // a stop, a cut-off, an answered park or the asker of a question (the branches below). A take this machine never ran,
        // and one whose session runs or waits on the person, is not planned, so a pause says nothing of it either.
        bool Plans(QuestView quest) =>
            quest.Status == "Open"
            || WrittenTo(quest)
            || (quest.Status == "Taken"
                && snapshot.LastRun.TryGetValue(quest.Id, out var run)
                && (quest.Awaits is { Length: > 0 }
                    || run.AnsweredPark
                    || string.Equals(run.State, "failed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(run.State, "stopped", StringComparison.OrdinalIgnoreCase)
                    || run is { State: "completed", Answer: not null }));

        // Whether the person's words wait on this quest's last session here (MSG1b, D137 §2.2), parked or ended, and are not
        // all words a reopen already found it could not take (MSG1a's open point). A quest waiting on a question waits with
        // them: D79's resume is its next start, and the words stay on the record they were said to.
        bool WrittenTo(QuestView quest) =>
            quest.Awaits is null or ""
            && snapshot.LastRun.TryGetValue(quest.Id, out var written)
            && written.WordsWaiting
            && !GoOnMarks.Judged(written, snapshot.Unable.GetValueOrDefault(written.Session));

        // Words going on in the record they were said to (MSG1b, D137 §2.2), `continuing` it: a park is still an active record
        // holding its tree, its quest and its slot, which it is never busy with itself; an ended record holds nothing, so it
        // takes a slot, ahead of every start planned after it. The sentence names whose words: a park's answer, as ANSWER1a
        // said it, or what the person wrote to a session that had ended.
        Consideration GoOn(QuestView quest, PriorSession written)
        {
            var considered = Consider(quest, into: written, continuing: true);
            if (considered.Verdict != StartVerdict.Start) return considered;

            var waiting = written.Waiting;
            var said = waiting.Count == 1 ? waiting[0].Text : $"{waiting[0].Text} (and {waiting.Count - 1} more)";
            return considered with
            {
                Reason = written.Parked
                    ? $"carrying on in `{quest.To}` — you answered session `{written.Session}`: {written.Answer ?? said}"
                    : $"going on in `{quest.To}` — you wrote to session `{written.Session}`: {said}",
                Resumes = written,
                GoesOn = true,
            };
        }

        // Held by a pause (PAUSE1b, design §2.3): the sentence names the pause, what Resume does and the terminal's door. It
        // says paused, never held, since a hold is a repository's and stops nothing that runs (design §8.1).
        static Consideration Paused(QuestView quest, PausedBy pause)
        {
            var resumes = quest.Status == "Taken" ? "carries it on" : "starts it";
            return new(quest, StartVerdict.Paused, pause.Scope == WorkScope.Ask
                ? $"paused with ask `#{pause.Id}`; Resume {resumes} — `daoris-driver ask --resume {pause.Id}`."
                : $"you paused `#{pause.Id}`; Resume {resumes} — `daoris-driver quest resume {pause.Id}`.")
            {
                PausedBy = pause,
            };
        }

        // Held by the person's stop (SESSUX1b, D126 §3.3): the sentence says whose stop, what Try again does, and the
        // terminal's door, which names the session since `daoris driver` cannot see this verdict (D50).
        static Consideration Stopped(QuestView quest, PriorSession stop) =>
            new(quest, StartVerdict.Stopped,
                $"you stopped session `{stop.Session}`; Try again {(quest.Status == "Taken" ? "carries it on" : "starts it again")} — "
                + $"`daoris driver retry {quest.Id} --session {stop.Session}`.")
            {
                HeldBy = stop,
            };

        // A cut-off (D80): this machine's session took the quest and failed before closing it — timed
        // out, refused, crashed, or ended by the sweep or a shutdown (D104) — so the take is still here
        // and the work is in its tree. Carried on like a failed start is retried: the strikes count every
        // cut-off, and the third parks it — bar one an account's limit made, which is no strike: its carry-on is
        // planned as any is, and held at spawn while the account cools (TOOL4d, D125 §4).
        // A record the person's words wait on, an answered park among them, is planned by GoOn above.
        Consideration CarryOn(QuestView quest, PriorSession cutOff)
        {
            var considered = Consider(quest, into: cutOff);
            return considered.Verdict == StartVerdict.Start
                ? considered with
                {
                    Reason = cutOff.Answer is { } answer
                        ? $"carrying on in `{quest.To}` — you answered session `{cutOff.Session}`: {answer}"
                        : cutOff.PersonStopped
                            ? $"carrying on in `{quest.To}` — you stopped session `{cutOff.Session}`, and released it."
                            : $"carrying on in `{quest.To}` — session `{cutOff.Session}` was cut off: "
                              + (cutOff.Note is { Length: > 0 } note ? note : "it ended before closing the quest."),
                    Resumes = cutOff,
                }
                : considered;
        }

        // A waiting quest (D79): the open list holds open and taken quests only, so a question still in
        // it is unanswered, and one absent from it has closed. A resume is a start in every other way —
        // the person's hold, the strikes, busy and the cap all still stand between it and a spawn.
        Consideration Resume(QuestView quest, string awaits, PriorSession prior)
        {
            var question = snapshot.Quests.FirstOrDefault(q =>
                string.Equals(q.Id, awaits, StringComparison.OrdinalIgnoreCase));
            // A question closed done departing from what the person required stays on the open list, held for their yes
            // (DRIFT1d, D133 §4), and what waits on it waits with it: the sentence names whose move it is, and its door.
            if (question is { Held: true })
            {
                return new(quest, StartVerdict.Waiting,
                    $"waits on `#{question.Id}`, which `{question.To}` closed done departing from what you required — it "
                    + $"resumes, in the same tree, once you accept that, your yes: `daoris-driver quest accept {question.Id}`.");
            }

            if (question is not null)
            {
                return new(quest, StartVerdict.Waiting,
                    $"waits on `#{question.Id}`, asked of `{question.To}` — it resumes, in the same tree, "
                    + "once that is answered.");
            }

            var considered = Consider(quest, into: prior);
            return considered.Verdict == StartVerdict.Start
                ? considered with { Reason = $"resuming in `{quest.To}` — `#{awaits}` is answered.", Resumes = prior }
                : considered;
        }

        // `into`: the earlier session whose tree a resume or a carry-on goes back into — the one tree a
        // live session there would hold (PAR1). Null for a first start, which grows a tree of its own.
        // `continuing`: `into` is the record the person's words wait on, which goes on itself (ANSWER1a; MSG1b), so it holds
        // nothing against itself.
        Consideration Consider(QuestView quest, PriorSession? into = null, bool continuing = false)
        {
            bool Itself(SessionView session) => continuing && string.Equals(session.Id, into?.Session, StringComparison.OrdinalIgnoreCase);

            var repo = snapshot.Repositories.FirstOrDefault(r =>
                string.Equals(r.Repository, quest.To, StringComparison.OrdinalIgnoreCase));

            if (repo is null)
            {
                return new(quest, StartVerdict.NotAdopted,
                    $"`{quest.To}` is not registered on this machine, so there is nowhere to start it.");
            }

            // Registered is drivable over the protocol door; adopted is disciplined (D70). The pipe
            // door's session reaches the knowledge tools only through the repository's own `.mcp.json`,
            // which adoption writes and the driver may never write for it (D32) — so there it sits.
            if (!repo.Adopted)
            {
                if (repo.Root is null)
                {
                    return new(quest, StartVerdict.NotAdopted,
                        $"`{quest.To}` has not adopted Daoris and no root is known for it on this machine, "
                        + "so there is nothing to start and nothing there to see the quest.");
                }

                if (door != SessionWire.Acp)
                {
                    return new(quest, StartVerdict.NotAdopted,
                        $"`{quest.To}` has not adopted Daoris, so a session there would have no connector to "
                        + "take the quest with — only the protocol door hands it one. Drive with an ACP "
                        + "agent, or adopt it.");
                }
            }

            if (!config.Drivable.Contains(quest.To, StringComparer.OrdinalIgnoreCase))
            {
                return new(quest, StartVerdict.NotDrivable,
                    $"`{quest.To}` has not been opted into driving on this machine.");
            }

            if (config.Holds.Contains(quest.To, StringComparer.OrdinalIgnoreCase))
            {
                return new(quest, StartVerdict.Held, $"`{quest.To}` is held by the person.");
            }

            if (repo.Root is null)
            {
                return new(quest, StartVerdict.NoRoot,
                    $"no root is known for `{quest.To}` — run `daoris connect` from that repository.");
            }

            // 🔴 Before busy and before capacity, because those are waits and this is a stop: a
            // parked quest must not read as "queued" in a surface that renders the reason.
            var strikes = snapshot.Strikes.TryGetValue(quest.Id, out var failures)
                ? failures - config.ForgivenAt(quest.Id)
                : 0;
            // The person's words to its session are their Try again (MSG1b, D137 §2.2): a failure they write to is forgiven as
            // its record leaves `failed`, and a quest the strikes parked goes on with them, as a retry would start it.
            if (config.Strikes > 0 && strikes >= config.Strikes && !(continuing && into is { WordsWaiting: true }))
            {
                return new(quest, StartVerdict.Exhausted,
                    $"{strikes} session(s) have failed on `#{quest.Id}` without landing anything — "
                    + $"parked, because trying again spends an account rather than making progress. "
                    + $"`daoris driver retry {quest.Id}` starts it again once you know why.");
            }

            // 🔴 A session outlives the look that started it (DEV3), so its quest can still be open while it
            // works — before it takes it, or when it never will. That session holds the quest: a second one
            // would double its work, and in a tree of its own nothing below would stop it.
            if (snapshot.Active.FirstOrDefault(s => string.Equals(s.Quest, quest.Id, StringComparison.OrdinalIgnoreCase) && !Itself(s))
                is { } serving)
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"session `{serving.Id}` is already working on `#{quest.Id}`.");
            }

            // 🔴 The TREE is the lock (D51), and where every session here opens its own there is no
            // reason to run one at a time (PAR1). What still holds is the one tree a resume or a carry-on goes back into.
            if (config.OpensOwnTree(quest.To))
            {
                if (into is { Tree: { Length: > 0 } tree }
                    && snapshot.Active.FirstOrDefault(s => SameTree(s.Tree, tree) && !Itself(s)) is { } holder)
                {
                    return new(quest, StartVerdict.RepositoryBusy,
                        $"session `{holder.Id}` is active in the tree `#{quest.Id}` goes back into — one session per tree.");
                }
            }
            else if ((continuing
                         ? snapshot.Active.FirstOrDefault(s =>
                             string.Equals(s.Repository, quest.To, StringComparison.OrdinalIgnoreCase) && !Itself(s))?.Id
                         : blockedBy.GetValueOrDefault(quest.To)) is { } session)
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"session `{session}` is active in `{quest.To}` — one session per repository, whose root "
                    + "is its one tree. Sessions there run side by side once they open trees of their own.");
            }
            else if (startedThisTick.TryGetValue(quest.To, out var ahead))
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"queued behind quest `#{ahead}` in `{quest.To}` — oldest first, one at a time.");
            }

            // An answered park already holds its slot among the active (ANSWER1a): going on, it starts no second session. An
            // ended record holds none (MSG1b), so going on takes one, as a start does; it was planned first, so it is first to.
            if (!(continuing && snapshot.Active.Any(Itself)))
            {
                if (slots <= 0)
                {
                    return new(quest, StartVerdict.AtCapacity,
                        $"the concurrency cap ({config.Cap}) is spent — it frees as sessions finish.");
                }

                slots--;
            }

            startedThisTick[quest.To] = quest.Id;
            return new(quest, StartVerdict.Start, $"starting in `{quest.To}`.", repo.Root, repo.Workspace);
        }
    }

    /// <summary>Two tree paths are one tree — separators and case aside, as Windows sees them.</summary>
    private static bool SameTree(string? a, string? b) =>
        a is { Length: > 0 } && b is { Length: > 0 }
        && string.Equals(
            a.Replace('\\', '/').TrimEnd('/'), b.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
