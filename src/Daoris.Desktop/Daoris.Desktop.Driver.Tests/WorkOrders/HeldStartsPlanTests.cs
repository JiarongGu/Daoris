using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A start that cannot go now never holds a repository's turn from one that can.</summary>
public sealed class HeldStartsPlanTests
{
    private static QuestView Quest(string id, string status = "Open") => new(id, "Asker", "Game", $"Ask {id}", "Here is why.", status);

    private static Snapshot Snap(QuestView[] quests, params string[] held) =>
        new Snapshot(quests, [new RepoView("Game", true, "D:/fam/Game")], [])
        {
            HeldStarts = new HashSet<string>(held, StringComparer.OrdinalIgnoreCase),
        };

    private static DriverConfig Config() => DriverConfig.Empty with { Drivable = ["Game"], Cap = 3 };

    [Fact]
    public void An_open_quest_starts_in_the_look_where_a_carry_on_ahead_of_it_is_held()
    {
        var cut = new PriorSession("s1", "D:/trees/s1", "failed", "timed out.");
        var quests = new[] { Quest("q1", "Taken"), Quest("q2") };
        var ran = Snap(quests, "q1") with
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = cut },
        };

        var plan = Planner.Plan(ran, Config());

        Assert.Equal(StartVerdict.Start, plan.Single(each => each.Quest.Id == "q2").Verdict);
        Assert.Equal(StartVerdict.RepositoryBusy, plan.Single(each => each.Quest.Id == "q1").Verdict);
    }

    [Fact]
    public void A_held_quest_is_tried_again_where_nothing_else_waits()
    {
        var plan = Planner.Plan(Snap([Quest("q1")], "q1"), Config());

        Assert.Equal(StartVerdict.Start, Assert.Single(plan).Verdict);
    }

    [Fact]
    public void A_quest_kept_by_one_look_is_let_go_when_its_record_opens()
    {
        var held = new HeldStarts();
        var started = new StartBegun("q1", null, "Game", "ws", "claude");

        held.Settle([new StartCame(started, new StartRun("held", Opened: false, Held: "cooling"), [])]);
        Assert.Contains("q1", held.Quests);

        held.Settle([new StartCame(started, null, [])]);
        Assert.Empty(held.Quests);
    }
}
