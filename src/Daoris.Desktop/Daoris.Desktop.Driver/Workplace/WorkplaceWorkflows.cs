namespace Daoris.Driver;

/// <summary>
/// What the workplace asks of the workflows: the landed route's accepted declaration, the process a quest's work follows, and a chat run's
/// bind and press. Workflows keeps each and implements this, installed when the driver loads
/// (<c>Workflows/WorkplaceWorkflowsWiring.cs</c>); each operation does its caller's own work, over the
/// workplace's own facets (<see cref="IWorkplaceProcess"/>, <see cref="IWorkplacePluginCatalog"/>) and the service's quests.
/// </summary>
internal interface IWorkplaceWorkflows
{
    /// <summary>The blob of <c>daoris.gates.json</c> the person accepted for the repository's landed route, or null (see <c>WorkflowSelection.ResolveLanded</c>).</summary>
    string? AcceptedGates(DriverConfig config, string repository, string? workspace);

    /// <summary>The text of the declaration at <paramref name="blob"/> in the checkout at <paramref name="root"/>, or null (see <c>LandedGates.TextAsync</c>).</summary>
    Task<string?> DeclarationTextAsync(string root, string blob, CancellationToken ct);

    /// <summary>
    /// The process a quest's work in <paramref name="repository"/> follows, its run read from the chain <paramref name="quests"/> answers
    /// (see <c>WorkflowProcesses.ReadAsync</c>).
    /// </summary>
    Task<IWorkplaceProcess> ReadProcessAsync(
        string home, DriverConfig config, Func<CancellationToken, Task<IReadOnlyList<QuestView>>> quests, string quest, string repository,
        string? workspace, CancellationToken ct);

    /// <summary>
    /// A chat run bound first, as the press binds it (see <c>ChatRuns.BindFirst</c>): why the home could not keep the binding, or null
    /// where it is bound, read back, or a graph choice binds nothing.
    /// </summary>
    string? BindChatFirst(DriverConfig config, string home, string run, string tree, IWorkplacePluginCatalog plugins, DateTimeOffset at);

    /// <summary>The press's view of a chat run's record: its line's ids, oldest first, and whether it is live (see <c>ChatRuns.PressOf</c>).</summary>
    (IReadOnlyList<string> Line, bool Live) ChatPressOf(string session, string recordsJson);

    /// <summary>The quest whose work a record continues though its own is none (see <c>ChatRuns.TakenOf</c>).</summary>
    string? TakenOf(string session, string recordsJson);

    /// <summary>
    /// A press's candidate on a graph-bound chat run (see <c>ChatCandidates.Submit</c>): submitted at <paramref name="tip"/>, the run's
    /// binding read first, and none for a run bound to anything else or held by a problem.
    /// </summary>
    void SubmitChatCandidate(string home, string run, string repository, string? workspace, string branch, string tip, DateTimeOffset at, Func<string, bool> landed);

    /// <summary>
    /// The person's words where <paramref name="node"/>, the look of a chat run's graph, stands skipped by them or their skip waits on the run's
    /// record; null otherwise (see <c>LookReading.SkippedWords</c>).
    /// </summary>
    string? SkippedLook(string home, string run, string? node);

    /// <summary>
    /// The person's Accept kept as the review's word of <paramref name="process"/>'s run, where its graph holds a review
    /// (see <c>RunNodeWords.Word</c>); a process whose graph holds none, or that is no run's, answers <see cref="ReviewWord.HasReview"/> false.
    /// </summary>
    /// <param name="words">What the word carries: for a head's combined Accept, the operation bringing sessions together it delivers.</param>
    /// <param name="revision">The review record the press drew, which a review standing on another refuses <c>moved</c>; null for the one standing.</param>
    ReviewWord KeepReviewWord(string home, Func<bool>? holdsLease, IWorkplaceProcess process, DateTimeOffset now, string? words = null, string? revision = null);

    /// <summary>Keeps the repair a carried review Accept waits on, on the run's record (see <c>RunRepairs.Keep</c>).</summary>
    void KeepRepair(string home, Func<bool>? holdsLease, string unit, string review, string tip, string turn, int carries, DateTimeOffset at);

    /// <summary>Takes the review's repair back (see <c>RunRepairs.Take</c>).</summary>
    void TakeRepair(string home, Func<bool>? holdsLease, string unit, string review);

    /// <summary>How many times the session is carried on for the review's Accept; 0 where no repair is kept (see <c>RunRepairs.Of</c>).</summary>
    int RepairCarries(string home, Func<bool>? holdsLease, string unit, string review);

    /// <summary>
    /// Whether work in <paramref name="repository"/> follows a saved workflow (a version of the person's own or the home's default), not
    /// Current: its landing is that workflow's, which the access setup's pauses do not compose into.
    /// </summary>
    bool FollowsSaved(DriverConfig config, string repository, string? workspace);

    /// <summary>Who requests <paramref name="repository"/>'s integration and where a conflict goes, from the saved workflows under <paramref name="home"/> (see <c>PlaceIntegrates.Of</c>).</summary>
    PlaceIntegrate IntegrateOf(string home, DriverConfig config, string repository, string? workspace);
}

/// <summary>What keeping the person's Accept as a run's review word came to.</summary>
/// <param name="HasReview">Whether the run's graph holds a review, so a word is to be kept; false lands by itself.</param>
/// <param name="Kept">Whether the word is on the run's record: kept now, or kept already and not applied yet.</param>
/// <param name="Unit">The run's own unit, whose record holds the review.</param>
/// <param name="Review">The review node's id.</param>
/// <param name="Said">Why the word is refused, as a sentence; null where it is kept, or where the review does not wait for it yet.</param>
public sealed record ReviewWord(bool HasReview, bool Kept, string? Unit, string? Review, string? Said)
{
    /// <summary>The review stands on another record than the one drawn: refused alone, the revision standing named.</summary>
    public string? Moved { get; init; }
}

internal static class WorkplaceWorkflows
{
    private static IWorkplaceWorkflows? _current;

    internal static IWorkplaceWorkflows Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace workflows operations are installed: Workflows installs them when the driver loads");
        set => _current = value;
    }
}
