using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The usage curve, measured (SWITCH1b-i; the switch design §2; D165 point 2): how much of each window an account may have
/// used by now, and how much room it has. Pure: the moment is a value handed in.
/// </summary>
public sealed class UsageCurvesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("session", 5 * 60, 0.0)]       // resets in a full window: just begun
    [InlineData("session", 150, 50.0)]         // half left
    [InlineData("session", 0, 100.0)]          // resets now
    [InlineData("session", -30, 100.0)]        // past its reset: clamped
    [InlineData("session", 6 * 60, 0.0)]       // reset further than the window: clamped
    [InlineData("weekly", 7 * 24 * 60, 0.0)]
    [InlineData("weekly", 7 * 12 * 60, 50.0)]
    [InlineData("weekly", 0, 100.0)]
    public void Elapsed_share_of_a_window(string window, int minutesToReset, double expected) =>
        Assert.Equal(expected, UsageCurves.Elapsed(window, Now.AddMinutes(minutesToReset), Now)!.Value, 6);

    [Fact]
    public void Window_lengths_are_five_hours_and_seven_days()
    {
        Assert.Equal(TimeSpan.FromHours(5), UsageCurves.Length("session"));
        Assert.Equal(TimeSpan.FromDays(7), UsageCurves.Length("weekly"));
        Assert.Null(UsageCurves.Length("monthly"));
    }

    [Theory]
    [InlineData(0, 40)]
    [InlineData(50, 40)]
    [InlineData(100, 40)]
    public void A_one_point_curve_is_flat(double elapsed, double expected) =>
        Assert.Equal(expected, UsageCurves.Value([new CurvePoint(10, 40)], elapsed), 6);

    [Theory]
    [InlineData(0, 10)]     // before the first point: the first's used
    [InlineData(20, 10)]
    [InlineData(40, 30)]    // between the first and second
    [InlineData(60, 50)]    // the middle point
    [InlineData(75, 70)]    // between the second and third
    [InlineData(90, 90)]
    [InlineData(100, 90)]   // after the last: the last's used
    public void A_three_point_curve_joins_its_points_with_straight_lines(double elapsed, double expected) =>
        Assert.Equal(expected, UsageCurves.Value([new CurvePoint(20, 10), new CurvePoint(60, 50), new CurvePoint(90, 90)], elapsed), 6);

    // USAGERESET1: from three-quarters gone the week's tail rises evenly to the whole week at its reset, never in a jump, so
    // the last 5% is spent rather than left to lapse, and headroom shrinks only as the reset nears.
    [Theory]
    [InlineData(0, 20)]
    [InlineData(50, 70)]
    [InlineData(75, 95)]
    [InlineData(80, 96)]
    [InlineData(90, 98)]
    [InlineData(99, 99.8)]
    [InlineData(100, 100)]
    public void The_smart_weekly_is_its_pace_plus_twenty_to_ninety_five_then_rises_evenly_to_a_hundred_at_its_reset(double elapsed, double expected) =>
        Assert.Equal(expected, UsageCurves.Value(UsageCurves.Smart("weekly")!, elapsed), 6);

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void The_smart_session_is_flat_at_the_rotation_use_number(double elapsed) =>
        Assert.Equal(RotationUse.SmartSession, UsageCurves.Value(UsageCurves.Smart("session")!, elapsed), 6);

    [Fact]
    public void A_window_this_build_does_not_know_has_no_curve() => Assert.Null(UsageCurves.Smart("monthly"));

    private static RotationUse Use(string mode, UseCurve? curve = null) =>
        RotationUse.Default with { Switch = mode, Curve = curve ?? UseCurve.None };

    [Fact]
    public void Under_curve_a_window_the_person_sets_takes_their_points_and_the_rest_the_smart_curve()
    {
        var weeklyOnly = new UseCurve(new Dictionary<string, IReadOnlyList<CurvePoint>> { ["weekly"] = [new CurvePoint(0, 30), new CurvePoint(100, 60)] });
        var use = Use("curve", weeklyOnly);

        Assert.Equal([new CurvePoint(0, 30), new CurvePoint(100, 60)], UsageCurves.For(use, "weekly"));
        Assert.Equal(UsageCurves.Smart("session"), UsageCurves.For(use, "session"));
    }

    [Theory]
    [InlineData("smart")]
    [InlineData("off")]
    public void Under_smart_and_off_the_smart_curve_even_when_the_person_kept_points(string mode)
    {
        var kept = UseCurve.Flat("weekly", 10);
        Assert.Equal(UsageCurves.Smart("weekly"), UsageCurves.For(Use(mode, kept), "weekly"));
    }

    private static WindowReading Reading(string window, double used, int minutesToReset) =>
        new(window, used, Now.AddMinutes(minutesToReset));

    [Fact]
    public void Room_is_the_curve_now_less_the_used_percent()
    {
        // Weekly half through: smart is 70; used 0.25 -> 45 points of room.
        Assert.Equal(45, UsageCurves.Room(Use("smart"), Reading("weekly", 0.25, 7 * 12 * 60), Now)!.Value, 6);
        // Over its curve: room below zero.
        Assert.Equal(-5, UsageCurves.Room(Use("smart"), Reading("session", 0.95, 60), Now)!.Value, 6);
    }

    [Fact]
    public void A_reading_with_no_used_is_not_measured()
    {
        Assert.Null(UsageCurves.Room(Use("smart"), new WindowReading("session", null, Now.AddHours(1)), Now));
        Assert.Null(UsageCurves.Room(Use("smart"), Reading("monthly", 0.1, 60), Now));
    }

    [Fact]
    public void Tightest_is_the_least_room_over_the_windows_read()
    {
        // session 40% used of 90 -> 50; weekly at 75% elapsed 80% used of 95 -> 15.
        var readings = new[] { Reading("session", 0.40, 60), Reading("weekly", 0.80, 7 * 24 * 60 / 4) };
        Assert.Equal(15, UsageCurves.Tightest(Use("smart"), readings, Now)!.Value, 6);
    }

    [Fact]
    public void A_window_with_no_reading_is_not_measured_and_no_readings_is_no_figure()
    {
        Assert.Equal(50, UsageCurves.Tightest(Use("smart"), [Reading("session", 0.40, 60)], Now)!.Value, 6);
        Assert.Null(UsageCurves.Tightest(Use("smart"), [], Now));
    }

    [Fact]
    public void Over_its_curve_is_tightest_room_below_zero()
    {
        Assert.True(UsageCurves.Over(Use("smart"), [Reading("session", 0.95, 60)], Now));
        Assert.False(UsageCurves.Over(Use("smart"), [Reading("session", 0.90, 60)], Now));
        Assert.False(UsageCurves.Over(Use("smart"), [], Now));
    }

    [Fact]
    public void The_switch_margin_is_ten_points() => Assert.Equal(10, UsageCurves.SwitchMargin);

    // ——— What an account's agent said, measured (SWITCH1b-ii): the walk ranks by its tightest window, and its reasons say
    // that window, its use and the curve's value there now.

    private static WindowSaid Said(string window, double? used, int minutesToReset) =>
        new(window, used, Now.AddMinutes(minutesToReset), Standing: null, Credits: false, Seen: Now.AddMinutes(-5), Session: "s0");

    [Fact]
    public void The_curve_now_on_a_window_is_its_value_at_the_share_gone()
    {
        Assert.Equal(70, UsageCurves.Value(Use("smart"), "weekly", Now.AddMinutes(7 * 12 * 60), Now)!.Value, 6);
        Assert.Equal(RotationUse.SmartSession, UsageCurves.Value(Use("smart"), "session", Now.AddMinutes(60), Now)!.Value, 6);
        Assert.Equal(40, UsageCurves.Value(Use("curve", UseCurve.Flat("session", 40)), "session", Now.AddMinutes(60), Now)!.Value, 6);
        Assert.Null(UsageCurves.Value(Use("smart"), "monthly", Now.AddMinutes(60), Now));
    }

    [Fact]
    public void An_account_s_tightest_window_is_the_one_with_least_room_with_its_curve_now()
    {
        // session 40% used of 90 -> 50; weekly at 75% elapsed 80% used of 95 -> 15.
        var said = new AccountSaid([Said("session", 0.40, 60), Said("weekly", 0.80, 7 * 24 * 60 / 4)]);

        var tightest = UsageCurves.TightestSaid(Use("smart"), said, Now)!.Value;

        Assert.Equal(("weekly", 95.0, 15.0), (tightest.Window.Window, Math.Round(tightest.Curve, 6), Math.Round(tightest.Room, 6)));
    }

    [Fact]
    public void An_account_that_said_no_number_or_nothing_has_no_tightest_window()
    {
        Assert.Null(UsageCurves.TightestSaid(Use("smart"), null, Now));
        Assert.Null(UsageCurves.TightestSaid(Use("smart"), new AccountSaid([Said("session", null, 60)]), Now));
        Assert.Null(UsageCurves.TightestSaid(Use("smart"), new AccountSaid([Said("monthly", 0.99, 60)]), Now));
    }
}
