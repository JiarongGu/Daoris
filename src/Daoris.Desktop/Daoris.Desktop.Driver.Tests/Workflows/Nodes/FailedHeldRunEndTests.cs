using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run that ended failed at a step its holder may still settle (<c>failed-held</c>) is still observed at that step where its kind reads a
/// failed attempt: a <c>push</c> refused whose branch origin then holds at the landed tip, pushed by the person's own push, reads reached and the
/// run goes on to its end; at another tip it stays as it ended, its end not written again. Seen failing first: the run stayed ended, since
/// only runs still governed were observed.
/// </summary>
public sealed class FailedHeldRunEndTests : NodeRunFixture
{
    private const string LandedThenPushed = """
        {"schemaVersion":2,"entries":["claimed"],"end":"push",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"},
                  {"id":"push","kind":"push","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"},{"id":"h>p","kind":"after","from":"handed","to":"push"}]}
        """;

    /// <summary>What origin's remote-tracking refs hold, by branch, as the look reads them.</summary>
    private readonly Dictionary<string, string> _origin = new(StringComparer.Ordinal);

    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime) => runtime.PassAsync(RunLook() with
    {
        Remotes = (_, branches, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(
            branches.Where(_origin.ContainsKey).ToDictionary(branch => branch, branch => _origin[branch], StringComparer.Ordinal)),
    }, CancellationToken.None);

    /// <summary>The run landed at <see cref="NodeRunFixture.Tip1"/>, its push failed <paramref name="code"/> (a refusal: a passing tree clean-up), and the run ended failed there.</summary>
    private async Task<NodeRuntime> EndedFailedAtItsPushAsync(string code = PushDoor.Refused)
    {
        BoundTo(LandedThenPushed);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId], RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId))];
        var runtime = Runtime();
        await PassAsync(runtime);
        await PassAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (RunStatus("handed").State, RunStatus("push").State));

        // The push door's refusal, written on the record off the pass.
        Files.Edit(RunUnit, record => (record with
        {
            Statuses = new Dictionary<string, NodeStatus>(record.Statuses) { ["push"] = record.Statuses["push"] with { State = NodeStates.Failed, Code = code, Unreconciled = null } },
        }, true));
        await PassAsync(runtime);
        var ended = Files.Read(RunUnit)!.Ended;
        Assert.Equal((RunOutcomes.Failed, "push"), (ended?.Outcome, ended?.Node));
        return runtime;
    }

    [Fact]
    public async Task A_run_ended_failed_at_a_refused_push_goes_on_once_origin_holds_its_branch_at_the_landed_tip()
    {
        var runtime = await EndedFailedAtItsPushAsync();
        _origin["feature/q7"] = Tip1;

        await PassAsync(runtime);

        Assert.Equal((NodeStates.Reached, NodeSource.Git), (RunStatus("push").State, RunStatus("push").By));
        var ended = Files.Read(RunUnit)!.Ended;
        Assert.Equal((RunOutcomes.Completed, RunId), (ended?.Outcome, ended?.Quest));
    }

    [Fact]
    public async Task A_run_ended_failed_at_a_refused_push_is_observed_after_a_restart_too()
    {
        await EndedFailedAtItsPushAsync();
        _origin["feature/q7"] = Tip1;

        await PassAsync(Runtime());

        Assert.Equal(NodeStates.Reached, RunStatus("push").State);
        Assert.Equal(RunOutcomes.Completed, Files.Read(RunUnit)!.Ended?.Outcome);
    }

    [Fact]
    public async Task At_another_tip_the_run_stays_as_it_ended_and_its_end_is_not_written_again()
    {
        var runtime = await EndedFailedAtItsPushAsync();
        var ended = Files.Read(RunUnit)!.Ended;
        _origin["feature/q7"] = Tip2;
        _now = _now.AddMinutes(5);

        await PassAsync(runtime);

        Assert.Equal((NodeStates.Failed, PushDoor.Refused), (RunStatus("push").State, RunStatus("push").Code));
        Assert.Equal(ended, Files.Read(RunUnit)!.Ended);
    }

    /// <summary>
    /// The left step is the person's: one wait, marked as its run's end (placed whatever its quest's state, <c>SessionStatesTests</c>), and one
    /// `step` row on the line's lead with <i>Try again</i>, <i>Done outside Daoris…</i> (a push's proof is origin's ref) and <i>End this
    /// work…</i>. Seen failing first: no row carried it.
    /// </summary>
    [Fact]
    public async Task The_step_a_run_ended_failed_at_is_one_wait_and_one_row_with_its_presses()
    {
        await EndedFailedAtItsPushAsync();

        var wait = Assert.Single(PersonWaitReader.Read(_home));
        Assert.Equal((RunId, "push", PersonWaits.Left, true), (wait.Run, wait.Node, wait.Why, wait.Ended));
        var row = Assert.Single(LeftSteps.Of(new GraphRuns(Files).Read(), Files.Read));
        Assert.Equal((PersonWaits.Left, AppointedRoles.Supervisor, RunId), (row.Why, row.Role, row.Work));
        Assert.Equal([NodeWordActs.Again, NodeWordActs.DoneOutside, NodeWordActs.Stop], row.Presses);
    }

    [Fact]
    public async Task Try_again_on_it_governs_the_run_again_as_a_new_attempt()
    {
        var runtime = await EndedFailedAtItsPushAsync();

        var worded = RunNodeWords.Word(Files, RunId, "push", NodeWordActs.Again, null, () => _now);
        Assert.True(worded.Done, worded.Refused);
        Assert.Null(Files.Read(RunUnit)!.Ended);
        Assert.Empty(LeftSteps.Of(new GraphRuns(Files).Read(), Files.Read));
        await PassAsync(runtime);

        var push = RunStatus("push");
        Assert.Equal((NodeStates.Active, 2), (push.State, push.Attempt));
        Assert.Null(Files.Read(RunUnit)!.Ended);
    }

    /// <summary>A push failed for a reason no look reads again is settled by the person's claim of a push outside, its proof origin's ref.</summary>
    [Fact]
    public async Task Done_outside_on_a_push_failed_otherwise_is_proven_by_origins_ref_and_the_run_goes_on()
    {
        var runtime = await EndedFailedAtItsPushAsync(PushLineRead.BranchGone);
        _origin["feature/q7"] = Tip1;
        await PassAsync(runtime);
        Assert.Equal(NodeStates.Failed, RunStatus("push").State);

        var worded = RunNodeWords.Word(Files, RunId, "push", NodeWordActs.DoneOutside, null, () => _now);
        Assert.True(worded.Done, worded.Refused);
        await PassAsync(runtime);

        Assert.Equal((NodeStates.Reached, NodeSource.Git), (RunStatus("push").State, RunStatus("push").By));
        Assert.Equal(RunOutcomes.Completed, Files.Read(RunUnit)!.Ended?.Outcome);
    }
}
