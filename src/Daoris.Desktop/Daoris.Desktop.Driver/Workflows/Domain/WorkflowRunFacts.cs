namespace Daoris.Driver;

/// <summary>
/// What one run is read from: its chosen workflow graph and the records that already exist,
/// never a copy of its own. Every list is the run's own, gathered by its reader, so the derivation decides from these alone.
/// </summary>
/// <param name="Current">The graph: Current live or the bound version's steps, with each participant.</param>
public sealed record WorkflowRunFacts(string Repository, CurrentWorkflow Current)
{
    public WorkflowProcess? Process { get; init; }
    public WorkflowGateState? WorkflowGate { get; init; }

    /// <summary>The chain's quests in this repository, in the chain's order: its work, and any set-up step of its review.</summary>
    public IReadOnlyList<QuestView> Quests { get; init; } = [];

    /// <summary>
    /// The whole chain, every repository's steps: where a submission's successor is read, which seals it. Empty
    /// reads <see cref="Quests"/>, so a run gathered without it judges a seal by this repository's steps alone.
    /// </summary>
    public IReadOnlyList<QuestView> Chain { get; init; } = [];

    /// <summary>The session records on those quests, closed ones included, in any order.</summary>
    public IReadOnlyList<TracedSession> Sessions { get; init; } = [];

    /// <summary>The go-aheads on the chain's ask; the run's are those a session or a quest of it asked.</summary>
    public IReadOnlyList<GoAheadView> GoAheads { get; init; } = [];

    /// <summary>The review's gate for the chain's work here (<see cref="ReviewGate"/>), or null where it is not read.</summary>
    public ReviewGateState? Review { get; init; }

    /// <summary>
    /// The second opinion's gate for the chain's work here (<see cref="OpinionGate"/>), as every landing door reads it, or
    /// null where it is not read: no tree of the work stands here to read it in.
    /// </summary>
    public OpinionGateState? Opinion { get; init; }

    /// <summary>This machine's landings that name a session of the run, standing or traces.</summary>
    public IReadOnlyList<LandedBranch> Landings { get; init; } = [];

    /// <summary>When each session's work is accepted into its line, as its conversation's record keeps the acceptance.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Accepted { get; init; } = new Dictionary<string, DateTimeOffset>();

    /// <summary>What a combined Accept brought each session into, read from its operation before any acceptance.</summary>
    public IReadOnlyDictionary<string, TogetherBrought> Brought { get; init; } = new Dictionary<string, TogetherBrought>();

    /// <summary>
    /// A chat run's own facts, where <see cref="Sessions"/> are its line's records in its tree and <see cref="Quests"/> none;
    /// null for a quest's run.
    /// </summary>
    public ChatRunFacts? Chat { get; init; }

    /// <summary>
    /// The requests the work's sessions made (<see cref="WorkRequests.Draw"/>): the quests they published, and the helpers they
    /// asked, read by the reader from every quest and session; drawn inside the work and never held on by this record.
    /// </summary>
    public IReadOnlyList<WorkflowRunRequest> Requests { get; init; } = [];
}
