using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's stop of a session awaiting its run's step is their stop of that run, as their own `workflow stop` is.
/// </summary>
public sealed class RunWaitStopsTests : NodeRunFixture
{
    private const string ClaimedThenRelease = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"release","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"release"},{"id":"r>h","kind":"after","from":"release","to":"handed"}]}
        """;

    private readonly List<RunFinish> _finished = [];

    /// <summary>The person's stop over this home's records, as the window's route makes it.</summary>
    private RunWaitStop Stop => new(Files, NodeRuntime.HomeDoor(_home), () => _now);

    private static SessionRecord Waiting(string state = SessionStates.AwaitingStep, string? run = RunId) =>
        new("s-work", "Owner", state) { NoteParts = (run is null ? StepWait.Check : new StepWait(run, "work")).Note().Parts };

    /// <summary>Run <c>q7</c> bound and standing at <c>release</c>, its second attempt under way after its first failed.</summary>
    private void AtRelease()
    {
        BoundTo(ClaimedThenRelease);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId)) with { Executors = ["s-work"] }];
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = new(RunUnit, "claimed", NodeStates.Reached) { Attempt = 1, Tries = 1 },
                ["release"] = new(RunUnit, "release", NodeStates.Active) { Attempt = 2, Tries = 2 },
            },
        });
    }

    private NodeRuntime Started(IReadOnlyList<SessionRecord> sessions)
    {
        var runtime = Runtime();
        runtime.Finish = (end, _) =>
        {
            _finished.Add(end);
            return Task.FromResult<RunEndResponse?>(new RunEndResponse(null, "ended", [.. end.Quests.Select(quest => quest.Id)], end.Sessions));
        };
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(sessions);
        return runtime;
    }

    [Fact]
    public async Task The_person_s_stop_of_a_session_awaiting_its_run_s_step_keeps_the_run_s_node_stop_and_the_run_s_end_ends_the_session()
    {
        AtRelease();

        var stopped = await RunWaitStops.StopAsync(Stop, Waiting());

        Assert.Equal((true, RunId), (stopped!.Done, stopped.Run));
        // Every step it has not passed, as the review's whole stop stops a run: the one it stands at, and the one not reached yet.
        Assert.Equal(["release", "handed"], stopped.Steps);
        Assert.Equal([("release", NodeWordActs.Stop), ("handed", NodeWordActs.Stop)], Files.Read(RunUnit)!.Pending.Select(word => (word.Node, word.Act)));
        Assert.All(Files.Read(RunUnit)!.Pending, word => Assert.Null(word.By));
        Assert.Contains("your stop of it is kept at `release`, `handed`", stopped.Said("s-work"), StringComparison.Ordinal);

        await RunPassAsync(Started([Waiting()]));

        Assert.Equal(NodeStates.Stopped, RunStatus("release").State);
        var finish = Assert.Single(_finished);
        Assert.Equal((RunId, "stopped", "s-work"), (finish.Run, finish.State, Assert.Single(finish.Sessions)));
    }

    [Fact]
    public async Task A_record_waiting_on_no_run_or_not_awaiting_a_step_is_not_this_stop_s()
    {
        AtRelease();

        Assert.Null(await RunWaitStops.StopAsync(Stop, Waiting(run: null)));
        Assert.Null(await RunWaitStops.StopAsync(Stop, Waiting(state: "working")));
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }

    [Fact]
    public async Task A_run_that_cannot_take_the_stop_keeps_nothing_and_says_why()
    {
        AtRelease();

        var unbound = await RunWaitStops.StopAsync(Stop, Waiting(run: "q-gone"));
        Files.Write(Files.Read(RunUnit)! with { Ended = new RunEnd(RunId, _now) { Outcome = RunOutcomes.Completed } });
        var ended = await RunWaitStops.StopAsync(Stop, Waiting());

        Assert.Equal((RunNodeWords.NotRun, RunNodeWords.Ended), (unbound!.Refused, ended!.Refused));
        Assert.Contains("waits on run q-gone, and nothing of it was stopped: no run `q-gone` here", unbound.Said("s-work"), StringComparison.Ordinal);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }
}
