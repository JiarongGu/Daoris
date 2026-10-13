using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The planner's half of an answer continuing its session (ANSWER1a, D131 §1): a taken quest whose last record here is a
/// park the person answered, still parked, is started from that park. The park is still an active record holding its
/// tree, its quest and its slot, so it is never busy with itself; anything else holding the tree still is.
/// </summary>
public sealed class AnswerContinuesPlanTests
{
    private static QuestView Taken(string id = "q1") => new(id, "Asker", "Game", $"Ask {id}", "Here is why.", "Taken");

    private static readonly RepoView Repo = new("Game", true, "D:/fam/Game");

    private static readonly PriorSession Park =
        new("s1", "D:/trees/s-1", "awaiting-person", "It stopped with its quest still taken, to ask you:\n\nWhich port?", "Game",
            Answer: "Port 8080.")
        {
            Profile = "account-1",
            Adapter = "claude-code-acp",
        };

    /// <summary>The park as the active list answers it: its record, its repository, its tree and its quest.</summary>
    private static readonly SessionView Parked = new("s1", "Game", "awaiting-person") { Tree = "D:/trees/s-1", Quest = "q1" };

    private static DriverConfig Config(bool trees = true, int cap = 2) =>
        DriverConfig.Empty with { Drivable = ["Game"], Trees = trees ? ["Game"] : [], Cap = cap };

    private static Snapshot With(params SessionView[] active) =>
        new([Taken()], [Repo], active) { LastRun = new Dictionary<string, PriorSession> { ["q1"] = Park } };

    [Fact]
    public void An_answered_park_is_started_from_itself_saying_the_answer()
    {
        var only = Assert.Single(Planner.Plan(With(Parked), Config()));

        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal(Park, only.Resumes);
        Assert.Equal("carrying on in `Game` — you answered session `s1`: Port 8080.", only.Reason);
        Assert.Equal("D:/fam/Game", only.Root);
    }

    /// <summary>🔴 The park holds its own tree (D51): read as busy with itself, the answer would wait for ever.</summary>
    [Fact]
    public void The_park_is_not_busy_with_its_own_tree_or_quest()
    {
        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(With(Parked), Config(trees: true))).Verdict);
    }

    /// <summary>Where the repository's root is its one tree, the park holding it is still not busy with itself.</summary>
    [Fact]
    public void In_a_repository_without_trees_of_its_own_the_park_is_not_busy_with_itself()
    {
        var root = Parked with { Tree = "D:/fam/Game" };
        var snapshot = new Snapshot([Taken()], [Repo], [root])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Park with { Tree = "D:/fam/Game" } },
        };

        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(snapshot, Config(trees: false))).Verdict);
    }

    /// <summary>The park already holds a slot of the cap; continuing it starts no second session.</summary>
    [Fact]
    public void The_park_holds_its_own_slot_so_a_spent_cap_does_not_hold_it()
    {
        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(With(Parked), Config(cap: 1))).Verdict);
    }

    /// <summary>Another session in its tree is still one session per tree.</summary>
    [Fact]
    public void Another_session_in_its_tree_still_holds_it()
    {
        var other = new SessionView("s9", "Game", "working") { Tree = "D:/trees/s-1" };

        var only = Assert.Single(Planner.Plan(With(Parked, other), Config(cap: 5)));

        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Contains("s9", only.Reason);
    }

    /// <summary>Another session in a root that is the repository's one tree still holds it.</summary>
    [Fact]
    public void Another_session_in_a_shared_root_still_holds_it()
    {
        var root = Parked with { Tree = "D:/fam/Game" };
        var chat = new SessionView("c1", "Game", "working") { Tree = "D:/fam/Game" };
        var snapshot = new Snapshot([Taken()], [Repo], [root, chat])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Park with { Tree = "D:/fam/Game" } },
        };

        var only = Assert.Single(Planner.Plan(snapshot, Config(trees: false, cap: 5)));

        Assert.Equal(StartVerdict.RepositoryBusy, only.Verdict);
        Assert.Contains("c1", only.Reason);
    }

    /// <summary>A park nobody has answered waits on the person, as before: nothing is planned.</summary>
    [Fact]
    public void An_unanswered_park_is_not_planned()
    {
        var snapshot = new Snapshot([Taken()], [Repo], [Parked])
        {
            LastRun = new Dictionary<string, PriorSession> { ["q1"] = Park with { Answer = null } },
        };

        Assert.Empty(Planner.Plan(snapshot, Config()));
    }

    /// <summary>The person's hold still stands between an answer and a start.</summary>
    [Fact]
    public void A_held_repository_holds_the_answer_too()
    {
        var only = Assert.Single(Planner.Plan(With(Parked), Config() with { Holds = ["Game"] }));

        Assert.Equal(StartVerdict.Held, only.Verdict);
    }

    /// <summary>
    /// The last run reads what a continuation compares: the adapter the record opened on, and the commit its tree stood
    /// at, so the review's range is the whole session's.
    /// </summary>
    [Fact]
    public void The_last_run_says_its_adapter_and_its_base_commit()
    {
        var last = ServiceClient.ReadLastRun("""
            [{ "id": "s1", "quest": "q1", "state": "awaiting-person", "adapter": "claude-code-acp", "baseCommit": "abc123",
               "answer": "Port 8080.", "created": "2026-10-02T10:00:00Z" },
             { "id": "s2", "quest": "q2", "state": "failed", "created": "2026-10-02T10:00:00Z" }]
            """);

        Assert.Equal(("claude-code-acp", "abc123", true), (last["q1"].Adapter, last["q1"].BaseCommit, last["q1"].AnsweredPark));
        Assert.Equal(((string?)null, (string?)null, false), (last["q2"].Adapter, last["q2"].BaseCommit, last["q2"].AnsweredPark));
    }
}
