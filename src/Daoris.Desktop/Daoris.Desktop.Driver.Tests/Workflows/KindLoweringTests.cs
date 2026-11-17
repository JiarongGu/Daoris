using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a kind's workflow asks less than the one its work would follow without the kind, which decides whether the kind's paths hold
/// the work: the person's part, and the checks. A kind whose workflow runs fewer of the declaration's gates lands a path outside the
/// kind's on checks that never reached it, so its paths hold as they do for one that drops the person's look. Read over this
/// repository's own two development graphs, the adapters' and the rest's.
/// </summary>
public sealed class KindLoweringTests
{
    private static NodeGraph Tracked(string file)
    {
        var text = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "tools", "workflows", file));
        Assert.True(DriverWorkflowsCommand.GraphIn(JsonNode.Parse(text), out var node));
        var (graph, problems) = NodeGraphs.Read(node!.ToJsonString());
        Assert.True(graph is not null, string.Join("; ", problems.Select(problem => problem.Words)));
        return graph!;
    }

    /// <summary>The adapters' graph with its reached check narrowed to the lock alone, as a version that ran only the adapter's own check would be.</summary>
    private static NodeGraph Narrowed()
    {
        var adapter = Tracked("daoris-adapter.json");
        return adapter with
        {
            Nodes = [.. adapter.Nodes.Select(node => node.Id == "check"
                ? node with { Options = new Dictionary<string, string>(StringComparer.Ordinal) { ["gates"] = """["packages-lock"]""" } }
                : node)],
        };
    }

    [Fact]
    public void A_kind_whose_workflow_narrows_a_check_lowers_and_its_paths_hold_a_change_outside_them()
    {
        var dev = Tracked("daoris-dev.json");
        var narrowed = Narrowed();

        var lowered = KindLowering.Of(Involvement.StepsOf(narrowed), narrowed, dev);

        Assert.Equal(["it narrows the check `check`'s gates"], lowered);
        var gate = WorkflowGate.Judge(Process(narrowed), new KindPathsRead(lowered, "`daoris-dev` v1",
            ["src/Daoris.Adapters/Daoris.Adapters.Git/GitProcessRuns.cs", "src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/Driver.cs"]));
        Assert.Equal(WorkflowGateStates.KindPaths, gate.State);
        Assert.Equal(["src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/Driver.cs"], gate.Outside);
    }

    [Fact]
    public void The_adapters_workflow_adds_its_own_check_and_keeps_every_gate_the_rest_runs_so_it_lowers_nothing()
    {
        var adapter = Tracked("daoris-adapter.json");

        Assert.Empty(KindLowering.Of(Involvement.StepsOf(adapter), adapter, Tracked("daoris-dev.json")));
        Assert.Equal(["packages-lock"], CheckNode.GatesOf(adapter.Nodes.Single(node => node.Id == "suite")));
        Assert.True(CheckNode.ReachedChoice(adapter.Nodes.Single(node => node.Id == "check")));
    }

    [Fact]
    public void The_persons_part_still_lowers_where_the_checks_hold_and_an_unbound_graph_compares_the_persons_part_alone()
    {
        var dev = Tracked("daoris-dev.json");
        var noOpinion = dev with
        {
            Nodes = [.. dev.Nodes.Where(node => node.Id != "opinion")],
            Wires = [.. dev.Wires.Where(wire => wire.To.Node != "opinion" && wire.From?.Node != "opinion"),
                new Wire("cl", WireKinds.After, new WireEnd("check"), new WireEnd("landing"))],
        };

        Assert.Equal(["it drops the second opinion"], KindLowering.Of(Involvement.StepsOf(noOpinion), noOpinion, dev));
        Assert.Equal(["it drops the second opinion"], KindLowering.Of(Involvement.StepsOf(noOpinion), null, dev));
    }

    private static WorkflowProcess Process(NodeGraph graph) =>
        new("Daoris", Landing.Merge, null, null)
        {
            Binding = new WorkflowRunBinding("q1", "Daoris", "ws", "daoris-adapter", WorkflowLevels.RepositoryKind, DateTimeOffset.Parse("2026-11-16T10:00:00Z"))
            {
                Version = 1, Kind = "adapter", Paths = ["src/Daoris.Adapters/**", "daoris.packages.lock"],
            },
            Steps = Involvement.StepsOf(graph),
            Graph = graph,
        };
}
