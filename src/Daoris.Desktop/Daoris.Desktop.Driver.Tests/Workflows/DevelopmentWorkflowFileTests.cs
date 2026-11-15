using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The repository's own development workflow files, as <c>daoris workflow apply</c> reads them and the save judges them: each a version of
/// schema 2 whose graph holds, so applying it is never refused for want of a graph or a rule. <c>daoris-dev</c> is the repository's;
/// <c>daoris-adapter</c> is the one its kind <c>adapter</c> maps to, an adapter package's own development.
/// </summary>
public sealed class DevelopmentWorkflowFileTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "dev-workflow", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    public static TheoryData<string> Files => new() { "daoris-dev", "daoris-adapter" };

    private static JsonNode? File(string id) => JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "tools", "workflows", $"{id}.json")));

    [Theory]
    [MemberData(nameof(Files))]
    public void The_file_is_a_version_apply_reads_as_a_graph_and_the_save_judges_it_whole(string id)
    {
        Assert.Equal(2, File(id)!["schema"]!.GetValue<int>());
        Assert.True(DriverWorkflowsCommand.GraphIn(File(id), out var graph));

        Assert.Empty(WorkflowOperations.GraphProblems(_home, graph!));
        var planned = WorkflowOperations.PlanGraph(_home, null, id, null, "Daoris development", "terminal", "2026-11-15T00:00:00Z", graph);
        Assert.NotNull(planned.Next?.Graph);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void A_run_bound_to_it_performs_the_graph_s_work_step_its_first_turn_names(string id)
    {
        Assert.True(DriverWorkflowsCommand.GraphIn(File(id), out var graph));
        var planned = WorkflowOperations.PlanGraph(_home, null, id, null, "Daoris development", "terminal", "2026-11-15T00:00:00Z", graph);
        var store = WorkflowPorts.Store(_home);
        store.Write(id, planned.Plan.Result!);
        var binding = new WorkflowRunBinding("q1", "Owner", "ws", id, WorkflowLevels.Task, DateTimeOffset.Parse("2026-11-15T10:00:00Z")) { Version = 1 };

        Assert.Equal(new WorkStep("work", 1), WorkflowWorkSteps.Of(store, binding));
        Assert.Null(WorkflowWorkSteps.Of(store, binding with { Workflow = WorkflowSelection.Current }));
    }

    [Fact]
    public void The_adapters_red_goes_back_to_the_work_before_the_repositorys_reached_gates_run()
    {
        Assert.True(DriverWorkflowsCommand.GraphIn(File("daoris-adapter"), out var node));
        var (graph, _) = NodeGraphs.Read(node!.ToJsonString());

        Assert.Contains(graph!.Wires, wire => wire is { Kind: WireKinds.After, From.Node: "content", To.Node: "suite" });
        Assert.Contains(graph.Wires, wire => wire is { Kind: WireKinds.After, From.Node: "suite", To.Node: "check" });
        Assert.Contains(graph.Wires, wire => wire is { Kind: "back", From.Node: "suite", To.Node: "work" });
    }
}
