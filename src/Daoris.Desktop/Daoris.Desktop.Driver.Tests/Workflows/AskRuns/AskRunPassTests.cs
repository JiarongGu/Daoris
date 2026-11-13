using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>An ask's delivery run taken through the runtime's own pass: its subject is the ask's branch, its parts read the train, and its review is the ask's one Accept.</summary>
public sealed class AskRunPassTests : NodeRunFixture
{
    private const string Ask = "a1";
    private const string Branch = "daoris/ask-a1";
    private const string Base = "9999999999999999999999999999999999999999";

    private static readonly string Run = AskRunKey.Of(Ask, RunHome.Repository)!;

    private static readonly string Unit = RunUnits.IdOf(Run);

    private IReadOnlySet<string> _landed = new HashSet<string>(StringComparer.Ordinal);

    private string[] _required = ["p1", "p2"];

    private readonly Dictionary<string, string> _repositories = new(StringComparer.Ordinal) { ["p1"] = RunHome.Repository, ["p2"] = RunHome.Repository, ["p3"] = "elsewhere" };

    private void Bound(string? graph = null) =>
        RunHome.Bind(_home, Run, RunHome.SaveGraph(_home, graph ?? WorkflowDefaults.Of(AskRunBinder.Shipped)!.Graph().ToJsonString()), _now, ask: Ask);

    private NodeRuntimeLook AskLook() => RunLook() with
    {
        Units = _ => Task.FromResult(new NodeUnitsRead(_units, new Dictionary<string, string> { [RunHome.Repository] = "checkout" })
        {
            Parts = new Dictionary<string, PartsReport>
            {
                [Ask] = new(Ask, 1, [.. _required, "p3"], _landed) { Repositories = _repositories },
            },
            AskBranches = new Dictionary<string, AskBranchRead> { [Run] = new(Ask, "main", Base) },
        }),
    };

    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime) => runtime.PassAsync(AskLook(), CancellationToken.None);

    [Fact]
    public async Task The_parts_node_waits_for_the_parts_the_plan_gives_this_repository_and_no_others()
    {
        Bound();
        _units = [];
        _branchTips[Branch] = Tip1;
        _landed = new HashSet<string>(StringComparer.Ordinal) { "p1" };
        var runtime = Runtime();

        await PassAsync(runtime);

        Assert.NotEqual(NodeStates.Reached, Files.Read(Unit)!.Statuses["parts"].State);

        // p3 is another repository's part: it never lands here, and nothing waits for it.
        _landed = new HashSet<string>(StringComparer.Ordinal) { "p1", "p2" };
        await PassAsync(runtime);

        Assert.Equal(NodeStates.Reached, Files.Read(Unit)!.Statuses["parts"].State);
    }

    [Fact]
    public async Task The_check_is_bound_to_the_ask_branchs_tip_once_the_parts_have_landed()
    {
        Bound();
        _units = [];
        _branchTips[Branch] = Tip1;
        _landed = new HashSet<string>(StringComparer.Ordinal) { "p1", "p2" };
        var checks = new Checks();
        var runtime = Runtime();
        runtime.Doors = NodeDoors.None.With(NodeKinds.Check, checks);

        await PassAsync(runtime);
        await PassAsync(runtime);

        var check = Files.Read(Unit)!.Statuses["check"];
        Assert.Equal(Tip1, check.Binding?.SubjectRevision);
        Assert.Equal(AskRunKey.BranchUnit(Run), check.Binding?.Observes);
        Assert.Equal(Tip1, Assert.Single(checks.Sent).Call.SubjectRevision);
    }

    private sealed class Checks : ICheckPort
    {
        public List<NodeAttempt> Sent { get; } = [];

        public Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
        {
            Sent.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct) => Task.FromResult(new WorkReconcileAnswer(ReconcileStates.Running));

        public bool Answered(string invocation) => false;

        public void Stop(string invocation)
        {
        }
    }

    private const string PartsThenReview = """
        {"schemaVersion":2,"entries":["parts"],"end":"line",
         "nodes":[{"id":"parts","kind":"parts","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}},{"id":"line","kind":"line","version":1,"reach":"observed"}],
         "wires":[{"id":"a","kind":"after","from":"parts","to":"review"},{"id":"b","kind":"after","from":"review","to":"landing"},{"id":"c","kind":"after","from":"landing","to":"line"}]}
        """;

    [Fact]
    public async Task The_review_waits_on_the_ask_branch_at_its_tip_against_where_it_left_the_line()
    {
        Bound(PartsThenReview);
        _units = [];
        _branchTips[Branch] = Tip1;
        _landed = new HashSet<string>(StringComparer.Ordinal) { "p1", "p2" };
        var runtime = Runtime();

        await PassAsync(runtime);
        await PassAsync(runtime);

        var review = Files.Read(Unit)!.Statuses["review"];
        Assert.Equal(NodeStates.Active, review.State);
        Assert.Equal(Tip1, review.Binding?.SubjectRevision);
        var record = Assert.Single(ReviewRecords.Of(_home, Run));
        Assert.Equal(ReviewKinds.Branch, record.Kind);
        Assert.Equal(Branch, record.Reference.Branch);
        Assert.Equal(Base, record.Reference.Base);
        Assert.Equal("main", record.Reference.Line);
    }

    [Fact]
    public async Task A_run_whose_nodes_all_reached_ends_naming_its_ask_and_is_not_governed_again()
    {
        Bound(PartsThenReview);
        _units = [];
        _branchTips[Branch] = Tip1;
        var statuses = new[] { "parts", "review", "landing", "line" }
            .ToDictionary(node => node, node => new NodeStatus(Unit, node, NodeStates.Reached), StringComparer.Ordinal);
        Files.Write(new NodeRecord(Unit) { Statuses = statuses });
        var runtime = Runtime();

        await PassAsync(runtime);

        Assert.Equal(Ask, Files.Read(Unit)!.Ended?.Quest);
        Assert.Equal(RunOutcomes.Completed, Files.Read(Unit)!.Ended?.Outcome);
    }
}
