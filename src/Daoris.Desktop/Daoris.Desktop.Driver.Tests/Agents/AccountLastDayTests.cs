namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Within a week's last day no curve holds and its remaining room is spent first, so an account at 81% of its week with four
/// hours to its reset still staffs a start before an account with more room.
/// </summary>
public sealed class AccountLastDayTests
{
    [Theory]
    [InlineData("account-1 account-2 account-3", "account-3:W81@+4h account-2:W20@+3d", "account-3 account-2 account-1")]
    // Before fewest running sessions as well.
    [InlineData("account-1 account-2 account-3", "account-3:W81@+4h,r2 account-2:W20@+3d", "account-3 account-2 account-1")]
    // A curve the person set does not hold the last day either: 81% is over a flat 10 but is room a day from the reset.
    [InlineData("account-1 account-2 account-3 switch=curve curve=weekly=0:10,100:10", "account-3:W81@+4h account-2:W5@+3d", "account-3 account-2 account-1")]
    // Beyond the last day the curve still holds: the same week a day and a half off is over it and goes last.
    [InlineData("account-1 account-2 account-3 switch=curve curve=weekly=0:10,100:10", "account-3:W81@+36h account-2:W5@+3d", "account-2 account-1 account-3")]
    public void The_last_day_of_a_week_is_under_no_curve_and_its_room_is_spent_first(string scope, string facts, string order)
    {
        Assert.Equal(order, AccountRotationWalkTests.Order(scope, facts));
    }
}
