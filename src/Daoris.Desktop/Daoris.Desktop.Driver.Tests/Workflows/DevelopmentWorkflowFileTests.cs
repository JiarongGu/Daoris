using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The repository's own development workflow file, as <c>daoris workflow apply</c> reads it and the save judges it: a version of schema 2
/// whose graph holds, so applying it is never refused for want of a graph or a rule.
/// </summary>
public sealed class DevelopmentWorkflowFileTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "dev-workflow", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private static JsonNode? File() => JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "tools", "workflows", "daoris-dev.json")));

    [Fact]
    public void The_file_is_a_version_apply_reads_as_a_graph_and_the_save_judges_it_whole()
    {
        Assert.Equal(2, File()!["schema"]!.GetValue<int>());
        Assert.True(DriverWorkflowsCommand.GraphIn(File(), out var graph));

        Assert.Empty(WorkflowOperations.GraphProblems(_home, graph!));
        var planned = WorkflowOperations.PlanGraph(_home, null, "daoris-dev", null, "Daoris development", "terminal", "2026-11-15T00:00:00Z", graph);
        Assert.NotNull(planned.Next?.Graph);
    }
}
