using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// DRV6's derivation: how often a quest has been failed, read off the session records this machine
/// already wrote rather than kept as a tally of its own.
///
/// <para>🔴 Measured before it was designed. ACP2's driven run failed for a reason no retry could fix
/// — the session had no tool to take its quest with — and the driver started the same quest
/// <b>18 times</b>, each one a real login, because a failed spawn leaves the quest <c>Open</c> and
/// untouched (driver design §3) and an untouched open quest is eligible again next tick.</para>
/// </summary>
public sealed class StrikeTests
{
    private const string Records = """
        [
          { "id": "s1", "quest": "q1", "repository": "Game", "state": "failed" },
          { "id": "s2", "quest": "q1", "repository": "Game", "state": "failed" },
          { "id": "s3", "quest": "q2", "repository": "Game", "state": "stood-down" },
          { "id": "s4", "quest": "q3", "repository": "Game", "state": "declined" },
          { "id": "s5", "quest": "q4", "repository": "Game", "state": "stopped" },
          { "id": "s6", "quest": "q5", "repository": "Game", "state": "completed" },
          { "id": "s7", "quest": "q6", "repository": "Game", "state": "working" }
        ]
        """;

    [Fact]
    public void Failures_are_counted_per_quest()
    {
        var strikes = ServiceClient.ReadStrikes(Records);

        Assert.Equal(2, strikes["q1"]);
    }

    /// <summary>
    /// Every other ending is NOT a strike, and each for its own reason: a stand-down is the race
    /// resolving as designed, a decline is a real answer, a stop was the person, and a session still
    /// working has not ended at all. Counting any of them would park a quest for succeeding.
    /// </summary>
    [Theory]
    [InlineData("q2")]
    [InlineData("q3")]
    [InlineData("q4")]
    [InlineData("q5")]
    [InlineData("q6")]
    public void Nothing_but_a_failure_counts(string quest)
    {
        Assert.DoesNotContain(quest, ServiceClient.ReadStrikes(Records).Keys);
    }

    /// <summary>A record from a service older than the state field is no evidence, not a failure.</summary>
    [Fact]
    public void A_record_with_no_state_is_not_a_strike()
    {
        Assert.Empty(ServiceClient.ReadStrikes("""[{ "id": "s1", "quest": "q1", "repository": "Game" }]"""));
    }

    /// <summary>
    /// A teammate's failed session came down with the sync keyed `origin/id` (SYNC4). A strike is this
    /// driver's judgement of its OWN attempts, so a failure on another machine parks nothing here.
    /// </summary>
    [Fact]
    public void A_teammates_failure_is_not_this_machines_strike()
    {
        Assert.Empty(ServiceClient.ReadStrikes("""
            [{ "id": "b@two/s1", "quest": "q1", "repository": "Game", "state": "failed" },
             { "id": "b@two/s2", "quest": "q1", "repository": "Game", "state": "failed" },
             { "id": "b@two/s3", "quest": "q1", "repository": "Game", "state": "failed" }]
            """));
    }

    /// <summary>
    /// Which session this machine last ran on a quest (D79) — the newest of its own, wherever the
    /// records list it, with the tree it ran in: where a waiting quest resumes.
    /// </summary>
    [Fact]
    public void The_last_run_on_a_quest_is_this_machines_newest_session_and_its_tree()
    {
        var last = ServiceClient.ReadLastRun("""
            [{ "id": "s2", "quest": "q1", "state": "completed", "tree": "D:/trees/s2", "created": "2026-09-27T10:00:00Z" },
             { "id": "s1", "quest": "q1", "state": "failed", "tree": "D:/trees/s1", "created": "2026-09-27T09:00:00Z" },
             { "id": "c1", "repository": "Game", "state": "stopped", "created": "2026-09-27T11:00:00Z" }]
            """);

        Assert.Equal(new PriorSession("s2", "D:/trees/s2"), last["q1"]);
        Assert.Single(last);
    }

    /// <summary>
    /// 🔴 A stand-down means somebody else had the quest, and a teammate's record is another machine's
    /// run: neither makes a waiting quest this machine's to resume.
    /// </summary>
    [Fact]
    public void A_stand_down_or_a_teammates_session_is_not_a_run_here()
    {
        Assert.Empty(ServiceClient.ReadLastRun("""
            [{ "id": "s1", "quest": "q1", "state": "stood-down", "created": "2026-09-27T10:00:00Z" },
             { "id": "b@two/s2", "quest": "q2", "state": "completed", "created": "2026-09-27T10:00:00Z" }]
            """));
    }

    /// <summary>
    /// The mark round-trips through the file both doors edit, beside the limit — and beside every
    /// field this build has no verb for, which is the counterpart-set worry that has bitten this
    /// family twice (the harness pin, and profiles before it).
    /// </summary>
    [Fact]
    public void The_limit_and_the_marks_round_trip_through_the_file()
    {
        var config = DriverConfig.Empty
            .WithStrikes(5)
            .WithForgiven("q1", 3)
            .WithHold("Game", true);

        var read = DriverConfig.Parse(config.ToJson());

        Assert.Equal(5, read.Strikes);
        Assert.Equal(3, read.ForgivenAt("q1"));
        Assert.Equal(0, read.ForgivenAt("q2"));
        Assert.Contains("Game", read.Holds);
    }

    /// <summary>
    /// 🔴 Absent means the DEFAULT, and this is the opposite reading from <c>notify</c>. A machine
    /// whose `driver.json` predates this field is exactly the one that has been driving unattended
    /// longest, so silence must not mean "never park" there.
    /// </summary>
    [Fact]
    public void A_file_written_before_this_field_gets_the_default_limit()
    {
        var read = DriverConfig.Parse("""{ "drivable": ["Game"], "cap": 2 }""");

        Assert.Equal(3, read.Strikes);
        Assert.Empty(read.Forgiven);
    }
}
