using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A graph run is over when nothing in it can move, whatever its end node says, and its end keeps how it came out: a step after the end
/// still runs, and a failure nothing goes on from ends the run failed, naming the step.
/// </summary>
public sealed class RunCompletionTests : NodeRunFixture
{
    /// <summary>A release's shape: the work lands and its plugin's step after the landing, the graph's end, still to be seen.</summary>
    private const string LandThenDeployed = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"},
                  {"id":"deployed","kind":"acme.deploy/deployed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"},{"id":"h>d","kind":"after","from":"handed","to":"deployed"}]}
        """;

    private const string DeployThenLand = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    private void Landed()
    {
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId], RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId));
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), b1];
    }

    [Fact]
    public async Task A_step_after_the_graphs_end_still_runs_and_the_run_ends_once_it_settles()
    {
        BoundTo(LandThenDeployed);
        Landed();
        var runtime = Runtime();

        await RunPassAsync(runtime);

        Assert.Equal(NodeStates.Reached, RunStatus("handed").State);
        Assert.Equal(NodeStates.Active, RunStatus("deployed").State);
        Assert.Null(Files.Read(RunUnit)!.Ended);

        Assert.True(RunNodeWords.Word(Files, RunId, "deployed", NodeWordActs.Skip, "no deploy this time", () => _now).Done);
        await RunPassAsync(runtime);

        var ended = Files.Read(RunUnit)!.Ended;
        Assert.Equal((RunId, RunOutcomes.Completed), (ended?.Quest, ended?.Outcome));
    }

    [Fact]
    public async Task A_failure_nothing_goes_on_from_ends_the_run_failed_naming_its_step()
    {
        BoundTo(DeployThenLand);
        Landed();
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus> { ["deploy"] = new(RunUnit, "deploy", NodeStates.Failed) { Attempt = 1, Tries = 1, Code = "boom", By = NodeSource.Plugin } },
        });

        await RunPassAsync(Runtime());

        var ended = Files.Read(RunUnit)!.Ended;
        Assert.Equal((RunOutcomes.Failed, "deploy"), (ended?.Outcome, ended?.Node));
    }

    [Fact]
    public async Task A_persons_stop_ends_the_run_stopped_saying_whose()
    {
        BoundTo(DeployThenLand);
        Landed();
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus> { ["deploy"] = new(RunUnit, "deploy", NodeStates.Stopped) { Attempt = 1, By = NodeSource.Person, Words = "not now" } },
        });

        await RunPassAsync(Runtime());

        var ended = Files.Read(RunUnit)!.Ended;
        Assert.Equal((RunOutcomes.Stopped, "deploy", NodeSource.Person), (ended?.Outcome, ended?.Node, ended?.By));
    }
}
