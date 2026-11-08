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

    /// <summary>A look that runs <paramref name="landing"/> once, at the first read of a branch's tip: after the pass applied the words kept before it.</summary>
    private NodeRuntimeLook LandingAtFirstTip(Action landing)
    {
        var look = RunLook();
        var landed = false;
        return look with
        {
            Tips = (root, branches, ct) =>
            {
                if (!landed) landing();
                landed = true;
                return look.Tips(root, branches, ct);
            },
        };
    }

    /// <summary>The person's word kept on the run's record after the pass applied the words it found is applied by the next pass: the run does not end first.</summary>
    [Fact]
    public async Task A_word_kept_while_a_pass_judges_holds_the_runs_end_to_the_next_pass()
    {
        BoundTo(ClaimedThenHanded);
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId], RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId));
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), b1];
        var runtime = Runtime();

        await runtime.PassAsync(LandingAtFirstTip(() => Files.Edit(
            RunUnit, record => (record with { Pending = [new NodeWord("handed", NodeWordActs.Skip, "later", _now)] }, true))), CancellationToken.None);

        var kept = Files.Read(RunUnit)!;
        Assert.Null(kept.Ended);
        Assert.Single(kept.Pending);

        await RunPassAsync(runtime);

        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.Equal(RunId, Files.Read(RunUnit)!.Ended?.Quest);
    }

    private const string ClaimedThenDeploy = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    /// <summary>The person's new attempt on a failure kept for them, given while a pass judges, is applied by the next pass: the run does not end first.</summary>
    [Fact]
    public async Task A_new_attempt_asked_for_while_a_pass_judges_holds_the_runs_end_to_the_next_pass()
    {
        BoundTo(ClaimedThenDeploy);
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), b1];
        var failed = Attempt("deploy", 1, _now, null) with { Code = "boom" };
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["deploy"] = new(RunUnit, "deploy", NodeStates.Failed) { Attempt = 1, Tries = 1, Code = "boom" },
            },
            Attempts = [failed],
        });
        var runtime = Runtime();

        await runtime.PassAsync(LandingAtFirstTip(() => Files.Edit(RunUnit, record =>
            (record with { Attempts = [failed with { Resolution = HeldResolutions.Again, ResolvedAt = _now }] }, true))), CancellationToken.None);

        Assert.Null(Files.Read(RunUnit)!.Ended);
    }

    /// <summary>
    /// A start's ask for an ended run landing while a pass governs, after the pass read which runs were asked for and before it took its asks
    /// off, is kept for the next pass: the pass took off only the asks it read.
    /// </summary>
    [Fact]
    public async Task A_return_landing_while_a_pass_governs_arms_the_ended_run_again()
    {
        BoundTo(ClaimedThenHanded);
        var b1 = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId], RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId));
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), b1];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal(RunId, Files.Read(RunUnit)!.Ended?.Quest);

        // A process started again reads its runs at its first look, the pass this ask lands in.
        runtime = Runtime();
        var look = RunLook();
        var landed = false;
        await runtime.PassAsync(look with
        {
            Steps = () =>
            {
                landed |= runtime.Return(RunId);
                return look.Steps();
            },
        }, CancellationToken.None);
        Assert.True(landed);

        // The chain went to another repository and came back here, as the ask says.
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.QuestUnit(RunHome.Quest("q8", "Done", parent: RunId, to: "other")),
            RunHome.QuestUnit(RunHome.Quest("q9", parent: "q8")), b1];
        var next = await RunPassAsync(runtime, whole: false);

        Assert.True(next.Armed);
        Assert.Contains(RunUnit, next.Evaluated);
    }
}
