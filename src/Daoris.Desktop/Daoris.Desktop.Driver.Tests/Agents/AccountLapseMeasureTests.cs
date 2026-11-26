using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A week lapses where the hours its room under its curve needs at the rate Daoris's sessions reached on the account reach the
/// hours left to its reset, on any day, and then ranks first, ahead of the fewest-sessions step; with no rate measured the last
/// day stands in. The rate is the facts' own (<see cref="AccountFacts.Rate"/>), written here as <c>account=points an hour</c>.
/// </summary>
public sealed class AccountLapseMeasureTests
{
    private static string Order(string scope, string facts, string rates)
    {
        var read = AccountRotationWalkTests.Scope(scope);
        var measured = AccountRotationWalkTests.Facts(facts);
        foreach (var rate in rates.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, points) = (rate[..rate.IndexOf('=')], rate[(rate.IndexOf('=') + 1)..]);
            measured[name] = measured[name] with { Rate = double.Parse(points, System.Globalization.CultureInfo.InvariantCulture) };
        }

        return string.Join(' ', AccountRotation.Order(read, StartKind.Driven, [.. read.List], measured, AccountRotationWalkTests.Now));
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
    // A rate of nothing reached needs no end of hours.
    [InlineData("account-1 account-2", "account-1:W2@+5d account-2:W50@+35h,r1", "account-2=0", "account-2 account-1")]
    public void A_week_lapses_by_the_hours_its_room_needs_at_the_rate_reached_against_the_hours_left(
        string scope, string facts, string rates, string order)
    {
        Assert.Equal(order, Order(scope, facts, rates));
    }
}
