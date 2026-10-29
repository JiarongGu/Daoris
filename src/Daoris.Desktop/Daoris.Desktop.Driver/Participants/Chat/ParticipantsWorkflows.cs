namespace Daoris.Driver;

/// <summary>
/// What the participants ask of the workflows for a conversation's chat run: whether a run is bound, which chat run a record is one of, the
/// run bound with what the conversation is told of it, and a run's process. Workflows keeps each and implements this, installed when the
/// driver loads (<c>Workflows/ParticipantsWorkflowsWiring.cs</c>); each operation does exactly what its caller did before it was a port, over
/// the workplace's own facets (<see cref="IWorkplaceProcess"/>, <see cref="IWorkplacePluginCatalog"/>).
/// </summary>
internal interface IParticipantsWorkflows
{
    /// <summary>Whether a run is bound under <paramref name="home"/> (see <c>WorkflowRunBindings.Bound</c>).</summary>
    bool RunBound(string home, string run);

    /// <summary>The chat run a record is one of, as the view classifies it, or null (see <c>ChatRuns.ChatOf</c>).</summary>
    string? ChatOf(TracedSession session, IReadOnlyList<TracedSession> sessions, Func<string, bool> holds);

    /// <summary>A chat run bound as a conversation's tree opens or its turn ends (see <c>ChatRuns.Bind</c>), what it came to.</summary>
    IParticipantsChatBound Bind(
        DriverConfig config, string home, string run, string repository, string? workspace, IWorkplacePluginCatalog plugins, DateTimeOffset at);

    /// <summary>The process of a run, its binding read (see <c>WorkflowProcesses.Read</c>).</summary>
    IWorkplaceProcess ReadProcess(string home, DriverConfig config, string repository, string? workspace, string? run);
}

/// <summary>What a chat run's bind came to, and what the conversation is told of it.</summary>
internal interface IParticipantsChatBound
{
    /// <summary>Why the home could not keep the binding; null where it was kept, read back, or a graph choice bound nothing.</summary>
    string? Failed { get; }

    /// <summary>Whether this bind wrote the binding.</summary>
    bool Wrote { get; }

    /// <summary>What the conversation's first line says of the binding it wrote under a named workflow; null under Current or where none was written.</summary>
    Noted? Told();

    /// <summary>What the conversation is told once of a bind the home could not keep, for <paramref name="problem"/>.</summary>
    Noted FailedLine(string problem);

    /// <summary>The process the run's told landing is planned with: its binding's, else the rules live (see <c>WorkflowProcesses.Bound</c>).</summary>
    IWorkplaceProcess Process(string home, DriverConfig config, string repository, string? workspace, string run);
}

internal static class ParticipantsWorkflows
{
    private static IParticipantsWorkflows? _current;

    internal static IParticipantsWorkflows Current
    {
        get => _current ?? throw new InvalidOperationException("no participants workflows operations are installed: Workflows installs them when the driver loads");
        set => _current = value;
    }
}
