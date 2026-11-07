using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Host policy: the reset bounds live in one function, <see cref="ResetBounds.Limit"/>, over a reset as
/// it is stated, which a sentence's reading and the layer's ingress both call. The margin is added to a stated reset, a dated one just past
/// is given the grace, one beyond the furthest is not believed; a time of day or a weekday never has grace, and an absolute moment a field
/// gave counts as dated. These are Daoris's own types, never a proof that the layer supplies the reset.
/// </summary>
public sealed class HostPolicyResetTests
{
    private static readonly DateTimeOffset Seen = new(2026, 11, 6, 16, 0, 30, TimeSpan.Zero);

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    [Fact]
    public void Host_policy_adds_the_margin_to_a_stated_reset_and_keeps_its_zone_as_stated()
    {
        var reset = new StatedReset(Seen.AddHours(3), ResetPrecision.DateAndTime, AssumedZone: true);

        var seen = ResetBounds.Limit("weekly", AccountWindows.Weekly, reset, Seen, Hour);

        Assert.Equal(new LimitSeen("weekly", AccountWindows.Weekly, Seen.AddHours(3) + AccountLimits.Margin, Stated: true, AssumedZone: true), seen);
    }

    [Theory]
    [InlineData(ResetPrecision.Absolute, true)]
    [InlineData(ResetPrecision.DateAndTime, true)]
    [InlineData(ResetPrecision.WeekdayAndTime, false)]
    [InlineData(ResetPrecision.TimeOfDay, false)]
    [InlineData(ResetPrecision.RelativeWait, false)]
    [InlineData(ResetPrecision.Resolved, false)]
    public void Host_policy_gives_a_reset_just_past_the_grace_only_where_it_is_dated(ResetPrecision precision, bool graced)
    {
        var justPast = new StatedReset(Seen.AddSeconds(-30), precision, AssumedZone: false);

        var seen = ResetBounds.Limit("weekly", null, justPast, Seen, Hour);

        Assert.Equal(
            graced
                ? new LimitSeen("weekly", null, Seen + AccountLimits.Margin, Stated: true, AssumedZone: false)
                : new LimitSeen("weekly", null, Seen + Hour, Stated: false, AssumedZone: false, NotBelieved: true),
            seen);
    }

    // The grace is asked of this one function, by the sentence's reading choosing among the moments a date names as by the bounds themselves.
    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(-15, true)]
    [InlineData(-16, false)]
    [InlineData(1, false)]
    public void The_grace_is_one_question_a_moment_just_behind_the_clock_up_to_the_grace_and_no_further(int minutes, bool inGrace) =>
        Assert.Equal(inGrace, ResetBounds.InGrace(Seen.AddMinutes(minutes), Seen));

    [Fact]
    public void Host_policy_believes_no_reset_beyond_the_furthest_and_cools_an_unstated_one_for_the_default()
    {
        var far = new StatedReset(Seen + AccountLimits.Furthest + TimeSpan.FromMinutes(1), ResetPrecision.Absolute, AssumedZone: false);
        Assert.Equal(
            new LimitSeen("usage", "weekly", Seen + Hour, Stated: false, AssumedZone: false, NotBelieved: true),
            ResetBounds.Limit("usage", "weekly", far, Seen, Hour));

        Assert.Equal(new LimitSeen("usage", null, Seen + Hour, Stated: false, AssumedZone: false), ResetBounds.Limit("usage", null, null, Seen, Hour));
    }

    /// <summary>A sentence's reading reaches its bounds through the same function: a dated reset at the grace's edge, read both ways.</summary>
    [Fact]
    public void Host_policy_reads_a_sentence_s_reset_through_the_same_bounds()
    {
        var words = new LimitWords([@"\byou've hit your (?<hit>.+?) limit$"], [@"^resets (?<when>.+?)(?: \((?<zone>[^)]+)\))?$"], []);
        var seen = new DateTimeOffset(2026, 10, 6, 22, 10, 0, TimeSpan.Zero);

        var read = AccountLimits.Read(words, "You've hit your weekly limit · resets Oct 6, 10pm (UTC)", seen, TimeZoneInfo.Utc, Hour);

        Assert.Equal(
            ResetBounds.Limit("weekly", null, new StatedReset(new DateTimeOffset(2026, 10, 6, 22, 0, 0, TimeSpan.Zero), ResetPrecision.DateAndTime, false), seen, Hour),
            read);
        Assert.Equal(seen + AccountLimits.Margin, read!.Until);
    }
}
