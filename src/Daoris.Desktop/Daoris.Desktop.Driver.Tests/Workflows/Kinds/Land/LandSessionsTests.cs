using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The session a run's <c>land</c> judges is the live part of its line: the newest of its branch unit's executors with a tree, by when each was
/// made and whatever order the executors are read in, so an earlier part a later one went on from never stands for the run. The landing it
/// then makes is <see cref="GraphRunLandingTests"/>'s.
/// </summary>
public sealed class LandSessionsTests
{
    private static readonly DateTimeOffset Then = new(2026, 11, 13, 0, 0, 0, TimeSpan.Zero);
    private static readonly string Tree = Path.GetFullPath(Path.Combine("land-sessions", "engine", "s1"));

    private static LandStart Start(string run, params string[] executors) =>
        new("run:" + run, run, "landing", "unit-a", executors, "engine", "aurora", "c0ffee", "gph-1", "review")
        {
            Authority = StepAuthority.OfDoor(run, "landing", 1),
        };

    private static SessionRecord Record(string id, DateTimeOffset created, string? quest = "q1", string? tree = null) =>
        new(id, "engine", "completed") { Quest = quest, Tree = tree ?? Tree, Created = created };

    /// <summary>A landing names the later part first, as a unit's executors once read it: the later part still stands for the run.</summary>
    [Fact]
    public void The_newest_executor_stands_for_the_run_whatever_order_the_executors_are_read_in()
    {
        var records = new[] { Record("s1", Then), Record("s2", Then.AddMinutes(20)) };

        Assert.Equal("s2", LandSessions.Of(Start("q1", "s2", "s1"), records)?.Id);
        Assert.Equal("s2", LandSessions.Of(Start("q1", "s1", "s2"), records)?.Id);
    }

    /// <summary>An executor with no tree or no quest stands for nothing; with none that has, the newest session on the run's own quest does.</summary>
    [Fact]
    public void An_executor_without_a_tree_stands_for_nothing_and_the_runs_own_newest_session_does()
    {
        var records = new[]
        {
            Record("s1", Then), Record("s2", Then.AddMinutes(20)), Record("s3", Then.AddMinutes(40)) with { Tree = null },
            Record("s4", Then.AddMinutes(50), quest: "q2"),
        };

        Assert.Equal("s2", LandSessions.Of(Start("q1", "s1", "s3", "s2"), records)?.Id);
        Assert.Equal("s2", LandSessions.Of(Start("q1"), records)?.Id);
        Assert.Null(LandSessions.Of(Start("q9"), records));
    }

    /// <summary>A conversation's run has no quest: the newest executor with a tree stands for it.</summary>
    [Fact]
    public void A_conversations_run_takes_its_newest_executor_with_a_tree()
    {
        var records = new[] { Record("c1", Then, quest: null), Record("c2", Then.AddMinutes(5), quest: null) };

        Assert.Equal("c2", LandSessions.OfChat(Start("chat-1", "c2", "c1"), records)?.Id);
    }
}
