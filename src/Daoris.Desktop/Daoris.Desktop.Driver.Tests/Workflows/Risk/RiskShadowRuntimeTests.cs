using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Risk;

/// <summary>
/// The runtime's hold point wired: a run's approval journals one verdict at the pass that opens its wait and one at the pass after
/// the press releases it, with today's gate beside each; the wait, its row and its press are as they were (the shadow decides nothing).
/// </summary>
public sealed class RiskShadowRuntimeTests : NodeRunFixture
{
    private const string ApprovalGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"sign","kind":"task","version":1,"reach":"pressed","outcomes":["approved"]},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>s","kind":"after","from":"claimed","to":"sign"},
                  {"id":"s>h","kind":"when","from":"sign","to":"handed","outcome":"approved"}]}
        """;

    [Fact]
    public async Task A_runs_approval_journals_its_open_and_its_release_beside_todays_gate_and_waits_as_before()
    {
        BoundTo(ApprovalGraph);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        var runtime = Runtime();
        runtime.Risk = new RiskShadow(_home, () => _now);

        await RunPassAsync(runtime);
        await RunPassAsync(runtime, whole: false);

        var opened = Assert.Single(RiskJournal.Read(_home));
        Assert.Equal((RiskJudged.Opened, RiskActs.TaskApprove, RunId, "sign"), (opened.Stage, opened.Kind, opened.Run, opened.Step));
        Assert.Equal(StepWaiting.Line(StepRoles.Owner, new StepHolder(StepHolders.Person, null)), opened.Today);
        Assert.Equal((RiskVerdict.Pass, false), (opened.Outcome, opened.Agrees));
        // Today's wait stands as it did.
        Assert.Equal(PersonWaits.Approval, Assert.Single(runtime.HeldSteps()).Why);

        Assert.True(RunNodeWords.Word(Files, RunId, "sign", NodeWordActs.Approve, null, () => _now).Done);
        await RunPassAsync(runtime, whole: false);

        var released = RiskJournal.Read(_home)[^1];
        Assert.Equal((RiskJudged.Released, "sign"), (released.Stage, released.Step));
        Assert.StartsWith("released: ", released.Today, StringComparison.Ordinal);
        Assert.Empty(runtime.HeldSteps());
    }
}
