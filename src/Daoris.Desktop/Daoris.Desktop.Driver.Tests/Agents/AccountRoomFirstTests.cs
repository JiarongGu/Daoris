using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A driven start weighs the room each account has left in its tightest window before it counts running sessions: pure,
/// through the walk's order and its pairwise ranking.
/// </summary>
public sealed class AccountRoomFirstTests
{
    private static readonly DateTimeOffset Now = AccountRotationWalkTests.Now;

    private static WindowSaid Window(string name, double used, TimeSpan resetIn) =>
        new(name, used, Now + resetIn, Standing: null, Credits: false, Seen: Now.AddMinutes(-10), Session: "s0");

    private static AccountFacts Account(int running, params WindowSaid[] windows) =>
        new(Running: running, Said: windows.Length == 0 ? null : new AccountSaid(windows));

    private static string Order(Dictionary<string, AccountFacts> facts, string scope = "account-1 account-2 account-3")
    {
        var read = AccountRotationWalkTests.Scope(scope);
        return string.Join(' ', AccountRotation.Order(read, StartKind.Driven, [.. read.List], facts, Now));
    }

    private static readonly TimeSpan SessionLeft = TimeSpan.FromHours(3);

    [Fact]
    public void An_account_at_ten_percent_opens_over_one_at_seventy_five_though_it_runs_more()
    {
        var facts = new Dictionary<string, AccountFacts>
        {
            ["account-1"] = Account(1, Window("session", 0.10, SessionLeft)),
            ["account-2"] = Account(0, Window("session", 0.75, SessionLeft)),
        };

        Assert.Equal("account-1 account-2", Order(facts, "account-1 account-2"));
        Assert.Equal(WalkStep.Headroom, AccountRotation.Rank("account-1", "account-2", facts, Now, AccountRotationWalkTests.Scope("account-1 account-2").Use).Step);
    }

    [Fact]
    public void Two_accounts_in_one_band_of_room_go_by_fewest_running()
    {
        var facts = new Dictionary<string, AccountFacts>
        {
            ["account-1"] = Account(1, Window("session", 0.06, SessionLeft)),
            ["account-2"] = Account(0, Window("session", 0.10, SessionLeft)),
        };

        Assert.Equal("account-2 account-1", Order(facts, "account-1 account-2"));
    }

    [Theory]
    [InlineData("account-1:W10@+4d,r1 account-2:W12@+4d", "account-2 account-1")]
    [InlineData("account-1:W5@+4d,r1 account-2:W60@+4d", "account-1 account-2")]
    public void The_band_holds_weekly_rooms_a_few_points_apart_to_the_sessions_run(string facts, string order)
    {
        Assert.Equal(order, string.Join(' ', AccountRotation.Order(
            AccountRotationWalkTests.Scope("account-1 account-2"), StartKind.Driven, ["account-1", "account-2"],
            AccountRotationWalkTests.Facts(facts), Now)));
    }

    [Fact]
    public void An_account_past_its_curve_still_goes_last()
    {
        var facts = new Dictionary<string, AccountFacts>
        {
            ["account-1"] = Account(0, Window("session", 0.95, SessionLeft)),
            ["account-2"] = Account(3, Window("session", 0.10, SessionLeft)),
        };

        Assert.Equal("account-2 account-1", Order(facts, "account-1 account-2"));
    }

    [Fact]
    public void An_account_that_said_no_number_ranks_as_one_with_no_room_and_never_as_one_with_plenty()
    {
        var facts = new Dictionary<string, AccountFacts>
        {
            ["account-1"] = Account(0),
            ["account-2"] = Account(1, Window("session", 0.10, SessionLeft)),
            ["account-3"] = Account(0, Window("session", 0.88, SessionLeft)),
        };

        // Room 80 first; no number and 2 points of room share a band, so the rest of the walk decides, as it did before.
        Assert.Equal("account-2 account-3 account-1", Order(facts));

        facts["account-3"] = Account(0, Window("session", 0.95, SessionLeft));
        Assert.Equal("account-2 account-1 account-3", Order(facts));
    }

    [Fact]
    public void A_codex_shaped_account_is_weighed_by_its_tightest_window_whatever_the_window()
    {
        var session = CodexUsage.WindowOf(300);
        var week = CodexUsage.WindowOf(10080);
        var facts = new Dictionary<string, AccountFacts>
        {
            // Its week is far from its reset, so it is well under the curve there; the other's week is not.
            ["account-1"] = Account(1, Window(session, 0.10, SessionLeft), Window(week, 0.20, TimeSpan.FromDays(5))),
            ["account-2"] = Account(0, Window(session, 0.10, SessionLeft), Window(week, 0.70, TimeSpan.FromDays(3))),
        };

        Assert.Equal("account-1 account-2", Order(facts, "account-1 account-2"));
    }

    [Fact]
    public void A_window_the_walk_has_never_seen_takes_part_by_what_is_left_of_it()
    {
        var facts = new Dictionary<string, AccountFacts>
        {
            ["account-1"] = Account(0, Window("session", 0.10, SessionLeft), Window("1440-minute", 0.90, TimeSpan.FromHours(5))),
            ["account-2"] = Account(2, Window("session", 0.10, SessionLeft), Window("1440-minute", 0.20, TimeSpan.FromHours(5))),
            ["account-3"] = Account(0, Window("a-window-from-a-later-provider", 0.50, TimeSpan.FromHours(5))),
        };

        Assert.Equal("account-2 account-3 account-1", Order(facts));
    }
}
