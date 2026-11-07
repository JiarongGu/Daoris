using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// D210 row 1c, TOOL4n, LIMITRESUME1f: a native run whose last turn its account's limit ended concludes as the protocol door's refused turn
/// does. A driven run's conclusion reads the limit as its turn's failure, so the account cools there once and the record says <c>limit</c>; a
/// conversation's cooling line stands before its turn's end, which reads it limited, so its line moves to the next account by its rules.
/// </summary>
public sealed class NativeLimitConcludesTests
{
    private static readonly AgentError Limit = new(
        AgentErrorKind.Limit, DoorFailures.OfResult("You've hit your limit · resets 5pm"), "own:words:limit",
        new LimitSeen("limit", null, DateTimeOffset.UnixEpoch.AddHours(5), Stated: true, AssumedZone: false));

    private static WireReading Failed(string words) => new WireReading.Failed(DoorFailures.OfResult(words));

    private static WireReading TurnEnded(string stop) => new WireReading.Said(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = stop });

    private static NativeLimits Reading(List<AgentError>? met = null) =>
        new(failure => failure.Words == Limit.Failure.Words ? Limit : Limit with { Kind = AgentErrorKind.Fatal }, met is null ? null : met.Add);

    [Fact]
    public void A_driven_native_run_whose_last_turn_a_limit_ended_concludes_on_it_as_the_protocol_door_s_refused_turn()
    {
        var limits = Reading();
        limits.Read(Failed("You've hit your limit · resets 5pm"));
        limits.Read(TurnEnded("success"));

        var ending = new HarnessEnding(1, "You've hit your limit · resets 5pm", []);

        Assert.Equal("You've hit your limit · resets 5pm", NativeLimits.TurnFailed(protocol: false, ending, limits));
    }

    [Fact]
    public void The_recorded_session_limit_on_a_failed_native_result_is_its_limit_until_the_reset_in_its_named_zone()
    {
        var built = ProviderRegistry.Built();
        var seen = new DateTimeOffset(2026, 11, 7, 14, 8, 34, TimeSpan.Zero);
        var limits = new NativeLimits(failure => AgentErrors.Read(built, built.Door("claude-code"), failure, seen, TimeZoneInfo.Utc));

        limits.Read(Failed("You've hit your session limit · resets 3am (Australia/Sydney)"));
        limits.Read(TurnEnded("success"));

        Assert.Equal(AgentErrorKind.Limit, limits.Ended?.Kind);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 3, 2, 0, TimeSpan.FromHours(11)), limits.Ended!.Limit!.Until);
    }

    [Fact]
    public void A_failed_native_result_that_is_no_limit_stays_the_harness_s_own_words_and_concludes_on_no_turn_failure()
    {
        var limits = Reading();
        limits.Read(Failed("API Error: 500 overloaded"));
        limits.Read(TurnEnded("error_during_execution"));

        Assert.Null(limits.Ended);
        Assert.Null(NativeLimits.TurnFailed(protocol: false, new HarnessEnding(1, "API Error: 500 overloaded", []), limits));
    }

    [Fact]
    public void The_protocol_door_s_refused_turn_is_read_as_before_whatever_the_native_part_heard()
    {
        var limits = Reading();
        limits.Read(Failed("You've hit your limit · resets 5pm"));
        limits.Read(TurnEnded("success"));

        Assert.Equal("refused", NativeLimits.TurnFailed(protocol: true, new HarnessEnding(0, "refused", []), limits));
        Assert.Null(NativeLimits.TurnFailed(protocol: true, new HarnessEnding(0, null, []), null));
    }

    [Fact]
    public void A_later_turn_that_ended_otherwise_leaves_the_run_on_no_limit_and_the_limit_is_told_once()
    {
        List<AgentError> met = [];
        var limits = Reading(met);

        limits.Read(Failed("You've hit your limit · resets 5pm"));
        limits.Read(TurnEnded("success"));
        limits.Read(TurnEnded("end_turn"));

        Assert.Single(met);
        Assert.Null(limits.Ended);
        Assert.Null(NativeLimits.TurnFailed(protocol: false, new HarnessEnding(0, null, []), limits));
    }

    [Fact]
    public void A_native_conversation_s_turn_its_limit_ended_reads_limited_from_its_record_as_a_protocol_one_does()
    {
        IReadOnlyList<SessionEvent> events =
        [
            new() { Kind = SessionEventKind.User, Origin = "person", Text = "Make the port configurable." },
            new() { Kind = SessionEventKind.Note, Text = "The account cools…", Parts = [new NotePart { Code = AccountNoteCodes.AccountCooling.Code }] },
            new() { Kind = SessionEventKind.Turn, StopReason = "success" },
        ];

        var held = LimitHeld.Of(events);

        Assert.NotNull(held);
        Assert.Equal(["Make the port configurable."], held.Words.Select(word => word.Text));
    }

    [Theory]
    [InlineData("end_turn")]
    [InlineData("cancelled")]
    public void A_turn_that_ended_cleanly_or_was_stopped_after_a_cooling_line_is_not_limited(string stop)
    {
        IReadOnlyList<SessionEvent> events =
        [
            new() { Kind = SessionEventKind.User, Origin = "person", Text = "Go on." },
            new() { Kind = SessionEventKind.Note, Text = "The account cools…", Parts = [new NotePart { Code = AccountNoteCodes.AccountCooling.Code }] },
            new() { Kind = SessionEventKind.Turn, StopReason = stop },
        ];

        Assert.Null(LimitHeld.Of(events));
    }
}
