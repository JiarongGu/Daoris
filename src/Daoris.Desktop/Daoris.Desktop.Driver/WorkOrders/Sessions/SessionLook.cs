namespace Daoris.Driver;

/// <summary>
/// Everything one look at the sessions reads: the records, the quests and the planner's verdicts, each quest's last session here and
/// its strikes as the planner's own readers derive them, the trees' judgement and the marks.
/// </summary>
public sealed record SessionLook(
    IReadOnlyList<SessionRecord> Records, IReadOnlyList<QuestView> Quests, IReadOnlyList<Consideration> Considered) : IKnowledgeSessionLook
{
    public IKnowledgeSessionLook WithTrees(IReadOnlyDictionary<string, TreeWork> trees) => this with { Trees = trees };
    public IKnowledgeSessionLook WithRecords(IReadOnlyList<SessionRecord> records) => this with { Records = records };

    /// <summary>The session this machine last ran on each quest: <see cref="ServiceClient.ReadLastRun"/>, the planner's own reading.</summary>
    public IReadOnlyDictionary<string, PriorSession> LastRun { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The session each taken quest's standing reopen names where a later one is its last here: <see cref="ServiceClient.ReadNamedByReopen"/>,
    /// the planner's own reading (<see cref="Snapshot.NamedByReopen"/>).
    /// </summary>
    public IReadOnlyDictionary<string, PriorSession> NamedByReopen { get; init; } =
        new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each quest's failed sessions since its last *Try again*, counted as the planner counts them.</summary>
    public IReadOnlyDictionary<string, int> Strikes { get; init; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>What each judged tree holds, by <see cref="SessionGroups.Normal"/>; a tree not here is not judged, and holds nothing to review.</summary>
    public IReadOnlyDictionary<string, TreeWork> Trees { get; init; } = new Dictionary<string, TreeWork>(StringComparer.OrdinalIgnoreCase);

    /// <summary>This machine's archive marks, by session.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Archived { get; init; } = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

    /// <summary>
    /// The sessions whose tree or landing this machine still holds (<see cref="SessionDeletion.Kept"/>): what keeps a record the
    /// ledger would delete from being deleted here.
    /// </summary>
    public IReadOnlySet<string> Kept { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The words each record could not go on with (<see cref="GoOnMarks"/>), by session: a record whose every waiting word is marked
    /// waits on nothing, as the planner leaves it.
    /// </summary>
    public IReadOnlyDictionary<string, GoOnMark> Unable { get; init; } = new Dictionary<string, GoOnMark>(StringComparer.Ordinal);

    /// <summary>
    /// The starts the loop's last look held on a cooling account: what a record whose words such a start holds is said with, its
    /// reset. None where no loop has looked, since a fresh plan cannot see a cool-off.
    /// </summary>
    public IReadOnlyList<AccountWait> Waits { get; init; } = [];

    /// <summary>
    /// The ended chats of this machine's the person's words wait on, not every word one its runner could not go on with
    /// (<see cref="SessionGroups.Waiting"/>): the planner never considers a chat, and its runner takes the words up at once, so each
    /// goes on, bar what <see cref="Held"/> says holds it.
    /// </summary>
    public IReadOnlySet<string> ChatsWaiting { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// What holds the words on a record where the planner cannot say it, by session: a chat whose runner said it does not go on yet,
    /// and, where no loop has looked, a driven record whose words resume on its own account while that account cools, since a fresh
    /// plan cannot see a cool-off.
    /// </summary>
    public IReadOnlyDictionary<string, WordsHold> Held { get; init; } = new Dictionary<string, WordsHold>(StringComparer.Ordinal);

    /// <summary>
    /// The call each live session's agent is inside, by session: what the process the look is taken in heard
    /// (<see cref="RunningCalls.Now"/>). None where it runs no session, as the terminal's listing does not.
    /// </summary>
    public IReadOnlyDictionary<string, RunningCall> Calls { get; init; } = new Dictionary<string, RunningCall>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The open conversations of this machine's between turns (<see cref="SessionGroups.Between"/>): live, their turn ended and
    /// nothing waiting, so their own tree offers its work to land as an ended session's does.
    /// </summary>
    public IReadOnlySet<string> Between { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The sessions a supervisor's held decision holds, from the decisions' memory in the process the look is taken in; none where
    /// it keeps none, as the terminal's listing.
    /// </summary>
    public IReadOnlySet<string> SupervisorHeld { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Whether a held decision holds <paramref name="record"/>: one is held, and no word of the owner's waits (the planner's own rule).</summary>
    public bool SupervisorHolds(PriorSession record) =>
        SupervisorHeld.Contains(record.Session) && !record.Waiting.Any(word => word.Persons);

    /// <summary>
    /// A look from the records as the service answered them: parsed, and each quest's last run and strikes read by the planner's own
    /// readers, never a second copy of either rule.
    /// </summary>
    /// <param name="forgivenAt">The mark for a quest (<see cref="DriverConfig.ForgivenAt"/>): the planner counts strikes from it.</param>
    public static SessionLook From(
        string recordsJson, IReadOnlyList<QuestView> quests, IReadOnlyList<Consideration> considered, Func<string, int> forgivenAt)
    {
        var lastRun = ServiceClient.ReadLastRun(recordsJson);
        return new(SessionRecords.Parse(recordsJson), quests, considered)
        {
            LastRun = lastRun,
            NamedByReopen = ServiceClient.ReadNamedByReopen(recordsJson, quests, lastRun),
            Strikes = ServiceClient.ReadStrikes(recordsJson)
                .ToDictionary(pair => pair.Key, pair => pair.Value - forgivenAt(pair.Key), StringComparer.OrdinalIgnoreCase),
        };
    }
}
