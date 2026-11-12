using System.Text.Json;
using Daoris.Driver;
using Lyntai.Agents;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The layer's last three readings of a failed or limited turn, each translated before the first move reads it: the two access readings
/// kept apart for the account readers, the limit's window, scope and stated reset under D125's bounds, and an allowance report read by the
/// one reader the native door's rate-limit frame goes through. On failures and events the tests make.
/// </summary>
public sealed class LayerLimitReadingsTests
{
    private static readonly DateTimeOffset Seen = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private static NormalizedFailure Read(AgentFailure failure) =>
        LayerFailures.Normalize(failure, "native", DoorWhere.Prompt, "words", Seen, Hour);

    private static AgentFailure Limit(RestrictionRecovery recovery, string? window = null, string? scope = null) =>
        new(AgentFailureKind.Limit, "tests:limit")
        {
            Restriction = new AgentRestriction(RestrictionCause.Quota, RestrictionSubject.Account, recovery) { Window = window, Scope = scope },
        };

    [Fact]
    public void An_access_failure_keeps_its_field_and_its_words_apart_for_each_account_reader()
    {
        var failure = new AgentFailure(AgentFailureKind.Access, "tests:access")
        {
            AccessReadings = new AgentAccessReadings(Lyntai.Agents.AccessReason.CredentialRefused, Lyntai.Agents.AccessReason.SignInAgain),
        };

        var access = Read(failure).Access!;

        Assert.Equal(Daoris.Driver.AccessReason.CredentialRefused, access.Field);
        Assert.True(access.WordsSignIn);
        Assert.False(access.WordsRefused);
        Assert.Equal(AccessDisposition.SignedOutByCode, AgentErrors.OfNormalized(Read(failure)).Access);
    }

    [Fact]
    public void An_access_failure_with_no_reading_and_every_other_kind_carry_no_evidence()
    {
        Assert.Null(Read(new AgentFailure(AgentFailureKind.Access, "tests:access")).Access);
        Assert.Null(Read(new AgentFailure(AgentFailureKind.Transient, "tests:transient")
        {
            AccessReadings = new AgentAccessReadings(null, Lyntai.Agents.AccessReason.SignInAgain),
        }).Access);
    }

    [Fact]
    public void An_owner_action_s_reason_crosses_as_daoris_s_own()
    {
        var read = Read(new AgentFailure(AgentFailureKind.Limit, "tests:credit")
        {
            Restriction = new AgentRestriction(
                RestrictionCause.Credit, RestrictionSubject.Account, RestrictionRecovery.OwnerActionFor(OwnerActionReason.Refill)),
        });

        Assert.Equal(OwnerNeed.Refill, read.Restriction!.Recovery.Need);
    }

    [Fact]
    public void A_stated_moment_is_dated_so_a_reset_just_behind_the_clock_is_a_disagreement_of_clocks()
    {
        var at = Seen.AddMinutes(-5);
        var recovery = RestrictionRecovery.ResetAt(at) with { Stated = new StatedRecoveryParts(StatedTimeShape.Moment) { Moment = at } };

        var limit = Read(Limit(recovery)).Limit!;

        Assert.Equal((Seen + AccountLimits.Margin, true, false), (limit.Until, limit.Stated, limit.NotBelieved));
    }

    [Fact]
    public void A_stated_time_of_day_is_never_graced_and_a_wait_is_counted_from_now()
    {
        var at = Seen.AddMinutes(-5);
        var timeOfDay = RestrictionRecovery.ResetAt(at)
            with { Stated = new StatedRecoveryParts(StatedTimeShape.TimeOfDay) { Time = new TimeOnly(8, 55), ZoneNamed = false } };
        Assert.True(Read(Limit(timeOfDay)).Limit!.NotBelieved);

        var wait = RestrictionRecovery.RetryNotBefore(Seen.AddMinutes(10))
            with { Stated = new StatedRecoveryParts(StatedTimeShape.Wait) { Wait = TimeSpan.FromMinutes(10) } };
        var limit = Read(Limit(wait)).Limit!;
        Assert.Equal((Seen.AddMinutes(10) + AccountLimits.Margin, true), (limit.Until, limit.Stated));
    }

    [Fact]
    public void A_time_with_no_zone_named_is_read_in_the_machine_s_zone_and_said_so()
    {
        var at = Seen.AddHours(2);
        var recovery = RestrictionRecovery.ResetAt(at)
            with { Stated = new StatedRecoveryParts(StatedTimeShape.TimeOfDay) { Time = TimeOnly.FromDateTime(at.DateTime), ZoneNamed = false } };

        Assert.True(Read(Limit(recovery)).Limit!.AssumedZone);
    }

    [Fact]
    public void A_limit_s_window_is_daoris_s_word_for_it_and_a_scope_names_what_is_hit()
    {
        var recovery = RestrictionRecovery.ResetAt(Seen.AddHours(5));

        Assert.Equal("session", Read(Limit(recovery, window: "five_hour")).Limit!.Window);
        Assert.Equal("weekly", Read(Limit(recovery, window: "seven_day")).Limit!.Window);
        Assert.Equal("some_window", Read(Limit(recovery, window: "some_window")).Limit!.Window);

        var scoped = Read(Limit(recovery, window: "seven_day", scope: "opus")).Limit!;
        Assert.Equal("opus", scoped.Hit);
        Assert.Equal("opus", ModelLimits.In(scoped, ClaudeWindows.Words.Models));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static JsonElement? Said(AgentAllowanceReport report)
    {
        var readings = new LayerReadings("native", new FixedClock(Seen), Hour).Read(report);
        return readings.Count == 0 ? null : Assert.IsType<WireReading.WindowsSaid>(Assert.Single(readings)).Windows;
    }

    [Fact]
    public void An_allowance_report_is_read_by_the_one_reader_the_native_rate_limit_frame_goes_through()
    {
        var resets = Seen.AddHours(2);
        var report = new AgentAllowanceReport(
            "allowed_warning",
            new AgentAllowanceWindow(AgentAllowanceWindow.FiveHour, 88, TimeSpan.FromHours(5), resets),
            LimitScope: null, IsUsingOverage: true, OverageResetsAt: null);

        var readings = AccountReadings.Read(Said(report)!.Value, ClaudeWindows.Words);

        var window = Assert.Single(readings);
        Assert.Equal(("session", AccountReadings.WarnedWord, true), (window.Window, window.Standing, window.Credits));
        Assert.Equal(0.88, window.Used!.Value, 3);
        Assert.Equal(resets, window.Reset);
    }

    [Fact]
    public void A_weekly_report_names_the_weekly_window_and_one_with_no_window_or_no_reset_says_nothing()
    {
        var resets = Seen.AddDays(3);
        var weekly = new AgentAllowanceReport(
            "rejected", new AgentAllowanceWindow(AgentAllowanceWindow.Weekly, 100, TimeSpan.FromDays(7), resets), null, null, null);
        var read = Assert.Single(AccountReadings.Read(Said(weekly)!.Value, ClaudeWindows.Words));
        Assert.Equal((AccountWindows.Weekly, AccountReadings.RefusedWord, 1.0), (read.Window, read.Standing, read.Used));

        Assert.Null(Said(new AgentAllowanceReport("allowed", null, null, null, null)));
        var noReset = new AgentAllowanceReport(
            "allowed", new AgentAllowanceWindow(AgentAllowanceWindow.FiveHour, 10, TimeSpan.FromHours(5), null), null, null, null);
        Assert.Empty(AccountReadings.Read(Said(noReset)!.Value, ClaudeWindows.Words).Where(each => each.Reset == default));
    }
}
