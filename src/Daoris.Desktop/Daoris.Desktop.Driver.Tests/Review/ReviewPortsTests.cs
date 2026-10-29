using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xvi, the Review cuts: the seams they add. The workflows' process and gate parts being the facets the review reads, the
/// parts cast back for a reader above, the line each part keeps in the machine log, the participants' port answering the process of a run
/// that has none named, and the workplace's chat press answering the line a capture reads. What the cuts keep is
/// <c>ReviewBoundaryTests</c>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class ReviewPortsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "review-ports", Guid.NewGuid().ToString("N")[..8]);

    public ReviewPortsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private static WorkflowProcess Named() =>
        WorkflowProcess.Current(DriverConfig.Empty, "web-app", null) with
        {
            Binding = new WorkflowRunBinding("q2", "web-app", "default", "docs", "repository", DateTimeOffset.UnixEpoch) { Version = 1 },
        };

    [Fact]
    public void The_workflows_process_and_gate_parts_are_the_facets_the_review_reads()
    {
        Assert.True(typeof(IReviewedProcess).IsAssignableFrom(typeof(WorkflowProcess)));
        Assert.True(typeof(ICheckedProcess).IsAssignableFrom(typeof(WorkflowProcess)));
        Assert.True(typeof(IWorkflowGatePart).IsAssignableFrom(typeof(WorkflowGateState)));
        Assert.True(typeof(IGraphGatePart).IsAssignableFrom(typeof(GraphGateState)));

        var named = Named() with { Look = new WorkflowLook("dev", null), Steps = [new("work", WorkflowKinds.Work, [new("checks", SessionChecks.Light)])] };
        IReviewedProcess reviewed = named;
        ICheckedProcess checks = named;
        Assert.Same(named.Review, reviewed.Review);
        Assert.Equal(new WorkflowLook("dev", null), reviewed.Look);
        Assert.Equal((true, SessionChecks.Light, (IReadOnlyDictionary<string, string>?)null), (checks.Named, checks.WorkChecks, checks.Claimed));
        Assert.Null(WorkflowProcess.Current(DriverConfig.Empty, "web-app", null).WorkChecks);
    }

    [Fact]
    public void The_run_s_part_says_what_it_holds_and_the_line_the_log_keeps_of_a_landing_it_held()
    {
        var named = Named();
        IWorkflowGatePart none = new WorkflowGateState(WorkflowGateStates.None, named);
        IWorkflowGatePart follows = new WorkflowGateState(WorkflowGateStates.Follows, named);
        IWorkflowGatePart cannot = new WorkflowGateState(WorkflowGateStates.CannotStart, named);

        Assert.Equal((false, true, true, false), (none.Speaks, follows.Speaks, follows.LetsGo, cannot.LetsGo));
        Assert.Same(named, cannot.Process);
        var held = cannot.Held("s1", "web-app", "work", "screen");
        Assert.Equal(WorkflowLines.Held("s1", "web-app", "work", (WorkflowGateState)cannot, "screen").Data, held.Data);
        Assert.Contains(("step", (object?)null), held.Data);
        // A part that lets the work go but whose landing step cannot start is told as the landing step's.
        var landing = follows.Held("s1", "web-app", "work", "look");
        Assert.Contains(("step", (object?)WorkflowKinds.Landing), landing.Data);
        Assert.Contains(("code", (object?)WorkflowGateStates.CannotStart), landing.Data);
    }

    [Fact]
    public void The_graph_s_part_keeps_its_line_for_the_process_it_is_handed_and_a_reader_above_casts_the_parts_back()
    {
        var named = Named();
        var graph = new GraphGateState(GraphGateStates.Waits, "q2") { Handed = "land", Node = "qa", Kind = NodeKinds.Human };
        IGraphGatePart part = graph;
        Assert.Equal((false, true, graph.Says), (part.LetsGo, part.AsksPass, part.Says));
        Assert.Equal(WorkflowLines.GraphHeld("s1", "web-app", "work", named, graph, "terminal").Data, part.Held("s1", "web-app", "work", named, "terminal").Data);
        Assert.Equal(WorkflowLines.GraphHeld("s1", "web-app", "work", null, graph, "terminal").Data, part.Held("s1", "web-app", "work", null, "terminal").Data);

        var run = new WorkflowGateState(WorkflowGateStates.Follows, named);
        var gate = LandingGate.Open("web-app");
        Assert.Null(gate.WorkflowPart());
        Assert.Null(gate.GraphPart());
        var read = gate with { Workflow = run, Graph = graph };
        Assert.Same(run, read.WorkflowPart());
        Assert.Same(graph, read.GraphPart());
        Assert.Same(named, read.Process);
    }

    [Fact]
    public void A_chains_run_is_the_reviews_and_the_workflows_name_answers_through_it()
    {
        IReadOnlyList<QuestView> chain = [new("q1", "ask #a1", "shop", "First", "", "Done"), new("q2", "ask #a1", "web-app", "Second", "", "Done")];
        Assert.Equal("q2", ReviewGate.RunOf(chain, "WEB-APP"));
        Assert.Null(ReviewGate.RunOf(chain, "engine"));
        Assert.Equal(WorkflowRunBindings.RunOf(chain, "shop"), ReviewGate.RunOf(chain, "shop"));
    }

    [Fact]
    public void The_installed_participants_port_reads_the_process_of_a_run_with_none_named_as_the_rules_live()
    {
        Assert.IsType<ParticipantsWorkflowsOperations>(ParticipantsWorkflows.Current);
        var process = ParticipantsWorkflows.Current.ReadProcess(_home, DriverConfig.Empty, "web-app", null, null);
        var read = WorkflowProcesses.Read(_home, DriverConfig.Empty, "web-app", null, null);
        Assert.False(process.Named);
        Assert.Equal((read.Name, read.Named, read.Problem, read.Unread), (process.Name, process.Named, process.Problem, process.Unread));
    }

    [Fact]
    public void The_workplace_s_chat_press_answers_the_line_a_capture_reads_and_its_own_id_for_a_record_it_does_not_hold()
    {
        var (line, live) = WorkplaceWorkflows.Current.ChatPressOf("S9", "[]");
        Assert.Equal(["S9"], line);
        Assert.True(live);
        Assert.Equal(ChatRuns.PressOf("S9", "[]").Line, line);
    }
}
