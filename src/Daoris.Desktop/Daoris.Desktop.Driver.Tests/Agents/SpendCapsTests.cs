using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A spend cap is a window Daoris learns: its refusal names no time and is read as a cap, each refusal is kept, the wait is when the account
/// reopened before (never an hour's guess), a cap met again at once waits twice as long, and the cool-off holds the account as any other.
/// </summary>
public sealed class SpendCapsTests : IDisposable
{
    private const string Refusal = "the ACP agent refused the call: Internal error: You hit your spend cap set by the owner of your workspace.";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.Utc;

    private static readonly DateTimeOffset Day = new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-spend-caps-" + Guid.NewGuid().ToString("N")[..8]);

    public SpendCapsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static DateTimeOffset At(int hour, int minute) => Day.AddHours(hour).AddMinutes(minute);

    [Fact]
    public void The_refusal_is_a_cap_with_no_time_named()
    {
        var table = ProviderRegistry.Built().AgentNamed("codex-acp")!.Tables.Limits!;

        var seen = AccountLimits.Read(table, Refusal, At(8, 36), Zone);

        Assert.NotNull(seen);
        Assert.True(SpendCaps.Is(seen));
        Assert.False(seen.Stated);
    }

    [Fact]
    public void An_account_never_seen_to_reopen_waits_the_unlearned_wait_and_not_an_hour()
    {
        var (wait, learned) = SpendCaps.WaitAfter([], [], At(8, 36));

        Assert.Equal((SpendCaps.Unlearned, false), (wait, learned));
        Assert.NotEqual(AccountLimits.DefaultCoolOff, wait);
    }

    /// <summary>The ledger's day: capped at 08:36, worked from 09:57, capped again at 10:46: it reopened within 81 minutes before.</summary>
    [Fact]
    public void An_account_that_worked_again_after_a_cap_waits_as_long_as_that_took_before()
    {
        var (wait, learned) = SpendCaps.WaitAfter([At(8, 36)], [At(9, 57), At(10, 30)], At(10, 46));

        Assert.Equal((TimeSpan.FromMinutes(81), true), (wait, learned));
    }

    [Fact]
    public void A_cap_met_again_with_no_work_between_waits_twice_as_long_up_to_the_longest()
    {
        var (second, _) = SpendCaps.WaitAfter([At(8, 36)], [], At(11, 36));
        var (many, _) = SpendCaps.WaitAfter(Enumerable.Range(0, 12).Select(i => At(0, 0).AddHours(i)).ToList(), [], At(12, 0));

        Assert.Equal(SpendCaps.Unlearned * 2, second);
        Assert.Equal(SpendCaps.Latest, many);
    }

    [Fact]
    public void The_cool_off_is_the_learned_wait_marked_as_a_cap_and_each_refusal_is_kept()
    {
        var table = ProviderRegistry.Built().AgentNamed("codex-acp")!.Tables.Limits!;
        var first = AccountLimits.Read(table, Refusal, At(8, 36), Zone)!;

        var learned = SpendCaps.Learn(_home, "codex", "account-1", first, At(8, 36));
        var again = SpendCaps.Learn(_home, "codex", "account-1", first, At(8, 50));

        Assert.Equal((SpendCaps.Window, false, At(8, 36) + SpendCaps.Unlearned), (learned.Window, learned.Stated, learned.Until));
        Assert.Equal(At(8, 50) + SpendCaps.Unlearned * 2, again.Until);
        Assert.Equal([At(8, 36), At(8, 50)], SpendCaps.Moments(_home, "codex", "account-1"));
        Assert.Empty(SpendCaps.Moments(_home, "codex", "account-2"));
    }

    [Fact]
    public void A_cap_cools_the_account_the_session_ran_as_until_the_learned_time_so_nothing_new_starts_on_it()
    {
        var now = At(8, 36);
        var roster = new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => now, Zone = Zone };
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "codex", "account-1"));

        var limited = roster.Limited("codex-acp", "account-1", Refusal, "s1");

        Assert.NotNull(limited);
        var (entry, _) = limited.Value;
        Assert.Equal((SpendCaps.Window, now + SpendCaps.Unlearned), (entry.Window, entry.Until));
        Assert.Equal(CoolingWhy.Learned, CoolingWhy.Of(entry));
        Assert.NotNull(roster.CoolingOf("codex-acp", "account-1"));

        now = now + SpendCaps.Unlearned + TimeSpan.FromMinutes(1);
        Assert.Null(roster.CoolingOf("codex-acp", "account-1"));
    }
}
