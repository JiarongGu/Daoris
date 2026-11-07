namespace Daoris.Driver;

/// <summary>Everything a tick's decisions are made from, fetched once so the plan is coherent.</summary>
/// <param name="Strikes">
/// How many sessions have <b>failed</b> on each quest, by quest id — <b>derived</b> from the session records this machine already
/// wrote, never a tally the driver keeps. Only `failed` counts, and a stop that is not the person's: a stand-down means somebody
/// else got there first, a decline is a real answer, and the person's stop is theirs. A failure an account's limit made is not
/// counted either: the account cools, and the quest waits for it. A quest nobody has failed is simply absent.
/// </param>
public sealed record Snapshot(
    IReadOnlyList<QuestView> Quests,
    IReadOnlyList<RepoView> Repositories,
    IReadOnlyList<SessionView> Active,
    IReadOnlyDictionary<string, int>? Strikes = null) : IKnowledgeSnapshot, ISupervisorLook<Snapshot>
{
    public IKnowledgeSnapshot Within(IReadOnlyList<QuestView> quests, IReadOnlyList<RepoView> repositories, IReadOnlyList<SessionView> active) =>
        this with { Quests = quests, Repositories = repositories, Active = active };

    public IReadOnlyDictionary<string, int> Strikes { get; init; } =
        Strikes ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The quests whose last session ended past its transient bound and whose role's policy moved them to another choice, by quest:
    /// the session left, so the planner carries the line on from it as <i>Try again</i> would, and the start walks past the choice it left.
    /// </summary>
    public IReadOnlyDictionary<string, StaffMoving> PolicyMoving { get; init; } = new Dictionary<string, StaffMoving>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The failures in a row each quest's last choice made (<see cref="ServiceClient.ReadFailedRuns"/>): what a <c>failed</c> move's <c>after</c> is weighed against.</summary>
    public IReadOnlyDictionary<string, int> FailedRuns { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The quests a role's policy holds for the person's yes, by quest: <c>policy-ask</c> or <c>moves-bound</c>.</summary>
    public IReadOnlyDictionary<string, string> PolicyHolds { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>On a reached home, the failed sessions that park each open quest, 0 for none; a quest absent is parked by <c>strikes</c>.</summary>
    public IReadOnlyDictionary<string, int> FailuresAt { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How many active sessions take a place under the machine's cap (<see cref="ActiveSessions.HoldsSlot"/>): the ones the driver
    /// started, never a conversation the person holds. Every site that spends a slot reads this, never the list's length, which is
    /// every active session and is read as that for a busy tree and a repository's holder. A piece reserved and not yet opened holds
    /// one too (<see cref="Pieces"/>), each counted once, since an active record carrying its piece counts it instead.
    /// </summary>
    public int SlotsHeld => ActiveSessions.Held(Active) + PartnerAdmission.Reserving(Pieces, Active);

    /// <summary>
    /// The session THIS machine last ran on each quest, by quest id — derived from the same records as the strikes, never kept. It
    /// is how a waiting quest is known to be this machine's to resume, and where: a taken quest no session here ran is somebody
    /// else's take.
    /// </summary>
    public IReadOnlyDictionary<string, PriorSession> LastRun { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The newest record of THIS machine's on each quest, by quest id, where it stood down: a take that lost, which
    /// <see cref="LastRun"/> never holds, and which the person's bring-forward carries on once (<see cref="Planner.BroughtForward"/>).
    /// Derived from the same records, never kept. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, PriorSession> StoodDown { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The record of this machine's that each taken quest's standing reopen names, by quest id, where it is not the quest's
    /// <see cref="LastRun"/>: the session that said the reopened <c>ready</c>, which the reopen is told to, so its words go on in it
    /// though a later session ran. Derived from the same records, never kept. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, PriorSession> NamedByReopen { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every record this machine keeps, as the goal's walk counts them: which account each ran on, when, and whether it runs now —
    /// derived from the same records, never kept. The roster reads it at each look.
    /// </summary>
    public IReadOnlyList<SessionStarted> Started { get; init; } = [];

    /// <summary>
    /// The quests of every paused work on this machine, each against the pause that holds it: the look computes it from
    /// <c>driver.json</c>'s pauses and <see cref="AskWork"/> (<see cref="PausedWork.LookAsync"/>), so the planner never holds a
    /// second copy of the work's rule. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, PausedBy> Paused { get; init; } =
        new Dictionary<string, PausedBy>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The closed quests whose last session here has the person's words waiting: no open list holds them, and the session goes on
    /// all the same, its quest staying as it closed. Absent is none.
    /// </summary>
    public IReadOnlyList<QuestView> Closed { get; init; } = [];

    /// <summary>
    /// The marks of words a record could not go on with, by session (<see cref="GoOnMarks"/>): read at each look, so the planner
    /// leaves those words waiting instead of trying them again. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, GoOnMark> Unable { get; init; } =
        new Dictionary<string, GoOnMark>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whose the take is on each taken quest a look would carry on, by quest id: read only for those, since the ledger refuses a
    /// carry-on over a take that is not this machine's. Absent is unread, and the quest is carried on, for the ledger to judge.
    /// </summary>
    public IReadOnlyDictionary<string, QuestTake> Takes { get; init; } =
        new Dictionary<string, QuestTake>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The quests a second opinion sits, by quest id, each with its sentence (<see cref="StartVerdict.WaitsForOpinion"/>): the look
    /// reads them from the opinions owed and the gate's facts, as it reads <see cref="Paused"/>. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, OpinionSit> Opinions { get; init; } =
        new Dictionary<string, OpinionSit>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The quests held on another agent, by quest id, read from <see cref="HandedQuests"/> at each look. The stop of the session a
    /// store names is its own release: the planner reads it as released, so the quest is carried on with no strike and no write to
    /// driver.json's <c>released</c>. Absent is none.
    /// </summary>
    public IReadOnlyDictionary<string, HandedQuest> Handed { get; init; } =
        new Dictionary<string, HandedQuest>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every record's entry key by its id, this machine's and the teammates' mirrors (<c>origin/id</c>), as the service answered it:
    /// a quest's publisher names the line its sessions join (<see cref="EntryParts.KeyOf"/>). Absent is none answered.
    /// </summary>
    public IReadOnlyDictionary<string, string> Entries { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The starts of this machine's loop not yet open, as the look reserved them: the planner holds each as it holds a live session
    /// (<see cref="StartReservation"/>), and counts under the cap each one no active session already stands for. Absent is none.
    /// </summary>
    public IReadOnlyList<StartReservation> Starting { get; init; } = [];

    /// <summary>
    /// The pieces on file with their leads and states, as the look read the boxes: those reserved and not yet opened hold a slot in
    /// <see cref="SlotsHeld"/>, the common admission every starter reads. Absent is none.
    /// </summary>
    public IReadOnlyList<PieceReservation> Pieces { get; init; } = [];

    /// <summary>
    /// How many commits the tree of each open quest's last session here holds beyond the commit it opened at, by quest id, where that
    /// session is cut off before it takes the quest (<see cref="CommittedWork"/>): the look reads them from git, and the planner
    /// carries a quest with any on in that tree rather than starting it again in a new one. Absent is unread or none.
    /// </summary>
    public IReadOnlyDictionary<string, int> Committed { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The paths each running unit's tree changed since it opened, by quest id (<see cref="WorkOverlaps"/>): one git read per running
    /// session, so a start's comparison sees what an earlier unit already changed as well as what it declared. Absent is unread.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Changed { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The quests whose integration request is open in the integration record: a done unit holds an overlapping start until its request is settled. Absent is none.</summary>
    public IReadOnlySet<string> Integrating { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The sessions a supervisor's decision holds, read from the decisions' memory at each look (<see cref="SupervisorQuarantine.DecideAsync"/>):
    /// while one is <c>held</c> and no word of the owner's waits, the record is held whatever else waits, so a helper's answer or
    /// Daoris's own words cannot cut the overrule window. Absent is none.
    /// </summary>
    public IReadOnlySet<string> SupervisorHeld { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The label each session's released supervisor's decision chose, from memory, which the go-on's sentence names. Absent is none.</summary>
    public IReadOnlyDictionary<string, string> SupervisorChose { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The snapshot with the sessions a held decision holds and the choice each released decision made (<see cref="ISupervisorLook{TLook}"/>).</summary>
    public Snapshot WithSupervisor(IReadOnlySet<string> held, IReadOnlyDictionary<string, string> chose) =>
        this with { SupervisorHeld = held, SupervisorChose = chose };

    /// <summary>The snapshot with these records in place of its last runs and its reopens' named records (<see cref="ISupervisorLook{TLook}"/>).</summary>
    public Snapshot WithRecords(IReadOnlyDictionary<string, PriorSession> lastRun, IReadOnlyDictionary<string, PriorSession> namedByReopen) =>
        this with { LastRun = lastRun, NamedByReopen = namedByReopen };

    /// <summary>Whether a held supervisor's decision holds <paramref name="record"/>: one is held, and no word of the owner's waits.</summary>
    public bool SupervisorHolds(PriorSession record) =>
        SupervisorHeld.Contains(record.Session) && !record.Waiting.Any(word => word.Persons);
}
