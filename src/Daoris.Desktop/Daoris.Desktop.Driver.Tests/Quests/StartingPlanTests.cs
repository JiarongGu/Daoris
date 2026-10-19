using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARALLEL1b (D181 point 3): a start no longer holds the look past its reservation, so a start of this machine's that has not yet
/// opened its record is in no snapshot the service answers. The look hands the planner what it reserved (<see cref="Snapshot.Starting"/>):
/// its quest is not started again, its place under the cap is taken, the repository whose root is its one tree is held, and so is
/// the tree it goes back into. A start that has opened since is counted once.
/// </summary>
public sealed class StartingPlanTests
{
    private static QuestView Quest(string id = "q1", string to = "Game", string status = "Open") =>
        new(id, "Asker", to, $"Ask {id}", "Here is why.", status);

    private static RepoView Repo(string name = "Game", string? root = null) => new(name, true, root ?? $"D:/fam/{name}");

    private static DriverConfig Config(int cap = 3, bool trees = true) =>
        DriverConfig.Empty with { Drivable = ["Game", "Tools", "Docs"], Cap = cap, Trees = trees ? ["Game", "Tools", "Docs"] : [] };

    private static Snapshot Snapshot(QuestView[] quests, StartReservation[] starting, SessionView[]? active = null) =>
        new(quests, [Repo("Game"), Repo("Tools"), Repo("Docs")], active ?? []) { Starting = starting };

    [Fact]
    public void A_quest_a_start_here_is_still_opening_is_not_started_again()
    {
        var plan = Planner.Plan(Snapshot([Quest("q1")], [new StartReservation("q1", null, "Game", null)]), Config());

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Contains("#q1", only.Reason);
        Assert.Contains("starting", only.Reason);
    }

    [Fact]
    public void A_start_still_opening_takes_its_place_under_the_cap()
    {
        var plan = Planner.Plan(
            Snapshot([Quest("q2", "Tools"), Quest("q3", "Docs")], [new StartReservation("q1", null, "Game", null)]), Config(cap: 2));

        Assert.Equal(StartVerdict.Start, plan[0].Verdict);
        Assert.Equal(StartVerdict.AtCapacity, plan[1].Verdict);
    }

    [Fact]
    public void A_start_that_has_opened_since_is_counted_once()
    {
        var plan = Planner.Plan(
            Snapshot(
                [Quest("q2", "Tools"), Quest("q3", "Docs")],
                [new StartReservation("q1", null, "Game", null)],
                [new SessionView("s1", "Game", "working") { Quest = "q1" }]),
            Config(cap: 3));

        Assert.Equal(StartVerdict.Start, plan[0].Verdict);
        Assert.Equal(StartVerdict.Start, plan[1].Verdict);
    }

    [Fact]
    public void An_intake_still_opening_takes_its_place_under_the_cap()
    {
        var plan = Planner.Plan(
            Snapshot([Quest("q2", "Tools"), Quest("q3", "Docs")], [new StartReservation(null, "a1", "ask #a1", null)]), Config(cap: 2));

        Assert.Equal(StartVerdict.Start, plan[0].Verdict);
        Assert.Equal(StartVerdict.AtCapacity, plan[1].Verdict);
    }

    [Fact]
    public void Without_trees_a_start_still_opening_holds_its_repository_s_root()
    {
        var plan = Planner.Plan(Snapshot([Quest("q2")], [new StartReservation("q1", null, "Game", null)]), Config(trees: false));

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Contains("#q1", only.Reason);
    }

    [Fact]
    public void The_tree_a_start_still_opening_goes_back_into_is_held()
    {
        var prior = new PriorSession("s1", "D:/trees/s-1", "failed", "timed out.", Repository: "Game");
        var snapshot = Snapshot([Quest("q2", status: "Taken")], [new StartReservation("q9", null, "Game", "D:/trees/s-1")]) with
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q2"] = prior },
        };

        var only = Assert.Single(Planner.Plan(snapshot, Config()));
        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Contains("#q9", only.Reason);
    }
}
