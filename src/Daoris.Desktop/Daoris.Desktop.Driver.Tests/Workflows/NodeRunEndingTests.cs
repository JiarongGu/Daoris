using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What ends a graph run, and what arms an ended one again: the retry delay a moved node leaves behind.</summary>
public sealed class NodeRunEndingTests : NodeRunFixture
{
    private const string ClaimedThenHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    private static NodeAttempt Attempt(string node, int number, DateTimeOffset at, DateTimeOffset? retryAt) =>
        new(node, number, Plugin, new WorkPerformCall("unit-b1", Plugin + "/deploy", 1, $"i-{number}", $"k-{number}", Tip1, [], at.AddMinutes(15)),
            AttemptStates.Settled)
        {
            RecordedAt = at, Generation = 1, Deliveries = 1, Outcome = WorkStates.Failed, Code = "unavailable", RetryAt = retryAt,
        };

    /// <summary>A node's older attempt that kept its retry delay when the tip moved and a new attempt began holds the run's end no longer.</summary>
    [Fact]
    public async Task A_retry_delay_an_attempt_kept_after_its_node_moved_on_does_not_hold_the_runs_end()
    {
        BoundTo(ClaimedThenHanded);
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId], RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId));
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), b1];
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus> { ["handed"] = new(RunUnit, "handed", NodeStates.Reached) { Attempt = 2, Tries = 2 } },
            Attempts = [Attempt("handed", 1, _now, _now.AddHours(2)), Attempt("handed", 2, _now.AddMinutes(1), null)],
        });
        var runtime = Runtime();

        await RunPassAsync(runtime);

        Assert.Equal(RunId, Files.Read(RunUnit)!.Ended?.Quest);
        Assert.False((await RunPassAsync(runtime)).Armed);
        Assert.Null(runtime.NextDue);
    }
}
