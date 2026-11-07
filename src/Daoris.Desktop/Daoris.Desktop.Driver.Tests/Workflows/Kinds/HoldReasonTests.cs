using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.HoldFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A hold that keeps its step active says why wherever the run is read: the evaluation keeps its reading's sentence on the node's record, and the
/// run's view, the terminal's <c>workflow run</c> and the landing's plan say it; a move clears it, and a record written without it reads none.
/// </summary>
public sealed class HoldReasonTests : IDisposable
{
    private const string Why = "it holds no commit the line lacks: nothing to land";
    private const string Digest = "gph-0123456789abcdef0123456789abcdef";
    private const string Tip = "1111111111111111111111111111111111111111";
    private const string RunId = "q1";
    private static readonly string Self = RunUnits.IdOf(RunId);

    private const string ClaimedContentHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"content","kind":"content.holds","version":1,"reach":"observed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"c>t","kind":"after","from":"claimed","to":"content"},{"id":"t>h","kind":"after","from":"content","to":"handed"}]}
        """;

    private readonly string _home = Path.Combine(Directory.GetCurrentDirectory(), "_holds-" + Guid.NewGuid().ToString("N")[..8]);

    public HoldReasonTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void The_evaluation_keeps_a_held_steps_reason_on_its_record_and_a_move_clears_it()
    {
        var content = Hold(NodeKinds.ContentHolds);
        NodeDeclaration[] nodes = [content, new("land", NodeKinds.Land, 1, NodeReach.Observed)];
        Wire[] wires = [new("a", WireKinds.After, new WireEnd("content"), new WireEnd("land"))];

        WireEvaluated Evaluate(HoldContent read, string? kept = null) => WireEvaluation.Evaluate(nodes, wires,
            new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["content"] = new(Branch.Id, "content", NodeStates.Active)
                {
                    Attempt = 1, Tries = 1, Binding = new NodeAcceptance(Branch.Id, "tip-1", Scope.GraphDigest, "content", 1, 1), Scope = Scope, Holds = kept,
                },
                ["land"] = NodeLifecycle.Start(Branch.Id, "land"),
            },
            new WireRun(Scope.GraphDigest, Scope.Run)
            {
                Revisions = new Dictionary<string, string?> { [Branch.Id] = "tip-1" },
                Units = new Dictionary<string, WorkUnit> { [Branch.Id] = Branch },
                Git = new NodeGitReads { Content = (_, _) => read },
            });

        var held = Evaluate(new HoldContent(null, 0, 0));
        Assert.True(held.Settled);
        Assert.Equal((NodeStates.Active, Why), (held.Statuses["content"].State, held.Statuses["content"].Holds));
        Assert.Equal(["content"], held.Held);

        // The same reason kept already is nothing new to keep.
        Assert.Empty(Evaluate(new HoldContent(null, 0, 0), kept: Why).Held);

        var let = Evaluate(new HoldContent(null, 2, 2), kept: Why);
        Assert.Equal((NodeStates.Reached, (string?)null), (let.Statuses["content"].State, let.Statuses["content"].Holds));
    }

    [Fact]
    public void A_held_content_steps_reason_is_said_by_workflow_run()
    {
        var run = WorkflowGraphRuns.Derive(new WorkflowGraphRunFacts(Facts(), RunId, Graph(), Held()), "default", "a1");

        var step = run.Steps.Single(each => each.Step.Id == "content");
        Assert.Equal(Why, step.Words);
        Assert.Equal($"Holds the work: {Why}.", WorkflowRunWords.Said(step));
        Assert.Contains($"Holds the work: {Why}.", WorkflowRunWords.Say([run], null));
    }

    [Fact]
    public void A_held_content_steps_reason_is_said_by_the_landings_plan()
    {
        var gate = GraphGate.Judge(new GraphGateFacts(RunId, Graph(), Digest, Held()) { Commit = Tip });

        Assert.Equal(GraphGateStates.Waits, gate.State);
        Assert.StartsWith($"Run {RunId}'s graph waits on `content` (content.holds) before `handed`: Holds the work: {Why}.", gate.Says);
    }

    [Fact]
    public void A_record_keeps_the_reason_and_one_written_without_it_reads_none()
    {
        var records = new NodeRecordFiles(_home, () => true);
        var status = Status("content", NodeStates.Active) with { Holds = Why };
        records.Edit<bool>(Self, record => (record with { Statuses = new Dictionary<string, NodeStatus> { ["content"] = status } }, true));

        Assert.Equal(Why, records.Read(Self)!.Statuses["content"].Holds);

        records.Edit<bool>(Self, record => (record with { Statuses = new Dictionary<string, NodeStatus> { ["content"] = status with { Holds = null } } }, true));
        Assert.Null(records.Read(Self)!.Statuses["content"].Holds);
    }

    private static NodeGraph Graph()
    {
        var (graph, problems) = NodeGraphs.Read(ClaimedContentHanded);
        Assert.True(graph is not null, string.Join("; ", problems.Select(problem => problem.Words)));
        return graph;
    }

    private static NodeStatus Status(string node, string state, string? subject = null, string? revision = null) =>
        new(Self, node, state)
        {
            Attempt = 1,
            Binding = subject is null ? null : new NodeAcceptance(Self, revision, Digest, node, 1, 1) { Subject = subject },
            Acceptance = state == NodeStates.Reached ? new NodeAcceptance(Self, revision, Digest, node, 1, 1) { Subject = subject } : null,
        };

    /// <summary>The run's record with its work reached, `content` held on its branch with its reading's reason, and the landing not reached.</summary>
    private static NodeRecord Held() => new(Self)
    {
        Statuses = new[]
        {
            Status(NodeKinds.Claimed, NodeStates.Reached, "quest:q1", "q1-moment"),
            Status("content", NodeStates.Active, "unit-b", Tip) with { Holds = Why },
            Status("handed", NodeStates.Blocked),
        }.ToDictionary(status => status.Node, StringComparer.Ordinal),
    };

    private static WorkflowRunFacts Facts() =>
        new("engine", new CurrentWorkflow([], [], Digest))
        {
            Quests = [new QuestView("q1", "ask:a1", "engine", "The work", "Body.", "Done")],
            Process = WorkflowProcess.Declared(DriverConfig.Empty, "engine", "default") with
            {
                Binding = new WorkflowRunBinding(RunId, "engine", "default", "docs", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch) { Version = 1, Digest = Digest },
                Run = RunId,
            },
        };
}
