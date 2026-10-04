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
    /// resolving as designed, a decline is a real answer, a stop the person made was theirs (one that was
    /// not is below), and a session still
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

    /// <summary>
    /// D104: a stop that was not the person's — the orphan sweep's, or a shutdown's — is a cut-off, and
    /// counts as one. The person's own stop, said or unsaid, never does.
    /// </summary>
    [Fact]
    public void An_interrupted_stop_is_a_strike_and_the_persons_stop_is_not()
    {
        var strikes = ServiceClient.ReadStrikes("""
            [{ "id": "s1", "quest": "q1", "repository": "Game", "state": "stopped", "interrupted": true },
             { "id": "s2", "quest": "q1", "repository": "Game", "state": "failed" },
             { "id": "s3", "quest": "q2", "repository": "Game", "state": "stopped" },
             { "id": "s4", "quest": "q3", "repository": "Game", "state": "stopped", "interrupted": false },
             { "id": "s5", "quest": "q4", "repository": "Game", "state": "completed", "interrupted": true }]
            """);

        Assert.Equal(2, strikes["q1"]);
        Assert.Equal(["q1"], strikes.Keys);
    }

    /// <summary>
    /// D125 §5.2, amending D58: a failure an account's limit made is the account's state, with its own reset, and the
    /// cool-off bounds it, so it is never a strike. A failure that does not say `limit`, or says it false, still is.
    /// </summary>
    [Fact]
    public void A_failure_an_account_s_limit_made_is_never_a_strike()
    {
        var strikes = ServiceClient.ReadStrikes("""
            [{ "id": "s1", "quest": "q1", "repository": "Game", "state": "failed", "limit": true },
             { "id": "s2", "quest": "q1", "repository": "Game", "state": "failed", "limit": false },
             { "id": "s3", "quest": "q1", "repository": "Game", "state": "failed" },
             { "id": "s4", "quest": "q2", "repository": "Game", "state": "failed", "limit": true }]
            """);

        Assert.Equal(2, strikes["q1"]);
        Assert.Equal(["q1"], strikes.Keys);
    }

    /// <summary>
    /// ROSTER1b: a failure whose note carries an account's line saying the agent or its provider refused the account it ran on
    /// (its sign-in, or its credential, AGT3b) is the account's, never the quest's, so it is never a strike. Read by the line's
    /// code, which the driver wrote, never by its words. A failure with other lines, or with none, still is.
    /// </summary>
    [Theory]
    [InlineData("account.signed-out")]
    [InlineData("account.signed-out-own")]
    [InlineData("account.refused")]
    [InlineData("account.refused-own")]
    public void A_failure_the_account_s_sign_in_made_is_never_a_strike(string code)
    {
        var strikes = ServiceClient.ReadStrikes($$"""
            [{ "id": "s1", "quest": "q1", "repository": "Game", "state": "failed", "note": "refused.",
               "noteParts": [{ "code": "ended.turn-failed-open", "values": {}, "text": "the agent's turn failed before it took its quest:" },
                             { "words": "the ACP agent refused the call: Authentication required", "by": "agent" },
                             { "code": "{{code}}", "values": { "owner": "claude-code" }, "text": "The agent refused it." }] },
             { "id": "s2", "quest": "q1", "repository": "Game", "state": "failed", "note": "exit 1.",
               "noteParts": [{ "code": "ended.untouched-exit", "values": { "exit": 1 }, "text": "exit 1 before taking its quest." }] },
             { "id": "s3", "quest": "q1", "repository": "Game", "state": "failed", "note": "an older record, with no parts." },
             { "id": "s4", "quest": "q2", "repository": "Game", "state": "failed", "note": "refused.",
               "noteParts": [{ "code": "{{code}}", "values": { "owner": "claude-code" }, "text": "The agent refused it." }] }]
            """);

        Assert.Equal(2, strikes["q1"]);
        Assert.Equal(["q1"], strikes.Keys);
    }

    /// <summary>The words alone spare nothing: a record whose English says it, with no coded line, is a strike as it was.</summary>
    [Fact]
    public void An_account_s_words_with_no_coded_line_are_still_a_strike()
    {
        var strikes = ServiceClient.ReadStrikes("""
            [{ "id": "s1", "quest": "q1", "repository": "Game", "state": "failed",
               "note": "The agent refused the `claude-code` account `work` for its sign-in." }]
            """);

        Assert.Equal(1, strikes["q1"]);
    }

    /// <summary>The last run says whether its stop was interrupted — what the planner carries on from.</summary>
    [Fact]
    public void The_last_run_says_whether_its_stop_was_interrupted()
    {
        var last = ServiceClient.ReadLastRun("""
            [{ "id": "s1", "quest": "q1", "state": "stopped", "interrupted": true, "note": "nothing ran it.",
               "tree": "D:/trees/s1", "created": "2026-09-30T10:00:00Z" },
             { "id": "s2", "quest": "q2", "state": "stopped", "note": "the person stopped it.",
               "created": "2026-09-30T10:00:00Z" }]
            """);

        Assert.Equal(new PriorSession("s1", "D:/trees/s1", "stopped", "nothing ran it.", Interrupted: true), last["q1"]);
        Assert.False(last["q2"].Interrupted);
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

        Assert.Equal(new PriorSession("s2", "D:/trees/s2", "completed"), last["q1"]);
        Assert.Single(last);
    }

    /// <summary>A cut-off carries its own words (D80), which the session that carries on is told.</summary>
    [Fact]
    public void The_last_run_carries_how_it_ended_and_what_it_said()
    {
        var last = ServiceClient.ReadLastRun("""
            [{ "id": "s1", "quest": "q1", "state": "failed", "note": "timed out after 30 minutes and was killed.",
               "tree": "D:/trees/s1", "created": "2026-09-27T10:00:00Z" }]
            """);

        Assert.Equal(new PriorSession("s1", "D:/trees/s1", "failed", "timed out after 30 minutes and was killed."), last["q1"]);
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
