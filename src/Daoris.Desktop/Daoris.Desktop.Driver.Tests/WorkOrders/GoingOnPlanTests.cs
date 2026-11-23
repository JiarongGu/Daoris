using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A record a run of this machine's already goes on in begins nothing, so it never heads the start queue: planned as a start at
/// each look, it took the repository's turn and every quest behind it read "queued behind" a head that never began, for 45 minutes.
/// </summary>
public sealed class GoingOnPlanTests
{
    private static QuestView Quest(string id, string status = "Open") => new(id, "Asker", "Game", $"Ask {id}", "Here is why.", status);

    private static readonly PriorSession Waiting =
        new("s1", "D:/trees/s1", "completed", "ended on its step.") { Said = PersonWords.Of("Go on.") };

    private static Snapshot Snap(params string[] goingOn) =>
        new Snapshot([Quest("q1", "Taken"), Quest("q2")], [new RepoView("Game", true, "D:/fam/Game")], [])
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = Waiting },
            GoingOn = new HashSet<string>(goingOn, StringComparer.OrdinalIgnoreCase),
        };

    private static DriverConfig Config() => DriverConfig.Empty with { Drivable = ["Game"], Cap = 3 };

    [Fact]
    public void A_go_on_not_yet_taken_up_heads_the_queue_and_the_next_quest_waits_behind_it()
    {
        var plan = Planner.Plan(Snap(), Config());

        Assert.Equal(StartVerdict.Start, plan.Single(each => each.Quest.Id == "q1").Verdict);
        var behind = plan.Single(each => each.Quest.Id == "q2");
        Assert.Equal(StartVerdict.RepositoryBusy, behind.Verdict);
        Assert.Equal(new SittingFact(SittingFact.Queued) { Quest = "q1" }, behind.Said);
    }

    [Fact]
    public void A_go_on_a_run_already_holds_is_no_start_and_the_next_quest_starts()
    {
        var plan = Planner.Plan(Snap("s1"), Config());

        var held = plan.Single(each => each.Quest.Id == "q1");
        Assert.Equal(StartVerdict.RepositoryBusy, held.Verdict);
        Assert.Equal("s1", held.Said?.Session);
        Assert.Equal(StartVerdict.Start, plan.Single(each => each.Quest.Id == "q2").Verdict);
    }

    [Fact]
    public void The_queue_log_names_each_queued_quest_its_head_and_the_heads_own_verdict()
    {
        var waits = StartQueueLog.Waits(Planner.Plan(Snap(), Config()));

        var (key, fields) = waits["q2"];
        Assert.Equal("queued|q1|Start", key);
        Assert.Equal("q1", fields.Single(field => field.Key == "head").Value);
        Assert.Equal("Start", fields.Single(field => field.Key == "headVerdict").Value);
        Assert.Equal(true, fields.Single(field => field.Key == "headGoesOn").Value);
        Assert.Equal("s1", fields.Single(field => field.Key == "headSession").Value);
        Assert.False(waits.ContainsKey("q1"));
    }

    [Fact]
    public void A_queue_that_stands_is_told_once_and_told_again_when_it_moves_or_comes_back()
    {
        var runs = new RunningSessions();
        var standing = StartQueueLog.Waits(Planner.Plan(Snap(), Config())).ToDictionary(each => each.Key, each => each.Value.Key);

        Assert.Equal(["q2"], runs.QueueWaits(standing));
        Assert.Empty(runs.QueueWaits(standing));

        Assert.Empty(runs.QueueWaits(new Dictionary<string, string>()));
        Assert.Equal(["q2"], runs.QueueWaits(standing));

        Assert.Equal(["q2"], runs.QueueWaits(new Dictionary<string, string> { ["q2"] = "queued|q1|Blocked" }));
    }
}
