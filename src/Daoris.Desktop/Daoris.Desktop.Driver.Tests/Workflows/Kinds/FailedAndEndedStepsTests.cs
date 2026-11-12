using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A step of an ended run never reads as working, and a failed step always has the person's stop: it ends the step and its run as
/// stopped with the caller's words, through the one door the window, Ask Daoris and the terminal all press.
/// </summary>
public sealed class FailedAndEndedStepsTests : NodeRunFixture
{
    private const string Digest = "gph-0123456789abcdef0123456789abcdef";

    private const string Graph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"deploy",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deploy","kind":"acme.deploy/pull-request","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deploy"}]}
        """;

    private static readonly NodeDeclaration Deploy = new("deploy", "acme.deploy/pull-request", 1, NodeReach.Performed);

    private NodeRecord Record(string deploy, RunEnd? ended = null) => new(RunUnit)
    {
        Ended = ended,
        Statuses = new Dictionary<string, NodeStatus>
        {
            ["claimed"] = new(RunUnit, "claimed", NodeStates.Reached) { Attempt = 1 },
            ["deploy"] = new(RunUnit, "deploy", deploy) { Attempt = 1, Tries = 1, Code = deploy == NodeStates.Failed ? "no-token" : null },
        },
        Attempts = deploy != NodeStates.Failed ? [] :
        [
            new("deploy", 1, Plugin, new WorkPerformCall("unit-b1", Plugin + "/pull-request", 1, "i-1", "k-1", Tip1, [], _now.AddMinutes(15)), AttemptStates.Settled)
            {
                RecordedAt = _now, Generation = 1, Deliveries = 1, Outcome = WorkStates.Failed, Code = "no-token",
            },
        ],
    };

    private static WorkflowRunStep StepOf(NodeRecord record)
    {
        var (graph, problems) = NodeGraphs.Read(Graph);
        Assert.True(graph is not null, string.Join("; ", problems.Select(problem => problem.Words)));
        var facts = new WorkflowRunFacts("engine", new CurrentWorkflow([], [], Digest))
        {
            Process = WorkflowProcess.Declared(DriverConfig.Empty, "engine", "default") with
            {
                Binding = new WorkflowRunBinding(RunId, "engine", "default", "docs", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch) { Version = 1, Digest = Digest },
                Run = RunId,
            },
        };
        return WorkflowGraphRuns.Derive(new WorkflowGraphRunFacts(facts, RunId, graph, record), "default").Steps.Single(each => each.Step.Id == "deploy");
    }

    [Theory]
    [InlineData(NodeStates.Active)]
    [InlineData(NodeStates.Ready)]
    [InlineData(NodeStates.Invalidated)]
    public void A_step_of_an_ended_run_never_reads_as_working(string state)
    {
        var step = StepOf(Record(state, new RunEnd(RunId, _now)));

        Assert.Equal(WorkflowRunStates.NotReached, step.State);
        Assert.Equal(WorkflowRunStates.Working, StepOf(Record(state)).State);
    }

    [Fact]
    public void A_failed_step_offers_its_stop()
    {
        Assert.Contains(NodeWordActs.Stop, RunNodeWords.Acts(Deploy, new(RunUnit, "deploy", NodeStates.Failed)));
    }

    [Fact]
    public async Task The_stop_of_the_step_a_run_ended_failed_at_ends_the_run_again_as_stopped()
    {
        BoundTo(Graph);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        _branchTips["daoris/s1"] = Tip1;
        Files.Write(Record(NodeStates.Failed, new RunEnd(RunId, _now) { Outcome = RunOutcomes.Failed, Node = "deploy" }));

        Assert.Equal(RunNodeWords.Ended, RunNodeWords.Word(Files, RunId, "claimed", NodeWordActs.Stop, null, () => _now).Refused);
        var worded = RunNodeWords.Word(Files, RunId, "deploy", NodeWordActs.Stop, "not needed", () => _now);
        Assert.True(worded.Done, worded.Refused);
        await RunPassAsync(Runtime(), whole: false);

        Assert.Equal(NodeStates.Stopped, RunStatus("deploy").State);
        var ended = Files.Read(RunUnit)!.Ended;
        Assert.Equal((RunOutcomes.Stopped, "deploy"), (ended?.Outcome, ended?.Node));
    }

    [Fact]
    public async Task The_stop_of_a_failed_step_ends_it_and_its_run_as_stopped_with_the_callers_words()
    {
        BoundTo(Graph);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        _branchTips["daoris/s1"] = Tip1;
        Files.Write(Record(NodeStates.Failed));

        var worded = RunNodeWords.Word(Files, RunId, "deploy", NodeWordActs.Stop, "the pull request was opened by hand", () => _now);
        Assert.True(worded.Done, worded.Refused);
        await RunPassAsync(Runtime(), whole: false);

        var deploy = RunStatus("deploy");
        Assert.Equal((NodeStates.Stopped, NodeSource.Person, "the pull request was opened by hand"), (deploy.State, deploy.By, deploy.Words));
        var ended = Files.Read(RunUnit)!.Ended;
        Assert.Equal((RunOutcomes.Stopped, "deploy"), (ended?.Outcome, ended?.Node));
    }
}
