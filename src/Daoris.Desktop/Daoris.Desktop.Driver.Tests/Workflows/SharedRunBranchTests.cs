using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The shape the install's stuck runs had: two quests of one ask in one repository, each its own graph run, the later run's session placed in
/// the tree the earlier run's session opened. The branch carries both runs' work; each run binds it, so its branch-side step starts rather
/// than waiting for good on a subject no pass would ever name. A branch Current's work shares with a graph run stays held, said.
/// </summary>
public sealed class SharedRunBranchTests : NodeRunFixture
{
    private const string Later = "q8";

    /// <summary><c>claimed</c>, then the branch-side <c>handed</c>, whose subject is the run's branch as <c>content</c>'s is.</summary>
    private const string ClaimedContentHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    /// <summary>Each of two graph runs whose sessions worked one tree binds that branch: its step after <c>claimed</c> is begun on it.</summary>
    [Fact]
    public async Task A_branch_two_graph_runs_produced_is_each_runs_subject()
    {
        var digest = RunHome.SaveGraph(_home, ClaimedContentHanded);
        RunHome.Bind(_home, RunId, digest, _now);
        RunHome.Bind(_home, Later, digest, _now);
        _branchTips["daoris/s1"] = Tip1;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.QuestUnit(RunHome.Quest(Later, "Done")),
            RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId, Later]) with { Subject = new WorkSubject("daoris/s1", Tip1) },
        ];

        var pass = await RunPassAsync(Runtime());

        foreach (var unit in new[] { RunUnit, RunUnits.IdOf(Later) })
        {
            var handed = RunStatus("handed", unit);
            Assert.NotEqual(NodeStates.Ready, handed.State);
            Assert.Equal(("unit-b1", Tip1), (handed.Binding?.Subject, handed.Binding?.SubjectRevision));
        }

        Assert.DoesNotContain(pass.Said, line => line.Contains("neither governs it", StringComparison.Ordinal));
    }

    /// <summary><c>claimed</c>, then a plugin's observed deploy on the run's branch, then the landing.</summary>
    private const string ClaimedDeployed = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deployed","kind":"acme.deploy/deployed","version":1,"reach":"observed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deployed"},{"id":"d>h","kind":"after","from":"deployed","to":"handed"}]}
        """;

    /// <summary>
    /// A plugin's event on a branch two runs share, told once for both, goes to the run whose own node awaits it there, never to the run the
    /// branch maps to first: the earlier run's deploy is stopped, and the later run's is reached by the event.
    /// </summary>
    [Fact]
    public async Task An_event_on_a_shared_branch_goes_to_the_run_whose_node_awaits_it()
    {
        var digest = RunHome.SaveGraph(_home, ClaimedDeployed);
        RunHome.Bind(_home, RunId, digest, _now);
        RunHome.Bind(_home, Later, digest, _now);
        _branchTips["daoris/s1"] = Tip1;
        _units =
        [
            RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.QuestUnit(RunHome.Quest(Later, "Done")),
            RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId, Later]) with { Subject = new WorkSubject("daoris/s1", Tip1) },
        ];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.True(RunNodeWords.Word(Files, RunId, "deployed", NodeWordActs.Stop, "Not this one.", () => _now).Done);
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("deployed", RunUnits.IdOf(Later)).State);

        _port.Heard.Enqueue(new(Plugin, 1, new WorkStepFrame("deployed", new WorkOutcome(WorkStates.Reached), _now, "e-1", RunHome.Repository, "daoris/s1", Tip1)));
        await RunPassAsync(runtime, whole: false);

        Assert.Equal(NodeStates.Reached, RunStatus("deployed", RunUnits.IdOf(Later)).State);
        Assert.Equal(NodeStates.Stopped, RunStatus("deployed").State);
    }
}
