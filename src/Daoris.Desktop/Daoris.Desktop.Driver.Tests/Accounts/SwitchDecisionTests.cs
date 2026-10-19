using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// When a running session moves to another account by the usage curve (SWITCH1c-i; the switch design §2 and §4; D165): stay,
/// read the others first, or move to the account with the most room, at least the margin under its curve on every window it
/// said. Pure: the readings, the readiness, the moment and the last move are values handed in.
/// </summary>
public sealed class SwitchDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A week half gone: the smart curve there is 70.</summary>
    private const int HalfWeek = 7 * 12 * 60;

    private static readonly RotationUse Smart = RotationUse.Default;

    private static WindowSaid Window(string window, double? used, int minutesToReset, int secondsAgo = 30) =>
        new(window, used, Now.AddMinutes(minutesToReset), Standing: null, Credits: false, Seen: Now.AddSeconds(-secondsAgo), Session: null);

    /// <summary>An account's reading: its week half gone at <paramref name="weekly"/> used, its five hours at <paramref name="session"/>.</summary>
    private static AccountSaid Said(double weekly, double session = 0.10, int secondsAgo = 30) =>
        new([Window("session", session, 60, secondsAgo), Window("weekly", weekly, HalfWeek, secondsAgo)]);

    private static SwitchCandidate Ready(string account, AccountSaid? said) => new(account, said, Ready: true);

    /// <summary>`account-1` over its weekly curve: 75% used where the curve is 70.</summary>
    private static readonly AccountSaid Over = Said(0.75);

    private static SwitchChoice Decide(
        AccountSaid? own, IReadOnlyList<SwitchCandidate> candidates, RotationUse? use = null, DateTimeOffset? lastMove = null, bool othersRead = false) =>
        SwitchDecision.Decide(use ?? Smart, "account-1", own, candidates, Now, lastMove, othersRead);

    [Fact]
    public void An_account_over_its_curve_moves_to_the_candidate_with_the_most_room()
    {
        var choice = Decide(Over, [Ready("account-2", Said(0.55)), Ready("account-3", Said(0.30))]);

        var move = Assert.IsType<SwitchChoice.MoveTo>(choice);
        Assert.Equal("account-3", move.Account);
    }

    [Fact]
    public void A_move_says_the_session_account_s_tightest_window_its_use_and_the_curve_there_now()
    {
        // Five hours at 40% of 90 leaves 50; the week at 75% of 70 is 5 over: the week is the tightest.
        var move = Assert.IsType<SwitchChoice.MoveTo>(Decide(Said(0.75, session: 0.40), [Ready("account-3", Said(0.30))]));

        Assert.Equal(("weekly", 0.75, 70.0), (move.Window, move.Used, Math.Round(move.Curve, 6)));
    }

    [Fact]
    public void Switch_off_stays_however_far_over()
    {
        var choice = Decide(Said(0.99), [Ready("account-3", Said(0.10))], use: Smart with { Switch = "off" });

        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Off), choice);
    }

    [Fact]
    public void An_account_that_said_no_number_stays_since_absent_is_never_over()
    {
        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Unmeasured), Decide(null, [Ready("account-3", Said(0.10))]));
        Assert.Equal(
            new SwitchChoice.Stay(SwitchStay.Unmeasured),
            Decide(new AccountSaid([Window("weekly", null, HalfWeek)]), [Ready("account-3", Said(0.10))]));
    }

    [Theory]
    [InlineData(0.50)]  // 20 points of room: well under
    [InlineData(0.60)]  // exactly the margin: not within it
    public void An_account_with_the_margin_s_room_stays_and_reads_nothing(double weekly)
    {
        var stale = Ready("account-3", Said(0.10, secondsAgo: 600));

        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Room), Decide(Said(weekly), [stale]));
    }

    [Fact]
    public void Within_the_margin_and_a_candidate_s_reading_older_than_a_minute_reads_the_others_first()
    {
        // 65% of 70: five points of room, inside the margin, not yet over.
        var choice = Decide(Said(0.65), [Ready("account-2", Said(0.30)), Ready("account-3", Said(0.10, secondsAgo: 61))]);

        var read = Assert.IsType<SwitchChoice.ReadOthers>(choice);
        Assert.Equal(["account-3"], read.Accounts);
    }

    [Fact]
    public void Within_the_margin_with_every_reading_fresh_stays_as_it_is_not_over()
    {
        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Room), Decide(Said(0.65), [Ready("account-3", Said(0.10, secondsAgo: 60))]));
    }

    [Fact]
    public void On_its_curve_is_not_over_it()
    {
        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Room), Decide(Said(0.70), [Ready("account-3", Said(0.10))]));
    }

    [Fact]
    public void Over_reads_the_ready_candidates_said_long_ago_or_never_in_the_list_s_order()
    {
        var candidates = new[]
        {
            Ready("account-2", Said(0.30, secondsAgo: 120)),
            Ready("account-3", Said(0.30)),
            Ready("account-4", null),
            Ready("account-5", new AccountSaid([])),
            new SwitchCandidate("account-6", Said(0.30, secondsAgo: 600), Ready: false),
        };

        var read = Assert.IsType<SwitchChoice.ReadOthers>(Decide(Over, candidates));

        Assert.Equal(["account-2", "account-4", "account-5"], read.Accounts);
    }

    [Fact]
    public void Once_the_others_were_read_a_reading_still_old_is_weighed_as_it_stands()
    {
        var choice = Decide(Over, [Ready("account-2", Said(0.30, secondsAgo: 240)), Ready("account-3", null)], othersRead: true);

        Assert.Equal("account-2", Assert.IsType<SwitchChoice.MoveTo>(choice).Account);
    }

    [Theory]
    [InlineData(0.61)]  // nine points of room: under the margin
    [InlineData(0.70)]  // on its curve
    [InlineData(0.80)]  // over it too
    public void No_candidate_with_the_margin_s_room_stays(double weekly)
    {
        Assert.Equal(new SwitchChoice.Stay(SwitchStay.NoRoom), Decide(Over, [Ready("account-2", Said(weekly))]));
    }

    [Fact]
    public void The_margin_holds_on_every_window_a_candidate_said()
    {
        // The week has 40 points, the five hours five: its tightest is five.
        Assert.Equal(new SwitchChoice.Stay(SwitchStay.NoRoom), Decide(Over, [Ready("account-2", Said(0.30, session: 0.85))]));
    }

    [Fact]
    public void A_candidate_not_ready_or_with_no_number_is_never_the_target()
    {
        Assert.Equal(new SwitchChoice.Stay(SwitchStay.NoRoom), Decide(Over, [new SwitchCandidate("account-2", Said(0.10), Ready: false)]));
        Assert.Equal(
            new SwitchChoice.Stay(SwitchStay.NoRoom),
            Decide(Over, [Ready("account-2", new AccountSaid([Window("weekly", null, HalfWeek)]))], othersRead: true));
        Assert.Equal(new SwitchChoice.Stay(SwitchStay.NoRoom), Decide(Over, []));
    }

    [Fact]
    public void Equal_room_goes_to_the_first_in_the_scope_s_list()
    {
        var choice = Decide(Over, [Ready("account-4", Said(0.30)), Ready("account-2", Said(0.30))]);

        Assert.Equal("account-4", Assert.IsType<SwitchChoice.MoveTo>(choice).Account);
    }

    [Fact]
    public void The_session_s_own_account_is_never_a_candidate()
    {
        var choice = Decide(Over, [Ready("account-1", Said(0.10)), Ready("account-2", Said(0.30))]);

        Assert.Equal("account-2", Assert.IsType<SwitchChoice.MoveTo>(choice).Account);
    }

    [Fact]
    public void A_move_holds_thirty_minutes_after_the_last_curve_move()
    {
        var candidates = new[] { Ready("account-3", Said(0.30)) };

        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Held), Decide(Over, candidates, lastMove: Now.AddMinutes(-29)));
        Assert.IsType<SwitchChoice.MoveTo>(Decide(Over, candidates, lastMove: Now.AddMinutes(-30)));
    }

    [Fact]
    public void While_held_nothing_is_read_and_an_account_not_over_says_so()
    {
        var stale = new[] { Ready("account-3", Said(0.30, secondsAgo: 600)) };

        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Held), Decide(Over, stale, lastMove: Now.AddMinutes(-5)));
        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Room), Decide(Said(0.65), stale, lastMove: Now.AddMinutes(-5)));
    }

    [Fact]
    public void Under_curve_the_person_s_points_say_when_it_is_over()
    {
        // The person's week flat at 40: 45% is over it, where the smart curve's 70 leaves 25 points.
        var own = Smart with { Switch = "curve", Curve = UseCurve.Flat("weekly", 40) };

        var move = Assert.IsType<SwitchChoice.MoveTo>(Decide(Said(0.45), [Ready("account-3", Said(0.10))], use: own));

        Assert.Equal(("account-3", "weekly", 40.0), (move.Account, move.Window, move.Curve));
        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Room), Decide(Said(0.45), [Ready("account-3", Said(0.10))]));
    }

    // ——— What would lapse chooses only where a required move goes (USAGERESET1, the switch design §4): among the candidates
    // with the margin's room, the most of a week that would lapse per hour to its reset first, then the most room.

    /// <summary>A candidate whose week resets in <paramref name="hours"/> with <paramref name="weekly"/> of it used.</summary>
    private static AccountSaid Lapsing(double weekly, int hours, double session = 0.10) =>
        new([Window("session", session, 60), Window("weekly", weekly, hours * 60)]);

    [Fact]
    public void A_required_move_goes_where_more_would_lapse_before_where_there_is_more_room()
    {
        // account-2: 60 points of room half a week off; account-3: 40 points of room resetting in 8 hours, 5 an hour.
        var choice = Decide(Over, [Ready("account-2", Said(0.10)), Ready("account-3", Lapsing(0.59, 8))]);

        Assert.Equal("account-3", Assert.IsType<SwitchChoice.MoveTo>(choice).Account);
        // Of two lapsing, the more an hour; the margin still holds on every window.
        Assert.Equal("account-2", Assert.IsType<SwitchChoice.MoveTo>(
            Decide(Over, [Ready("account-3", Lapsing(0.59, 20)), Ready("account-2", Lapsing(0.59, 8))])).Account);
        Assert.Equal("account-2", Assert.IsType<SwitchChoice.MoveTo>(
            Decide(Over, [Ready("account-3", Lapsing(0.59, 8, session: 0.85)), Ready("account-2", Said(0.10))])).Account);
    }

    [Fact]
    public void A_healthy_session_never_moves_to_chase_a_reset()
    {
        var lapsing = new[] { Ready("account-3", Lapsing(0.10, 2)) };

        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Room), Decide(Said(0.30), lapsing));
        // Within the margin and not over: still no move.
        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Room), Decide(Said(0.65), lapsing));
    }

    [Fact]
    public void The_hold_the_freshness_and_the_margin_are_the_design_s()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), SwitchDecision.Hold);
        Assert.Equal(TimeSpan.FromMinutes(1), SwitchDecision.Fresh);
        Assert.Equal(10, UsageCurves.SwitchMargin);
    }
}
