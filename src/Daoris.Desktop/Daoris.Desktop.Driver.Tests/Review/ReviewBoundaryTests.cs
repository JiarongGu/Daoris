using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xvi, the Review cuts: what they keep. Each assertion uses only surface present on both sides of the cuts, so the class
/// passes on the move commit as well as after: the chat run's spelling the review's gate reads, the run a chain's work is, the look a named
/// version says, the checks and the long-call bound a run takes, and the words the landing gate keeps of the run's part and the graph's.
/// </summary>
public sealed class ReviewBoundaryTests
{
    private static readonly IReadOnlyList<QuestView> Chain =
    [
        new("q1", "ask #a1", "shop", "First", "", "Done"),
        new("q2", "ask #a1", "web-app", "Second", "", "Done"),
        new("q3", "ask #a1", "web-app", "Third", "", "Open"),
    ];

    [Fact]
    public void A_chat_run_is_named_by_its_prefix_and_the_opinion_due_and_the_gate_s_files_read_it_so()
    {
        Assert.True(new OpinionDue("s1", "q1", "web-app", "work", "/tree", OpinionRules.Landing, DateTimeOffset.UnixEpoch) { Run = "chat-s1" }.OnRun);
        Assert.False(new OpinionDue("s1", "q1", "web-app", "work", "/tree", OpinionRules.Landing, DateTimeOffset.UnixEpoch) { Run = "q1" }.OnRun);
        Assert.False(new OpinionDue("s1", "q1", "web-app", "work", "/tree", OpinionRules.Landing, DateTimeOffset.UnixEpoch).OnRun);

        var home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "review-boundary", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var looks = new ChatLooks(home);
            Assert.Equal(Path.Combine(DaorisHome.Entry(home, HomeEntry.Workflows), "looks", "chat-s1.json"), looks.PathOf("chat-s1"));
            Assert.Null(looks.PathOf("q1"));
        }
        finally { GitSeed.Remove(home); }
    }

    [Fact]
    public void A_chains_run_is_the_first_quest_in_the_repository_and_none_where_it_has_none()
    {
        Assert.Equal("q2", WorkflowRunBindings.RunOf(Chain, "web-app"));
        Assert.Equal("q2", WorkflowRunBindings.RunOf(Chain, "WEB-APP"));
        Assert.Equal("q1", WorkflowRunBindings.RunOf(Chain, "shop"));
        Assert.Null(WorkflowRunBindings.RunOf(Chain, "engine"));
    }

    [Fact]
    public void A_look_a_named_version_says_decides_rows_five_and_six_as_before()
    {
        var none = ReviewGate.Decide([Chain[1]], "web-app", null, null, new WorkflowLook(null, null), "`docs-to-pr` v2");
        Assert.Equal((ReviewLevels.Workflow, (string?)null), (none.Level, none.Environment));
        var looks = ReviewGate.Decide([Chain[1]], "web-app", null, null, new WorkflowLook("dev", null), "`docs-to-pr` v2");
        Assert.Equal((ReviewLevels.Workflow, "dev", "`docs-to-pr` v2"), (looks.Level, looks.Environment, looks.Workflow));
        Assert.Equal(new WorkflowLook("dev", null), new WorkflowLook("dev", null));
    }

    [Fact]
    public void A_run_takes_its_checks_from_the_work_step_of_a_steps_version_and_nothing_from_current()
    {
        var config = DriverConfig.Empty.WithSessionChecks("web-app", SessionChecks.Light);
        var current = WorkflowProcess.Current(config, "web-app", null);
        var named = current with { Binding = new WorkflowRunBinding("q2", "web-app", "default", "docs", "repository", DateTimeOffset.UnixEpoch) { Version = 1 } };
        var heavy = named with { Steps = [new("work", WorkflowKinds.Work, [new("checks", SessionChecks.HeavyAtEnd)])] };

        Assert.Null(current.Claimed);
        Assert.Equal(SessionChecks.HeavyAtEnd, SessionChecks.Of(config, "web-app", heavy));
        Assert.Equal(SessionChecks.Light, SessionChecks.Of(config, "web-app", current));
        Assert.Null(SessionChecks.Of(config, "web-app", named));
        Assert.Null(LongCalls.MinutesOf(named));
        Assert.Null(LongCalls.MinutesOf(null));
    }

    [Fact]
    public void The_landing_gate_says_the_run_s_part_and_the_graph_s_in_its_plan_and_keeps_their_lines_in_the_log()
    {
        var current = WorkflowProcess.Current(DriverConfig.Empty, "web-app", null);
        var named = current with { Binding = new WorkflowRunBinding("q2", "web-app", "default", "docs", "repository", DateTimeOffset.UnixEpoch) { Version = 1 } };
        var cannot = new WorkflowGateState(WorkflowGateStates.CannotStart, named);
        var open = LandingGate.Open("web-app");

        Assert.Empty(LandingGateWords.Plan(open with { Workflow = new WorkflowGateState(WorkflowGateStates.None, current) }));
        var held = open with { Workflow = cannot };
        Assert.Equal([$"trees: {cannot.Says}"], LandingGateWords.Plan(held));
        Assert.False(held.LetsGo);
        Assert.Equal(AutoLandingCode.Refused, held.Refusal!.Refusal);
        var line = LandingGateWords.Held("s1", ("work", "web-app"), held, held.Refusal!, "terminal")!;
        Assert.Equal("workflow.held", line.Event);
        Assert.Contains(("code", (object?)WorkflowGateStates.CannotStart), line.Data);

        var graph = new GraphGateState(GraphGateStates.Waits, "q2") { Handed = "land", Node = "qa", Kind = NodeKinds.Human };
        var waiting = open with { Workflow = new WorkflowGateState(WorkflowGateStates.Follows, named), Graph = graph };
        Assert.Contains($"trees: {graph.Says}", LandingGateWords.Plan(waiting));
        Assert.True(waiting.GraphHolds);
        var graphLine = LandingGateWords.Held("s1", ("work", "web-app"), waiting, waiting.Refusal!, "look")!;
        Assert.Equal(WorkflowLines.GraphHeld("s1", "web-app", "work", named, graph, "look").Data, graphLine.Data);
    }
}
