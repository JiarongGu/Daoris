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

    /// <summary>The run landed at <see cref="NodeRunFixture.Tip1"/>, its push refused (a passing tree clean-up), and the run ended failed there.</summary>
    private async Task<NodeRuntime> EndedFailedAtItsPushAsync()
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
            Statuses = new Dictionary<string, NodeStatus>(record.Statuses) { ["push"] = record.Statuses["push"] with { State = NodeStates.Failed, Code = PushDoor.Refused } },
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
}
