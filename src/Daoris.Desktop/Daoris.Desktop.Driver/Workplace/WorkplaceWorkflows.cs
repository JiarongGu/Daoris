namespace Daoris.Driver;

/// <summary>
/// What the workplace asks of the workflows: the landed route's accepted declaration, the process a quest's work follows, and a chat run's
/// bind and press. Workflows keeps each and implements this, installed when the driver loads
/// (<c>Workflows/WorkplaceWorkflowsWiring.cs</c>); each operation does exactly what its caller did before it was a port, over the
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
    /// where it was bound, read back, or a graph choice bound nothing.
    /// </summary>
    string? BindChatFirst(DriverConfig config, string home, string run, string tree, IWorkplacePluginCatalog plugins, DateTimeOffset at);

    /// <summary>The press's view of a chat run's record: its line's ids, oldest first, and whether it is live (see <c>ChatRuns.PressOf</c>).</summary>
    (IReadOnlyList<string> Line, bool Live) ChatPressOf(string session, string recordsJson);

    /// <summary>Current's landing graph for <paramref name="rule"/>: the method its <c>land</c> names, and the graph's digest (see <c>NodeGraphHomes.Landing</c>).</summary>
    (string Method, string Graph) CurrentLand(LandingRule rule);

    /// <summary>
    /// The person's Accept kept as the review's word of <paramref name="process"/>'s run (WORKFLOWCLEAN1 1c), where its graph holds a review
    /// (see <c>RunNodeWords.Word</c>); a process whose graph holds none, or that is no run's, answers <see cref="ReviewWord.HasReview"/> false.
    /// </summary>
    ReviewWord KeepReviewWord(string home, Func<bool>? holdsLease, IWorkplaceProcess process, DateTimeOffset now);

    /// <summary>Keeps the repair a carried review Accept waits on, on the run's record (see <c>RunRepairs.Keep</c>).</summary>
    void KeepRepair(string home, Func<bool>? holdsLease, string unit, string review, string tip, string turn, int carries, DateTimeOffset at);

    /// <summary>Takes the review's repair back (see <c>RunRepairs.Take</c>).</summary>
    void TakeRepair(string home, Func<bool>? holdsLease, string unit, string review);

    /// <summary>How many times the session was carried on for the review's Accept; 0 where no repair is kept (see <c>RunRepairs.Of</c>).</summary>
    int RepairCarries(string home, Func<bool>? holdsLease, string unit, string review);
}

/// <summary>What keeping the person's Accept as a run's review word came to (WORKFLOWCLEAN1 1c).</summary>
/// <param name="HasReview">Whether the run's graph holds a review, so a word was to be kept; false lands by itself.</param>
/// <param name="Kept">Whether the word is on the run's record: kept now, or kept already and not applied yet.</param>
/// <param name="Unit">The run's own unit, whose record holds the review.</param>
/// <param name="Review">The review node's id.</param>
/// <param name="Said">Why the word was refused, as a sentence; null where it was kept, or where the review does not wait for it yet.</param>
public sealed record ReviewWord(bool HasReview, bool Kept, string? Unit, string? Review, string? Said);

internal static class WorkplaceWorkflows
{
    private static IWorkplaceWorkflows? _current;

    internal static IWorkplaceWorkflows Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace workflows operations are installed: Workflows installs them when the driver loads");
        set => _current = value;
    }
}
