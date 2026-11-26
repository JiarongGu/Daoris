using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A week lapses where the hours its room under its curve needs at the rate Daoris's sessions reached on the account reach the
/// hours left to its reset, on any day, and then ranks first, ahead of the fewest-sessions step; with no rate measured the last
/// day stands in. The rate is the facts' own (<see cref="AccountFacts.Rate"/>), written here as <c>account=points an hour</c>.
/// </summary>
public sealed class AccountLapseMeasureTests
{
    private static readonly DateTimeOffset Now = AccountRotationWalkTests.Now;

    internal static Dictionary<string, AccountFacts> Rated(string facts, string rates)
    {
        var measured = AccountRotationWalkTests.Facts(facts);
        foreach (var rate in rates.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, points) = (rate[..rate.IndexOf('=')], rate[(rate.IndexOf('=') + 1)..]);
            measured[name] = measured[name] with { Rate = double.Parse(points, System.Globalization.CultureInfo.InvariantCulture) };
        }

        return measured;
    }

    private static string Order(string scope, string facts, string rates)
    {
        var read = AccountRotationWalkTests.Scope(scope);
        return string.Join(' ', AccountRotation.Order(read, StartKind.Driven, [.. read.List], Rated(facts, rates), Now));
    }

    [Theory]
    // The 2026-11-26 readings: 46 points at one an hour take 46 hours of the 35 left, so it goes first though the other has fewer sessions.
    [InlineData("account-1 account-2", "account-1:W2@+5d account-2:W50@+35h,r1", "account-1=1 account-2=1", "account-2 account-1")]
    // At two an hour the room is spent in 23 hours: nothing lapses and the order is the fewest sessions'.
    [InlineData("account-1 account-2", "account-1:W2@+5d account-2:W50@+35h,r1", "account-1=2 account-2=2", "account-1 account-2")]
    // No rate measured, none claimed beyond the last day.
    [InlineData("account-1 account-2", "account-1:W2@+5d account-2:W50@+35h,r1", "", "account-1 account-2")]
    // Within the last day a rate that spends the room in time keeps the order, and one that does not goes first.
    [InlineData("account-1 account-2", "account-1:W2@+5d account-2:W60@+8h,r1", "account-2=10", "account-1 account-2")]
    [InlineData("account-1 account-2", "account-1:W2@+5d account-2:W60@+8h,r1", "account-2=1", "account-2 account-1")]
    // A rate of nothing reached is no rate measured: beyond the last day nothing is claimed.
    [InlineData("account-1 account-2", "account-1:W2@+5d account-2:W50@+35h,r1", "account-2=0", "account-1 account-2")]
    public void A_week_lapses_by_the_hours_its_room_needs_at_the_rate_reached_against_the_hours_left(
        string scope, string facts, string rates, string order)
    {
        Assert.Equal(order, Order(scope, facts, rates));
    }

    private static WindowSaid Said(string window, double? used, TimeSpan toReset, TimeSpan ago) =>
        new(window, used, Now + toReset, Standing: null, Credits: false, Seen: Now - ago, Session: "s0");

    [Theory]
    // The five-hour window read with no number, newer than the week's reading, which is eight hours old.
    [InlineData(5, 8)]
    // Both readings eight hours old.
    [InlineData(480, 8)]
    public void An_unknown_shorter_window_and_an_old_reading_hold_the_lapsing_account_back_no_more_than_a_fresh_one(int sessionAgoMinutes, int weekAgoHours)
    {
        var facts = AccountRotationWalkTests.Facts("account-1:W2@+5d");
        facts["account-2"] = new AccountFacts(
            Running: 1, Rate: 1,
            Said: new AccountSaid([Said("session", null, TimeSpan.FromHours(3), TimeSpan.FromMinutes(sessionAgoMinutes)), Said("weekly", 0.50, TimeSpan.FromHours(35), TimeSpan.FromHours(weekAgoHours))]));
        var read = AccountRotationWalkTests.Scope("account-1 account-2");

        Assert.Equal("account-2 account-1", string.Join(' ', AccountRotation.Order(read, StartKind.Driven, [.. read.List], facts, Now)));
    }

    [Fact]
    public void A_required_move_goes_where_the_measured_rate_leaves_more_to_lapse()
    {
        static AccountSaid Week(double used, int hours) =>
            new([Said("session", 0.10, TimeSpan.FromHours(1), TimeSpan.FromSeconds(30)), Said("weekly", used, TimeSpan.FromHours(hours), TimeSpan.FromSeconds(30))]);
        var over = Week(0.97, 84);
        SwitchCandidate[] candidates =
        [
            new("account-2", Week(0.10, 84), Ready: true),
            new("account-3", Week(0.59, 8), Ready: true, Rate: 1),
        ];

        Assert.Equal("account-3", Assert.IsType<SwitchChoice.MoveTo>(SwitchDecision.Decide(RotationUse.Default, "account-1", over, candidates, Now, null)).Account);
        SwitchCandidate[] spent = [candidates[0], candidates[1] with { Rate = 10 }];
        Assert.Equal("account-2", Assert.IsType<SwitchChoice.MoveTo>(SwitchDecision.Decide(RotationUse.Default, "account-1", over, spent, Now, null)).Account);
    }

    [Fact]
    public void The_next_start_says_the_rate_reached_and_the_hours_the_room_needs_against_the_hours_left()
    {
        var read = AccountRotationWalkTests.Scope("account-1 account-2");
        var known = Rated("account-1:W2@+5d account-2:W50@+35h,r1", "account-1=1 account-2=1");
        var tried = AccountRotation.Order(read, StartKind.Driven, [.. read.List], known, Now)
            .Select(name => new AccountState(name, AccountReadiness.Ready)).ToList();

        var next = AccountRotation.Next(read, StartKind.Driven, tried, [.. read.List], known, Now);

        Assert.Equal(("account-2", NextReason.Lapsing), (next.Account, next.Reason));
        Assert.Equal(Now.AddHours(35), next.When);
        Assert.Equal(50, next.Unused);
        // 46 points of room at one an hour is 46 hours, where 35 are left.
        Assert.Equal(1.0, next.Rate);
        Assert.Equal(46, next.Needs);
    }
}
