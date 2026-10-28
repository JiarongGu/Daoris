namespace Daoris.Driver;

/// <summary>What the gate reads of the service: every quest, closed ones included, and an ask. A seam, so its tests stand in.</summary>
public interface IReviewWorld
{
    /// <summary>Every quest the service holds, closed ones included, from which a chain is read.</summary>
    Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct);

    /// <summary>One ask, or null where the service holds none.</summary>
    Task<AskView?> AskAsync(string id, CancellationToken ct);
}

/// <summary>
/// What the landing at done reads of the service (LAND2b): a seam, so its tests stand in for the ledger. Its quests and asks are what the
/// review's gate reads too (REVIEWENV1c), so a look lands nothing the gate holds. The review's <c>IAutoLandingWorld</c> adds the opinions
/// the second opinion's part reads (XAGENT1f) and is the world every door hands the look.
/// </summary>
public interface IWorkplaceLandingWorld : IReviewWorld
{
    /// <summary>One quest as it stands, closed ones included; null where the service holds none.</summary>
    Task<QuestView?> QuestAsync(string id, CancellationToken ct);

    /// <summary>Every session record, closed ones included: which is the newest on a tree, and whether a live one holds it.</summary>
    Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct);

    /// <summary>The trees sessions still running or waiting name, which the rule's tidy keeps (LAND3).</summary>
    Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct);

    /// <summary>
    /// A world that reads the service afresh (FABRIC-END7's review): each chosen entry is judged again from it just before its own
    /// landing. A world that keeps nothing between reads answers itself.
    /// </summary>
    IWorkplaceLandingWorld Again() => this;

    /// <summary>
    /// The say door (MSG1a) for Daoris's words (LANDREBASE1): kept on an ended or parked record of this machine's under their fact's key,
    /// for its own conversation to go on with at the next look. Null where the world has no door, and nothing is said.
    /// </summary>
    Task<SayAnswer?> SayAsync(string session, string text, string key, CancellationToken ct) => Task.FromResult<SayAnswer?>(null);
}

/// <summary>The doors a landing or a verdict is given at, as the log's lines name them; the review's <c>ReviewDoors</c> spells them alike.</summary>
internal static class LandingDoors
{
    internal const string Screen = "screen";
    internal const string Terminal = "terminal";
    internal const string Look = "look";
}

/// <summary>
/// What the trees read of the whole landing gate a door hands them (XAGENT1f; the second-agent design §7): the commit it was judged at,
/// the first part that holds as a landing's refusal, the process it was read with, what the review and the second opinion keep on the
/// landing's record when they let the work go, whether the graph's part asks a pass, and the gate's words for the plan and the machine
/// log. The review's <c>LandingGate</c> implements this directly, so the landing names no more of the gate than it reads.
/// </summary>
public interface IWorkplaceGate
{
    /// <summary>
    /// The tree's tip the gate was judged at, where the door pins it: the landing lands exactly that commit, or refuses where the tree
    /// moved past it. Null lands the tree as it stands, as the person's press does.
    /// </summary>
    string? Tip { get; }

    /// <summary>The gate pinned to <paramref name="tip"/>, every other part as it was.</summary>
    IWorkplaceGate WithTip(string? tip);

    /// <summary>The first part that holds, as a landing's refusal: its sentence and its code; null where all let go.</summary>
    TreeLanding? Refusal { get; }

    /// <summary>The process the gate was read with, which the landing lands by; null where none was read.</summary>
    IWorkplaceProcess? Process { get; }

    /// <summary>What the review's gate keeps of the set-up that let the work go; null where it asked none.</summary>
    LandingReview? ReviewLanding { get; }

    /// <summary>What the second opinion's gate keeps of the pass that let the work go; null where the level asked none.</summary>
    LandingOpinion? OpinionLanding { get; }

    /// <summary>Whether what holds the work is its graph's part and that part asks a pass of the run's next look.</summary>
    bool GraphAsksPass { get; }

    /// <summary>What <c>trees land --plan</c> says of the gate before the press: each part that asks anything, in the gate's order.</summary>
    IReadOnlyList<string> PlanLines();

    /// <summary>The machine log's line for a landing the gate held, by the part that held it; null where none did.</summary>
    LandingLine? Held(string session, (string Workspace, string Repository) owner, TreeLanding landed, string door);
}

/// <summary>
/// What the workplace asks of the review: the second opinion a landing the gate holds is owed. The review keeps it and implements this,
/// installed when the driver loads (<c>Review/WorkplaceReviewWiring.cs</c>); it does exactly what <see cref="AutoLander"/> did before it
/// was a port.
/// </summary>
internal interface IWorkplaceReview
{
    /// <summary>
    /// Owe the second opinion for a landing at the landing occasion unless one is owed there already, and unless the loop closed one as
    /// landed since <paramref name="since"/>. Never throws for a due list that cannot be written (see <c>AutoLander.Owe</c>).
    /// </summary>
    /// <param name="at">The time the due is made at, read only where one is made.</param>
    void OweLanding(
        string home, string session, string quest, string repository, string workspace, string tree, Func<DateTimeOffset> at, DateTimeOffset? since);
}

internal static class WorkplaceReview
{
    private static IWorkplaceReview? _current;

    internal static IWorkplaceReview Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace review operations are installed: Review installs them when the driver loads");
        set => _current = value;
    }
}
