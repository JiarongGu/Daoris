namespace Daoris.Driver;

/// <summary>
/// Which of a verdict's sentences the planner said, as a code with its facts, for the verdicts whose sentence the page had no words
/// for: <see cref="StartVerdict.AtCapacity"/>, <see cref="StartVerdict.RepositoryBusy"/>, <see cref="StartVerdict.Waiting"/>,
/// <see cref="StartVerdict.WaitsForOpinion"/>, <see cref="StartVerdict.NotAdopted"/> and <see cref="StartVerdict.SourceReopened"/>.
/// The page words it by the code, never by reading the English beside it (<see cref="Consideration.Reason"/>). Each fact is null
/// where the code's sentence names none.
/// </summary>
/// <param name="Code">One of the constants below, read with the verdict: the same code under two verdicts never meets.</param>
public sealed record SittingFact(string Code)
{
    /// <summary>The concurrency cap that is spent (<see cref="StartVerdict.AtCapacity"/>).</summary>
    public int? Cap { get; init; }

    /// <summary>The session that holds the quest, its tree or its repository; or that will read a question's evidence.</summary>
    public string? Session { get; init; }

    /// <summary>The other quest the sentence names: one queued ahead, the question waited on, the step before, the reopened source.</summary>
    public string? Quest { get; init; }

    /// <summary>The repository the awaited question is asked of.</summary>
    public string? Repository { get; init; }

    /// <summary>The environment a set-up step waits for review in, where it names one.</summary>
    public string? Environment { get; init; }

    /// <summary>The second opinion's state (<see cref="OpinionGateStates"/>); null where it is not judged yet.</summary>
    public string? State { get; init; }

    /// <summary>The quest's declared paths that meet the earlier quest's work (<see cref="Overlaps"/>); null where the code names none.</summary>
    public IReadOnlyList<string>? Paths { get; init; }

    /// <summary><see cref="StartVerdict.RepositoryBusy"/>: its declared paths meet an earlier unit's of the repository under the rule's <c>overlap: wait</c>.</summary>
    public const string Overlaps = "overlaps";

    /// <summary><see cref="StartVerdict.AtCapacity"/>: the cap is spent.</summary>
    public const string CapSpent = "cap";

    /// <summary><see cref="StartVerdict.RepositoryBusy"/>: a session is already working on the quest.</summary>
    public const string Serving = "serving";

    /// <summary><see cref="StartVerdict.RepositoryBusy"/>: the quest is starting on this machine already.</summary>
    public const string Opening = "opening";

    /// <summary><see cref="StartVerdict.RepositoryBusy"/>: a session is active in the tree the quest goes back into.</summary>
    public const string Tree = "tree";

    /// <summary><see cref="StartVerdict.RepositoryBusy"/>: queued behind a quest starting in the tree it goes back into.</summary>
    public const string TreeOpening = "treeOpening";

    /// <summary><see cref="StartVerdict.RepositoryBusy"/>: queued behind a quest going into that tree at this look.</summary>
    public const string TreeThisLook = "treeThisLook";

    /// <summary><see cref="StartVerdict.RepositoryBusy"/>: the session holds the place <see cref="Repository"/>, parked with <see cref="Paths"/> where named.</summary>
    public const string Place = "place";

    /// <summary><see cref="StartVerdict.RepositoryBusy"/>: <see cref="Cap"/> sessions work apart in <see cref="Repository"/> at once, its isolate step's count.</summary>
    public const string Apart = "apart";

    /// <summary><see cref="StartVerdict.RepositoryBusy"/>: queued behind a quest starting in the repository at this look.</summary>
    public const string Queued = "queued";

    /// <summary><see cref="StartVerdict.RepositoryBusy"/>: queued behind a quest still starting in the repository's root.</summary>
    public const string RepositoryOpening = "repositoryOpening";

    /// <summary><see cref="StartVerdict.Waiting"/>: the question is not answered yet.</summary>
    public const string Asked = "asked";

    /// <summary><see cref="StartVerdict.Waiting"/>: the question is closed done, its evidence read when its session ends.</summary>
    public const string UnreadRunning = "unreadRunning";

    /// <summary><see cref="StartVerdict.Waiting"/>: the question is closed done, its evidence not read yet.</summary>
    public const string Unread = "unread";

    /// <summary><see cref="StartVerdict.Waiting"/>: as <see cref="Unread"/>, and no session here ended on it, so its check names a commit.</summary>
    public const string UnreadAtCommit = "unreadAtCommit";

    /// <summary><see cref="StartVerdict.Waiting"/>: the question is closed done without the evidence it names.</summary>
    public const string Missing = "missing";

    /// <summary><see cref="StartVerdict.Waiting"/>: the question is a set-up step waiting for the person's review.</summary>
    public const string Unreviewed = "unreviewed";

    /// <summary><see cref="StartVerdict.Waiting"/>: the question is closed done departing from what the person required.</summary>
    public const string Departed = "departed";

    /// <summary><see cref="StartVerdict.NotAdopted"/>: the repository is not registered on this machine.</summary>
    public const string Unregistered = "unregistered";

    /// <summary><see cref="StartVerdict.NotAdopted"/>: not adopted, and no root is known for it here.</summary>
    public const string NoRoot = "noRoot";

    /// <summary><see cref="StartVerdict.NotAdopted"/>: not adopted, and the pipe door would hand its session no connector.</summary>
    public const string NoConnector = "noConnector";

    /// <summary><see cref="StartVerdict.WaitsForOpinion"/>: a set-up step waits for the opinion on the work it shows.</summary>
    public const string Landing = "landing";

    /// <summary><see cref="StartVerdict.WaitsForOpinion"/>: a chain's next step waits for the opinion on the step before.</summary>
    public const string Steps = "steps";

    /// <summary><see cref="StartVerdict.SourceReopened"/>: open, so withdrawn until the person chooses.</summary>
    public const string Withdrawn = "withdrawn";

    /// <summary><see cref="StartVerdict.SourceReopened"/>: taken, so held until the person chooses.</summary>
    public const string HeldByReopen = "held";

    /// <summary><see cref="StartVerdict.TransientHeld"/>: its last session's provider kept failing past its bound.</summary>
    public const string Transient = "transient";

    /// <summary><see cref="StartVerdict.PolicyHeld"/>: its role's policy asks before it moves the quest to another choice.</summary>
    public const string PolicyAsk = "policy-ask";

    /// <summary><see cref="StartVerdict.PolicyHeld"/>: the quest made its role's policy's bound of moves in 24 hours.</summary>
    public const string MovesBound = "moves-bound";

    /// <summary><see cref="StartVerdict.Stopped"/>: the steward stopped its last session.</summary>
    public const string Steward = "steward";

    /// <summary><see cref="StartVerdict.Blocked"/>: a start a steward's act caused would work in the owner's checkout, so it is held for the owner.</summary>
    public const string Checkout = "checkout";
}

/// <summary>A quest a second opinion sits, with the sentence the look said and its code, so the planner's verdict carries both.</summary>
public sealed record OpinionSit(string Reason, SittingFact Fact);
