using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The plan is a pure function of what the service answered and what the person configured — the same
/// plan/apply split the CLI holds, for the same reason: a decision that can be asserted without
/// spawning anything is a decision that stays tested.
///
/// Every non-start carries its reason, because the Overview's question splits under a driver (D46 §3):
/// sitting because nobody CAN take it is the person's setup work; sitting because the driver has not
/// started it is the driver's state to explain.
/// </summary>
public sealed class PlannerTests
{
    private static QuestView Quest(string id = "q1", string to = "Game", string status = "Open") =>
        new(id, "Asker", to, $"Ask {id}", "Here is why.", status);

    private static RepoView Repo(string name = "Game", bool adopted = true, string? root = "D:/fam/Game") =>
        new(name, adopted, root);

    private static DriverConfig Config(
        string[]? drivable = null, string[]? holds = null, int cap = 2) =>
        DriverConfig.Empty with { Drivable = drivable ?? ["Game"], Holds = holds ?? [], Cap = cap };

    private static IReadOnlyList<Consideration> Plan(
        QuestView[] quests, RepoView[]? repos = null, SessionView[]? active = null, DriverConfig? config = null) =>
        Planner.Plan(new Snapshot(quests, repos ?? [Repo()], active ?? []), config ?? Config());

    [Fact]
    public void An_open_quest_for_a_drivable_repository_starts()
    {
        var plan = Plan([Quest()]);

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal("D:/fam/Game", only.Root);
    }

    /// <summary>Quests someone already has, and finished ones, are not the driver's to consider.</summary>
    [Fact]
    public void Only_open_quests_are_considered()
    {
        var plan = Plan([Quest(status: "Taken"), Quest("q2", status: "Done")]);

        Assert.Empty(plan);
    }

    /// <summary>Drivable is opt-in per repository, per machine (D46 §2) — silence means untouched.</summary>
    [Fact]
    public void A_repository_that_never_opted_in_is_not_driven_and_the_reason_says_so()
    {
        var plan = Plan([Quest()], config: Config(drivable: []));

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.NotDrivable, only.Verdict);
        Assert.Contains("opt", only.Reason);
    }

    [Fact]
    public void A_held_repository_waits_for_the_person()
    {
        var plan = Plan([Quest()], config: Config(holds: ["Game"]));

        Assert.Equal(StartVerdict.Held, Assert.Single(plan).Verdict);
    }

    [Fact]
    public void A_receiver_that_has_not_adopted_cannot_be_driven()
    {
        var plan = Plan([Quest()], repos: [Repo(adopted: false)]);

        Assert.Equal(StartVerdict.NotAdopted, Assert.Single(plan).Verdict);
    }

    /// <summary>No root, no working tree to spawn in — the reason names `connect` as the fix.</summary>
    [Fact]
    public void A_repository_without_a_root_cannot_be_spawned_into()
    {
        var plan = Plan([Quest()], repos: [Repo(root: null)]);

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.NoRoot, only.Verdict);
        Assert.Contains("connect", only.Reason);
    }

    /// <summary>One session per repository: an active session holds the tree (D46 §3).</summary>
    [Fact]
    public void An_active_session_blocks_its_repository_and_is_named()
    {
        var plan = Plan([Quest()], active: [new SessionView("s1", "Game")]);

        var only = Assert.Single(plan);
        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Contains("s1", only.Reason);
    }

    /// <summary>Oldest first, one per repository — the second ask queues behind the first.</summary>
    [Fact]
    public void The_oldest_open_quest_starts_and_the_next_queues_behind_it()
    {
        var plan = Plan([Quest("q1"), Quest("q2")]);

        Assert.Equal(StartVerdict.Start, plan[0].Verdict);
        Assert.Equal(StartVerdict.RepositoryBusy, plan[1].Verdict);
        Assert.Contains("q1", plan[1].Reason);
    }

    /// <summary>Concurrency across repositories is the point; the cap bounds it.</summary>
    [Fact]
    public void Different_repositories_start_together_until_the_cap()
    {
        var repos = new[] { Repo("A", root: "/a"), Repo("B", root: "/b"), Repo("C", root: "/c") };
        var quests = new[] { Quest("q1", "A"), Quest("q2", "B"), Quest("q3", "C") };

        var plan = Plan(quests, repos, config: Config(drivable: ["A", "B", "C"], cap: 2));

        Assert.Equal(StartVerdict.Start, plan[0].Verdict);
        Assert.Equal(StartVerdict.Start, plan[1].Verdict);
        Assert.Equal(StartVerdict.AtCapacity, plan[2].Verdict);
    }

    /// <summary>Sessions already running count against the cap before anything new starts.</summary>
    [Fact]
    public void Active_sessions_elsewhere_consume_the_cap()
    {
        var repos = new[] { Repo("A", root: "/a"), Repo("B", root: "/b") };

        var plan = Plan(
            [Quest("q1", "A")], repos,
            active: [new SessionView("s1", "B")],
            config: Config(drivable: ["A", "B"], cap: 1));

        Assert.Equal(StartVerdict.AtCapacity, Assert.Single(plan).Verdict);
    }

    /// <summary>Repository names compare the way the exchange compares them — case is not identity.</summary>
    [Fact]
    public void Drivable_matching_ignores_case()
    {
        var plan = Plan([Quest()], config: Config(drivable: ["game"]));

        Assert.Equal(StartVerdict.Start, Assert.Single(plan).Verdict);
    }
}
