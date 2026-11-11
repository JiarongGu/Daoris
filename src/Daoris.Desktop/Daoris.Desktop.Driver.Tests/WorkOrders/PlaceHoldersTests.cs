using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The queue behind a place's writer is the planner's order, read at each look: a start in a repository without trees sits behind
/// the one record holding its place, a running turn or a parked line with uncommitted paths of its own; a park with none holds nothing.
/// </summary>
public sealed class PlaceHoldersTests
{
    private const string Root = "D:/fam/Game";

    private static IReadOnlyList<Consideration> Plan(params SessionView[] active) =>
        Planner.Plan(
            new Snapshot([new QuestView("q1", "Asker", "Game", "Ask q1", "Why.", "Open")], [new RepoView("Game", true, Root)], active),
            DriverConfig.Empty with { Drivable = ["Game"], Holds = [], Cap = 2 },
            unadopted: false);

    [Fact]
    public void A_second_start_sits_behind_the_running_turn_holding_the_place()
    {
        var only = Assert.Single(Plan(new SessionView("s-1a2b", "Game", "working") { Tree = Root, Quest = "q0" }));

        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Equal(new SittingFact(SittingFact.Place) { Session = "s-1a2b", Repository = "Game" }, only.Said);
        Assert.Equal("`s-1a2b` is working in Game; this starts when its turn ends.", only.Reason);
    }

    [Fact]
    public void A_park_with_nothing_uncommitted_no_longer_holds_the_place()
    {
        var only = Assert.Single(Plan(new SessionView("s-1a2b", "Game", "awaiting-person") { Tree = Root, Quest = "q0" }));

        Assert.Equal(StartVerdict.Start, only.Verdict);
    }

    [Fact]
    public void A_park_with_uncommitted_paths_of_its_own_holds_the_place_and_says_so()
    {
        var only = Assert.Single(Plan(
            new SessionView("s-1a2b", "Game", "awaiting-person") { Tree = Root, Quest = "q0", HoldsPlace = ["a.md", "b.md", "c.md"] }));

        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Equal(["a.md", "b.md", "c.md"], only.Said!.Paths);
        Assert.Equal(
            "`s-1a2b` holds the checkout of Game: 3 uncommitted paths of its own; this starts once they are committed, discarded, or the person lets others work here.",
            only.Reason);
    }

    [Fact]
    public void A_session_in_a_tree_of_its_own_holds_no_lease_on_the_place()
    {
        var only = Assert.Single(Plan(new SessionView("s-1a2b", "Game", "working") { Tree = "X:/daoris/trees/default/Game/s-1a2b" }));

        Assert.Equal(StartVerdict.Start, only.Verdict);
    }
}
