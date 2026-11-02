namespace Daoris.Driver;

/// <summary>The saved workflow facts the room reads, without its graph or executable steps.</summary>
internal interface IKnowledgeWorkflowFile
{
    string Id { get; }
    string? Problem { get; }
    string? Name { get; }
    int NewestVersion { get; }

    /// <summary>The newest version's <c>land</c> method and pattern as it writes them; null where it writes none.</summary>
    string? LandMethod { get; }
    string? LandPattern { get; }
}

internal interface IKnowledgeWorkflows
{
    (string Workflow, string Level) Select(DriverConfig config, string repository, string workspace);
}

internal static class KnowledgeWorkflows
{
    private static IKnowledgeWorkflows? _current;

    internal static IKnowledgeWorkflows Current
    {
        get => _current ?? throw new InvalidOperationException("no reading of workflow choices is installed");
        set => _current = value;
    }
}
