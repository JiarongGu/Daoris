using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A stand-down against this machine's own dead taker, read from the records the look reads, leaves the quest carried on.</summary>
public sealed class StandDownBehindTakerTests
{
    private const string Records = """
        [{ "id": "s1", "quest": "q1", "state": "failed", "took": true, "created": "2026-11-13T09:00:00Z", "updated": "2026-11-13T09:10:00Z", "tree": "trees/s1" },
         { "id": "s2", "quest": "q1", "state": "stood-down", "created": "2026-11-13T09:20:00Z", "updated": "2026-11-13T09:25:00Z", "tree": "trees/s1" }]
        """;

    private static Snapshot Read(string claim)
    {
        var listed = SessionReads.Parse(Records);
        var quest = new QuestView("q1", "Asker", "Game", "Ask q1", "Here is why.", "Taken");
        return new Snapshot([quest], [new RepoView("Game", true, "D:/fam/Game")], [])
        {
            LastRun = SessionReads.LastRun(listed),
            StoodDown = SessionReads.StoodDown(listed),
            Takes = SessionReads.Takes(listed, new Dictionary<string, string> { ["q1"] = claim }),
        };
    }

    private static DriverConfig Config() => DriverConfig.Empty with { Drivable = ["Game"], Holds = [], Cap = 2 };

    [Fact]
    public void A_stand_down_behind_a_failed_taker_whose_claim_is_held_here_is_carried_on_from_the_taker()
    {
        var only = Assert.Single(Planner.Plan(Read("held"), Config()));

        Assert.Equal(StartVerdict.Start, only.Verdict);
        Assert.Equal("s1", only.Resumes?.Session);
    }

    [Fact]
    public void A_stand_down_against_another_machines_take_is_not_carried_on()
    {
        var only = Assert.Single(Planner.Plan(Read("lost"), Config()));

        Assert.Equal(StartVerdict.TakenElsewhere, only.Verdict);
    }
}
