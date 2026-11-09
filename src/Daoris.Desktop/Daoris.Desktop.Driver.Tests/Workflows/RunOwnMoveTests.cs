using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's own move as its binding's revision (<see cref="RunMoves"/>): a ref the run moved itself is read at the revision it moved it from
/// while it stands where the run moved it, so nothing bound to it lapses; a move on from there is new work. The run's entry carries its work
/// on a port (<c>claimed</c>'s <c>work</c>).
/// </summary>
public sealed class RunOwnMoveTests : NodeRunFixture
{
    private const string ClaimedThenHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    private const string Tip3 = "3333333333333333333333333333333333333333";

    [Fact]
    public async Task A_recorded_own_move_followed_by_a_pass_lapses_nothing_and_a_move_on_from_it_is_new_work()
    {
        BoundTo(ClaimedThenHanded);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        var handed = RunStatus("handed");
        Assert.Equal((NodeStates.Active, "unit-b1", Tip1), (handed.State, handed.Binding!.Subject, handed.Binding.SubjectRevision));
        var claimed = RunStatus("claimed");
        var work = claimed.Current!.Outputs[ClaimedWork.Port];
        Assert.Equal((WireValues.Work, new NodeProducer("claimed", claimed.Attempt), claimed.Current.Id), (work.Schema, work.Producer, work.Acceptance));

        Assert.True(RunMoves.Keep(Files, RunUnit, new RunMove("unit-b1", Tip1, Tip2, "tidy", 1)));
        _branchTips["daoris/s1"] = Tip2;
        await RunPassAsync(runtime);
        var kept = RunStatus("handed");
        Assert.Equal((NodeStates.Active, handed.Attempt, handed.Binding.Id), (kept.State, kept.Attempt, kept.Binding!.Id));
        Assert.Equal([new RunMove("unit-b1", Tip1, Tip2, "tidy", 1)], Files.Read(RunUnit)!.Moves);

        _branchTips["daoris/s1"] = Tip3;
        await RunPassAsync(runtime);
        var moved = RunStatus("handed");
        Assert.NotEqual((NodeStates.Active, handed.Binding.Id), (moved.State, moved.Binding?.Id));
    }
}
