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
