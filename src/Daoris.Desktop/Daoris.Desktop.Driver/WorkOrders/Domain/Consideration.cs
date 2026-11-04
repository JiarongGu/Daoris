namespace Daoris.Driver;

/// <param name="Quest">The quest considered.</param>
/// <param name="Verdict"><see cref="StartVerdict.Start"/>, or why not.</param>
/// <param name="Reason">The sentence a person reads. "Sitting" must always say why.</param>
/// <param name="Root">Where a start would spawn — carried so the executor never re-derives it.</param>
/// <param name="Workspace">The receiver's circle, carried for the same reason — and read by the toolchain.</param>
public sealed record Consideration(
    QuestView Quest, StartVerdict Verdict, string Reason, string? Root = null, string? Workspace = null) : IPlannedStart
{
    /// <summary>
    /// For a start that RESUMES a waiting quest: the session that asked and waited, whose tree the resume carries on in. Null for
    /// every first start.
    /// </summary>
    public PriorSession? Resumes { get; init; }

    /// <summary>
    /// For a chain's next step in the same repository: the step before's last run, whose branch this step's tree grows from, so it
    /// sees the unmerged work it builds on or checks.
    /// </summary>
    public PriorSession? BuildsOn { get; init; }

    /// <summary>
    /// For an open quest carried on in the tree of its last session here, which committed work and failed before it took the quest:
    /// how many commits that tree holds beyond its start, so the start is told to take the quest and finish that work. Set whatever
    /// the verdict, since a carry-on held for now still goes back into that tree. Null for every other quest.
    /// </summary>
    public int? CarriesWork { get; init; }

    /// <summary>
    /// For a first start that is a further part of a session: its entry, the parts it already has here, and whether the start goes
    /// into the entry's tree or runs alongside. Null where no part of its entry is here, where no record names its entry, where the
    /// repository's sessions run in its checkout, and for a chain's step <see cref="BuildsOn"/>.
    /// </summary>
    public EntryParts? Entry { get; init; }

    /// <summary>
    /// The line of work on an ask a start is a part of, whose channel its instruction is handed what waited on: set by the look for a
    /// start (<see cref="ChannelEntry.ForQuest"/>). Null for a quest on no ask.
    /// </summary>
    public ChannelEntry? Channel { get; init; }

    /// <summary>For a quest the person's stop holds (<see cref="StartVerdict.Stopped"/>): the session they stopped, which *Try again* names to release it.</summary>
    public PriorSession? HeldBy { get; init; }

    /// <summary>For a quest a pause holds (<see cref="StartVerdict.Paused"/>): whose pause, an ask's or the quest's own, which *Resume* names.</summary>
    public PausedBy? PausedBy { get; init; }

    /// <summary>
    /// For a quest whose take is not this machine's (<see cref="StartVerdict.TakenElsewhere"/>): whose it is, the machine and its
    /// session where a record names them, and the session here not carried on, so the page says the sentence from facts.
    /// </summary>
    public TakenBy? TakenBy { get; init; }

    /// <summary>For a set-up step the planner sits (<see cref="StartVerdict.CannotShow"/>): the code and the environment and address its sentence is said from.</summary>
    public CannotShowFact? CannotShow { get; init; }

    /// <summary>
    /// For <see cref="StartVerdict.AtCapacity"/>, <see cref="StartVerdict.RepositoryBusy"/>, <see cref="StartVerdict.Waiting"/>,
    /// <see cref="StartVerdict.WaitsForOpinion"/>, <see cref="StartVerdict.NotAdopted"/> and <see cref="StartVerdict.SourceReopened"/>:
    /// which of its sentences <see cref="Reason"/> is, as a code with its facts, so the page words it. Null for every other verdict.
    /// </summary>
    public SittingFact? Said { get; init; }

    /// <summary>
    /// For a start the person's words make: <see cref="Resumes"/> is the record they wait on, parked or ended, which goes on in its
    /// own conversation where it can, and else hands them on.
    /// </summary>
    public bool GoesOn { get; init; }

    /// <summary>
    /// For a carry-on of a stood-down session whose lost take the person brought forward: the fresh take, so the start brings its
    /// tree up to date with the step's current source (<see cref="Planner.BringsForwardToSource"/>) and says the press.
    /// </summary>
    public ForwardTake? BroughtForward { get; init; }

    /// <summary>For a start held at spawn (<see cref="StartVerdict.Blocked"/>): the accounts it passed not signed in, so the page says each and its sign-in from facts.</summary>
    public SignedOutAccounts? SignedOut { get; init; }

    /// <summary>
    /// For a quest its failed sessions park (<see cref="StartVerdict.Exhausted"/>): how many have failed on it here in all, as the
    /// records count them (<see cref="Snapshot.Strikes"/>), before any mark. *Try again* marks the quest at this count, so it starts
    /// and the limit's count parks it again; a mark at the limit left a quest parked a second time parked.
    /// </summary>
    public int? Failures { get; init; }

    /// <summary>For a start under <c>overlap: warn</c> whose declared paths meet an earlier unit's: the overlap, which its reason says.</summary>
    public OverlapFact? Overlaps { get; init; }
}
