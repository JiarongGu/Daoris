using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run that ended before a start, at its graph's end, that a session of this machine still waits on: the first pass after the start
/// governs it for its end alone, which ends that session, and judges nothing else on it; a run nobody waits on is not judged, and one a
/// session waits on that the pass skips anyway says why once.
/// </summary>
public sealed class RunReEndPassTests : NodeRunFixture
{
    private const string ClaimedThenHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    private const string ClaimedThenDeploy = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    private readonly List<RunFinish> _finished = [];

    private readonly List<(string Code, string Said)> _logged = [];

    private IReadOnlyList<SessionRecord>? _sessions = [Waiting()];

    private static SessionRecord Waiting(string state = SessionStates.AwaitingStep) =>
        new("s-work", "Owner", state) { NoteParts = new StepWait(RunId, "work").Note().Parts };

    /// <summary>A process started afresh over the home: its ends written through the service, its session records read, its machine log kept.</summary>
    private NodeRuntime Started()
    {
        var runtime = Runtime();
        runtime.Doors = NodeDoors.None.Saying((code, said) => _logged.Add((code, said)));
        runtime.Finish = (end, _) =>
        {
            _finished.Add(end);
            return Task.FromResult<RunEndResponse?>(new RunEndResponse(null, "ended", [.. end.Quests.Select(quest => quest.Id)], end.Sessions));
        };
        runtime.SessionsRead = _ => Task.FromResult(_sessions);
        return runtime;
    }

    /// <summary>Run <c>q7</c> bound to <paramref name="graph"/>, its record holding its end as an earlier build wrote it, its quest taken.</summary>
    private RunEnd AtItsEnd(string graph, RunEnd ended, params (string Node, string State)[] statuses)
    {
        BoundTo(graph);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId)) with { Executors = ["s-work"] },
            RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId], RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId))];
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = statuses.ToDictionary(each => each.Node, each => new NodeStatus(RunUnit, each.Node, each.State) { Attempt = 1, Tries = 1 }),
            Ended = ended,
        });
        return ended;
    }

    [Fact]
    public async Task A_run_at_its_end_that_a_session_waits_on_ends_it_at_the_first_pass_after_a_start_and_is_not_judged_once_none_waits()
    {
        var ended = AtItsEnd(ClaimedThenHanded, new RunEnd(RunId, _now.AddHours(-3)) { Outcome = RunOutcomes.Completed },
            ("claimed", NodeStates.Reached), ("handed", NodeStates.Reached));
        var runtime = Started();

        var first = await RunPassAsync(runtime);

        var finish = Assert.Single(_finished);
        Assert.Equal((RunId, "completed"), (finish.Run, finish.State));
        Assert.Equal(["s-work"], finish.Sessions);
        Assert.Contains(RunUnit, first.Evaluated);
        Assert.Equal(ended, Files.Read(RunUnit)!.Ended);

        // Its session ended with it: the next look judges the run no more, and sends nothing again.
        _sessions = [Waiting("completed")];
        var next = await RunPassAsync(runtime);

        Assert.Single(_finished);
        Assert.DoesNotContain(RunUnit, next.Evaluated);
    }

    [Fact]
    public async Task A_run_stopped_at_a_standing_step_that_a_session_waits_on_is_ended_again_and_starts_nothing_of_its_steps()
    {
        var ended = AtItsEnd(ClaimedThenDeploy,
            new RunEnd(RunId, _now.AddHours(-3)) { Outcome = RunOutcomes.Stopped, Node = "deploy", By = NodeSource.Daoris, Why = ClosedRunEnds.QuestClosed },
            ("claimed", NodeStates.Reached), ("deploy", NodeStates.Ready));

        await RunPassAsync(Started());

        var finish = Assert.Single(_finished);
        Assert.Equal(("stopped", "s-work"), (finish.State, Assert.Single(finish.Sessions)));
        Assert.Empty(_port.Sent);
        var record = Files.Read(RunUnit)!;
        Assert.Equal((ended, NodeStates.Ready), (record.Ended, record.Statuses["deploy"].State));
        Assert.Empty(record.Attempts);
    }

    [Fact]
    public async Task A_run_at_its_end_that_a_session_waits_on_skipped_for_its_quest_unread_says_why_once()
    {
        AtItsEnd(ClaimedThenHanded, new RunEnd(RunId, _now.AddHours(-3)) { Outcome = RunOutcomes.Completed },
            ("claimed", NodeStates.Reached), ("handed", NodeStates.Reached));
        _units = [.. _units.Where(unit => unit.Kind != WorkUnitKind.Quest)];
        var runtime = Started();

        await RunPassAsync(runtime);
        await RunPassAsync(runtime);

        Assert.Empty(_finished);
        var (code, said) = Assert.Single(_logged);
        Assert.Equal(EmptyRunEnds.Event, code);
        Assert.Contains($"run {RunId}:", said, StringComparison.Ordinal);
        Assert.Contains($"({EmptyRunEnds.QuestUnread})", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Session_records_that_do_not_read_are_said_once_and_end_nothing()
    {
        AtItsEnd(ClaimedThenHanded, new RunEnd(RunId, _now.AddHours(-3)) { Outcome = RunOutcomes.Completed },
            ("claimed", NodeStates.Reached), ("handed", NodeStates.Reached));
        _sessions = null;
        var runtime = Started();

        await RunPassAsync(runtime);
        await RunPassAsync(runtime);

        Assert.Empty(_finished);
        var (code, said) = Assert.Single(_logged);
        Assert.Equal(EmptyRunEnds.Event, code);
        Assert.Contains($"({EmptyRunEnds.SessionsUnread})", said, StringComparison.Ordinal);
    }
}
