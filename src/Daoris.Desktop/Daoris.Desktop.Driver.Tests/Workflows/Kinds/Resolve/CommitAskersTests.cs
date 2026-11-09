using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One asker to commit a tree's work (<see cref="CommitAskers"/>): nobody while a hand-off reserves the tree, a run's <c>resolve</c> while its
/// <c>rebase</c> will read the work, else the turn's end.
/// </summary>
public sealed class CommitAskersTests : TestHome
{
    private string Tree => DaorisHome.Entry(Home, HomeEntry.Trees, "default", "engine", "aurora");

    [Fact]
    public void A_tree_a_hand_off_reserves_is_asked_by_nobody_but_its_resolver()
    {
        Assert.Equal(CommitAskers.TurnsEnd, CommitAskers.Of(Home, Tree));
        var offs = new TreeHandOffs(Home, () => StoreSettled.Yes);
        offs.Write(new TreeHandOff(Tree, "engine", "daoris/aurora", "t1", "r-12", "resolve", 1, "stop", "session", TreeHandOff.AskKey("r-12", "resolve", 1, 1),
            TreeHandOffPhases.Reserved, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30)));

        Assert.Equal(CommitAskers.Nobody, CommitAskers.Of(Home, Tree));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(NodeStates.Active, true)]
    [InlineData(NodeStates.Reached, false)]
    [InlineData(NodeStates.Skipped, false)]
    public void A_run_s_rebase_answers_while_it_has_neither_reached_nor_been_skipped(string? state, bool answers)
    {
        var rebase = ObservedFixture.Node(NodeKinds.Rebase);
        var statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal);
        if (state is not null) statuses[rebase.Id] = new NodeStatus("run:r-12", rebase.Id, state);

        Assert.Equal(answers, CommitAskers.Rebases([rebase], statuses));
        Assert.False(CommitAskers.Rebases([ObservedFixture.Node(NodeKinds.Look)], statuses));
    }
}
