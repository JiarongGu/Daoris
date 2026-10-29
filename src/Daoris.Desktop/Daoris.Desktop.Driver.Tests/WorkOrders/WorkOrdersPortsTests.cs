using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xv, the Work orders cuts: the seams they add. The installed Setup and Workflows operations being the owners' and doing
/// what the room, the ask's door, the intake's kinds and the work inventory did, a supplied stand-in used where each is reached, the tick
/// report's judged opinions being the review's, and a missing operation required only where it is reached. What the cuts keep is
/// <c>WorkOrdersBoundaryTests</c>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class WorkOrdersPortsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "work-orders-ports", Guid.NewGuid().ToString("N")[..8]);

    public WorkOrdersPortsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private static readonly IReadOnlyList<DeclarationView> Adopted =
    [
        new("engine", Adopted: true, Registered: true, "The engine.", ["the core"], [], Root: null),
    ];

    private static readonly IReadOnlyList<DeclarationView> WithNewcomer =
    [
        new("engine", Adopted: true, Registered: true, "The engine.", ["the core"], [], Root: null),
        new("newcomer", Adopted: false, Registered: true, null, [], [], Root: "/work/newcomer"),
    ];

    [Fact]
    public void The_installed_operations_are_the_owners_and_the_types_the_room_and_the_report_hold_answer_the_facets()
    {
        Assert.IsType<WorkOrdersSetupOperations>(WorkOrdersSetup.Current);
        Assert.IsType<WorkOrdersWorkflowsOperations>(WorkOrdersWorkflows.Current);
        Assert.True(typeof(IIntakeDescription).IsAssignableFrom(typeof(RepositoryDescription)));
        Assert.True(typeof(IJudgedOpinion).IsAssignableFrom(typeof(OpinionJudged)));

        var judged = new OpinionJudged("s1", "q1", "engine", "work", DateTimeOffset.UnixEpoch, Auto: true, new OpinionGateState("disputed", "engine"));
        IJudgedOpinion opinion = judged;
        Assert.Equal(("s1", "q1", "engine", "work", DateTimeOffset.UnixEpoch, true),
            (opinion.Session, opinion.Quest, opinion.Repository, opinion.Workspace, opinion.Since, opinion.Auto));
        var report = new TickReport([], [], false, [], []) { JudgedOpinions = [judged] };
        Assert.Same(judged, Assert.Single(report.JudgedOpinions));
    }

    [Fact]
    public void The_installed_setup_reads_a_checkouts_own_files_as_the_room_did_and_says_nothing_of_one_not_there()
    {
        var root = Path.Combine(_home, "atlas");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "README.md"), "# Atlas\n\nA tool that sorts maps.\n");

        var said = WorkOrdersSetup.Current.Describe(root);
        var read = SelfDescriptionFiles.Read(root);
        Assert.NotNull(said);
        Assert.Equal((read!.Summary, read.Source), (said!.Summary, said.Source));
        Assert.Equal(read.Stack, said.Stack);
        Assert.Null(WorkOrdersSetup.Current.Describe(Path.Combine(_home, "nowhere")));
    }

    [Fact]
    public void A_supplied_setup_is_what_the_room_asks_for_a_repository_that_declared_nothing_and_no_other()
    {
        var actual = WorkOrdersSetup.Current;
        try
        {
            var stand = new StandInSetup();
            WorkOrdersSetup.Current = stand;
            var room = IntakeRoom.Prepare(_home, "work", WithNewcomer);

            Assert.Equal(["/work/newcomer"], stand.Roots);
            Assert.Contains("its NOTES says: Stood in.", File.ReadAllText(Path.Combine(room, "AGENTS.md")));

            stand.Roots.Clear();
            IntakeRoom.Prepare(_home, "work", Adopted);
            Assert.Empty(stand.Roots);
        }
        finally { WorkOrdersSetup.Current = actual; }
    }

    [Fact]
    public void A_supplied_workflows_operation_is_what_the_obligations_the_ask_and_the_kinds_are_read_through()
    {
        var actual = WorkOrdersWorkflows.Current;
        try
        {
            var stand = new StandInWorkflows();
            WorkOrdersWorkflows.Current = stand;

            Assert.Same(stand.Owed, WorkObligations.Read(_home, DateTimeOffset.UnixEpoch));
            Assert.Same(stand.Owes, WorkObligations.Owing(_home));

            var world = new AskWorkflowWorld(null!, () => DriverConfig.Empty, _home);
            Assert.Equal("stood in: docs-to-pr", AskWorkflowCommand.Unchoosable(null, "docs-to-pr", "work", world));
            Assert.Equal([_home], stand.Unusables);
            // current is no saved workflow, and none named is nothing to judge: neither reaches the operation
            Assert.Null(AskWorkflowCommand.Unchoosable(null, "current", "work", world));
            Assert.Null(AskWorkflowCommand.Unchoosable(null, null, "work", world));
            Assert.Single(stand.Unusables);

            var config = WorkflowSelection.Apply(DriverConfig.Empty, new WorkflowDeclare("docs", "work", "Documentation", ["docs/**"]));
            config = WorkflowSelection.Apply(config, new WorkflowDeclare("feature", "work", "Feature", []));
            var read = Assert.Single(IntakeKinds.Read(_home, config, "work", ["engine"]));
            Assert.Equal(("engine", "`stand` v1"), (read.Repository, read.Otherwise));
            Assert.Equal(["docs", "feature"], stand.KindsAsked);
            Assert.Equal([("docs", "`first` v2", "drops a step"), ("feature", "`second` v1", null)],
                read.Kinds.Select(kind => (kind.Kind, kind.Workflow, kind.Lowers is { } lowers ? string.Join(",", lowers) : null)));
        }
        finally { WorkOrdersWorkflows.Current = actual; }
    }

    [Fact]
    public void The_installed_workflows_say_a_workflow_not_saved_here_as_the_door_did_and_a_home_with_no_records_owes_nothing()
    {
        Assert.Equal("No workflow `docs-to-pr` is saved here: `daoris driver workflow list` names them. Nothing was kept.",
            WorkOrdersWorkflows.Current.Unusable(_home, "docs-to-pr"));
        Assert.Empty(WorkOrdersWorkflows.Current.Obligations(_home, DateTimeOffset.UnixEpoch));
        Assert.Empty(WorkOrdersWorkflows.Current.Owing(_home));
        Assert.Equal(WorkflowObligations.Read(_home, DateTimeOffset.UnixEpoch).Count, WorkObligations.Read(_home, DateTimeOffset.UnixEpoch).Count);
    }

    [Fact]
    public void A_missing_operation_is_required_only_where_it_is_reached()
    {
        var setup = WorkOrdersSetup.Current;
        var workflows = WorkOrdersWorkflows.Current;
        try
        {
            WorkOrdersSetup.Current = null!;
            WorkOrdersWorkflows.Current = null!;
            Assert.Contains("Setup installs them", Assert.Throws<InvalidOperationException>(() => WorkOrdersSetup.Current).Message);
            Assert.Contains("Workflows installs them", Assert.Throws<InvalidOperationException>(() => WorkOrdersWorkflows.Current).Message);

            // A room for repositories that all declared reaches no Setup; a workspace declaring no kind reaches no Workflows.
            IntakeRoom.Prepare(_home, "work", Adopted);
            Assert.Empty(IntakeKinds.Read(_home, DriverConfig.Empty, "work", ["engine"]));
            var world = new AskWorkflowWorld(null!, () => DriverConfig.Empty, _home);
            Assert.Null(AskWorkflowCommand.Unchoosable(null, "current", "work", world));

            // Where reached, each says so and the failure propagates.
            Assert.Throws<InvalidOperationException>(() => IntakeRoom.Prepare(_home, "work", WithNewcomer));
            Assert.Throws<InvalidOperationException>(() => WorkObligations.Read(_home, DateTimeOffset.UnixEpoch));
            Assert.Throws<InvalidOperationException>(() => WorkObligations.Owing(_home));
            Assert.Throws<InvalidOperationException>(() => AskWorkflowCommand.Unchoosable(null, "docs-to-pr", "work", world));
        }
        finally
        {
            WorkOrdersSetup.Current = setup;
            WorkOrdersWorkflows.Current = workflows;
        }
    }

    private sealed class StandInSetup : IWorkOrdersSetup
    {
        public List<string> Roots { get; } = [];

        public IIntakeDescription? Describe(string root)
        {
            Roots.Add(root);
            return new RepositoryDescription("Stood in.", ["Go"], "NOTES");
        }
    }

    private sealed class StandInWorkflows : IWorkOrdersWorkflows
    {
        public IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> Owed { get; } = new Dictionary<string, IReadOnlyList<InventoryEntry>>();

        public IReadOnlySet<string> Owes { get; } = new HashSet<string> { "quest:q1" };

        public List<string> Unusables { get; } = [];

        public List<string> KindsAsked { get; } = [];

        public IReadOnlyDictionary<string, IReadOnlyList<InventoryEntry>> Obligations(string home, DateTimeOffset now) => Owed;

        public IReadOnlySet<string> Owing(string home) => Owes;

        public string? Unusable(string home, string workflow)
        {
            Unusables.Add(home);
            return $"stood in: {workflow}";
        }

        public (string Otherwise, IReadOnlyList<(string Name, IReadOnlyList<string>? Lowers)> Kinds) KindsOf(
            string home, DriverConfig config, string repository, string workspace, IReadOnlyList<string> kinds)
        {
            KindsAsked.AddRange(kinds);
            return ("`stand` v1", [("`first` v2", ["drops a step"]), ("`second` v1", null)]);
        }
    }
}
