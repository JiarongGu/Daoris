using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run whose quest closed while a step of it still stands, judged through whole passes: it ends where none of those steps can move
/// again (its branch gone, or the line holding its bound work), keeping the steps as they stood, and once ended no pass pays for it.
/// </summary>
public sealed class ClosedRunEndTests : NodeRunFixture
{
    private const string ClaimedThenHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    private readonly HashSet<string> _onLine = new(StringComparer.Ordinal);

    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime) =>
        runtime.PassAsync(RunLook() with { OnLine = (_, _, _, tip, _) => Task.FromResult<bool?>(_onLine.Contains(tip)) }, CancellationToken.None);

    /// <summary>The landing a run waits on, active at <paramref name="tip"/> on <paramref name="branch"/>'s unit: what an old landing left.</summary>
    private void LandingActiveAt(string tip, string branch = "unit-b1") => Files.Write(new NodeRecord(RunUnit)
    {
        Statuses = new Dictionary<string, NodeStatus>
        {
            ["handed"] = new(RunUnit, "handed", NodeStates.Active)
            {
                Attempt = 1, Tries = 1, Binding = new NodeAcceptance(RunUnit, tip, "gph", "handed", 1, 1) { Subject = branch },
            },
        },
    });

    [Fact]
    public async Task A_done_quests_run_whose_branch_is_gone_ends_keeping_its_steps_as_they_stood()
    {
        BoundTo(ClaimedThenHanded);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done"))];
        var runtime = Runtime();

        await PassAsync(runtime);

        var kept = Files.Read(RunUnit)!;
        Assert.Equal(RunId, kept.Ended?.Quest);
        Assert.Equal(ClosedRunEnds.BranchGone, kept.Ended?.Why);
        Assert.Contains(kept.Statuses["handed"].State, NodeStates.Live);
    }

    [Fact]
    public async Task A_done_quests_run_whose_branch_is_recorded_gone_ends()
    {
        BoundTo(ClaimedThenHanded);
        var gone = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]);
        gone = gone with { Records = gone.Records with { Branch = gone.Records.Branch! with { GoneAt = _now } } };
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), gone];

        await PassAsync(Runtime());

        Assert.Equal(ClosedRunEnds.BranchGone, Files.Read(RunUnit)!.Ended?.Why);
    }

    [Fact]
    public async Task A_taken_quests_run_whose_branch_is_gone_goes_on()
    {
        BoundTo(ClaimedThenHanded);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId))];

        await PassAsync(Runtime());

        Assert.Null(Files.Read(RunUnit)!.Ended);
    }

    [Fact]
    public async Task A_done_quests_run_whose_branch_stands_goes_on()
    {
        BoundTo(ClaimedThenHanded);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        LandingActiveAt(Tip1);

        await PassAsync(Runtime());

        Assert.Null(Files.Read(RunUnit)!.Ended);
    }

    [Fact]
    public async Task A_done_quests_run_whose_landing_the_line_holds_by_content_ends_though_its_branch_stands()
    {
        BoundTo(ClaimedThenHanded);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        LandingActiveAt(Tip1);
        _onLine.Add(Tip1);

        await PassAsync(Runtime());

        var kept = Files.Read(RunUnit)!;
        Assert.Equal(ClosedRunEnds.OnLine, kept.Ended?.Why);
        Assert.Equal(NodeStates.Active, kept.Statuses["handed"].State);
    }

    [Fact]
    public async Task A_done_quests_run_waiting_on_the_persons_go_ahead_goes_on()
    {
        BoundTo(GoAheadBeforeHanded);
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done"))];

        await PassAsync(Runtime());

        Assert.Null(Files.Read(RunUnit)!.Ended);
    }

    /// <summary>Hundreds of such runs end over the passes a turn bounds, and once ended a whole pass judges none of them.</summary>
    [Fact]
    public async Task Hundreds_of_done_quests_runs_whose_branches_are_gone_end_and_cost_no_pass_after()
    {
        var digest = RunHome.SaveGraph(_home, ClaimedThenHanded);
        var runs = Enumerable.Range(0, 300).Select(each => $"r{each:D3}").ToList();
        foreach (var run in runs) RunHome.Bind(_home, run, digest, _now);
        _units = [.. runs.Select(run => RunHome.QuestUnit(RunHome.Quest(run, "Done")))];
        var runtime = Runtime();

        for (var pass = 0; pass < 20 && runs.Any(run => Files.Read(RunUnits.IdOf(run))?.Ended is null); pass++) await PassAsync(runtime);

        Assert.All(runs, run => Assert.Equal(ClosedRunEnds.BranchGone, Files.Read(RunUnits.IdOf(run))?.Ended?.Why));
        var after = await PassAsync(runtime);
        Assert.DoesNotContain(after.Evaluated, unit => RunUnits.Is(unit));
    }
}
