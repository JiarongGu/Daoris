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
}
