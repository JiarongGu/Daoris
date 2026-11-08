using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A run whose session's tree followed it onto another branch (the branch record's <c>Now</c>): every read of its branch is of that one.</summary>
public sealed class NodeRunFollowedBranchTests : NodeRunFixture
{
    private const string Followed = "tree/s1";

    private const string ClaimedThenDeployed = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deployed","kind":"acme.deploy/deployed","version":1,"reach":"observed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deployed"},{"id":"d>h","kind":"after","from":"deployed","to":"handed"}]}
        """;

    private const string ClaimedThenDeploy = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    private static WorkUnit Branch() => RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]) with
    {
        Records = new WorkUnitRecords(
            new GrownBranch(RunHome.Repository, "default", "daoris/s1", "main", "base", null, Opened, Followed) { Unit = "unit-b1" }, [], null),
    };

    /// <summary>The plugin of a run's performed node is told the branch the run's work stands on, at that branch's tip.</summary>
    [Fact]
    public async Task A_runs_plugin_is_told_the_branch_its_tree_followed_it_onto()
    {
        BoundTo(ClaimedThenDeploy);
        _branchTips[Followed] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), Branch()];
        var runtime = Runtime();

        await RunPassAsync(runtime);

        var told = Assert.Single(_port.Told[^1][Plugin]);
        Assert.Equal(("unit-b1", Followed, Tip1), (told.Unit, told.Branch, told.Tip));
    }

    /// <summary>A wake judges a run whose bound branch tip moved on the branch its tree followed it onto, as a look does.</summary>
    [Fact]
    public async Task A_wake_rebinds_a_run_on_the_branch_its_tree_followed_it_onto()
    {
        BoundTo(ClaimedThenDeployed);
        _branchTips[Followed] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), Branch()];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal((NodeStates.Active, Tip1), (RunStatus("deployed").State, RunStatus("deployed").Binding!.SubjectRevision));

        _branchTips[Followed] = Tip2;
        var wake = await RunPassAsync(runtime, whole: false);

        Assert.Contains(RunUnit, wake.Evaluated);
        Assert.Equal(Tip2, RunStatus("deployed").Binding!.SubjectRevision);
    }
}
