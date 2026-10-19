using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// USAGEPROBE1 (D165, D179): each account's use projected from its last reading plus Daoris's own turns since, by the points
/// its readings rose over the work its turns spent, and when a start owes the maker a question: never a guess upward from
/// nothing, and never near or over by a projection (D130 point 9). Pure but for the history built from USAGECAP1a2's shape.
/// </summary>
public sealed class UsageProjectionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-projection-" + Guid.NewGuid().ToString("N")[..8]);

    public UsageProjectionTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static ReadPoint Read(string window, double used, DateTimeOffset reset, double hoursAgo, int? epoch = 1) =>
        new(window, used, reset, Now.AddHours(-hoursAgo), epoch);

    private static SpentTurn Turn(double hoursAgo, double work, int? epoch = 1) => new(Now.AddHours(-hoursAgo), work, epoch);

    private static WindowSaid Said(string window, double used, DateTimeOffset reset, double minutesAgo, string? standing = null, bool credits = false) =>
        new(window, used, reset, standing, credits, Now.AddMinutes(-minutesAgo), null, AccountWindows.SourceRead);

    // ——— The work a turn spent.

    [Fact]
    public void A_turn_s_work_weighs_each_kind_by_the_table_and_a_count_not_said_is_never_zero_made_up()
    {
        // Input 1, output 5, a cache read 0.1, a cache write 1.25 (D179 point 3).
        Assert.Equal(3000, UsageProjection.Work(new TurnTokens(1000, 100, 10_000, 400)));
        Assert.Equal(1000, UsageProjection.Work(new TurnTokens(1000, null, null, null)));
        Assert.Null(UsageProjection.Work(new TurnTokens(null, null, null, null)));
        Assert.Null(UsageProjection.Work(null));
    }

    // ——— The ratio.

    [Fact]
    public void The_ratio_is_the_points_its_readings_rose_over_the_work_its_turns_spent_each_inside_one_reset()
    {
        var (first, second) = (Now.AddHours(-20), Now.AddHours(-2));
        ReadPoint[] readings =
        [
            // A window that has turned over: 10% to 14% on 200,000.
            Read("session", 0.10, first, 24), Read("session", 0.12, first, 23), Read("session", 0.14, first, 22),
            // The next: begun again at 1%, which is no drop, then 1% to 4% on 150,000.
            Read("session", 0.01, second, 6), Read("session", 0.04, second, 5),
        ];
        SpentTurn[] turns = [Turn(23.5, 100_000), Turn(22.5, 100_000), Turn(5.5, 150_000)];

        Assert.Equal(0.07 / 350_000, UsageProjection.Ratio(readings, turns, "session", Now)!.Value, 12);
        // Another window's readings say nothing of this one's.
        Assert.Null(UsageProjection.Ratio(readings, turns, "weekly", Now));
    }

    [Fact]
    public void Too_little_history_is_no_ratio_never_a_guess()
    {
        var reset = Now.AddHours(2);
        // Four points of rise is under the five a ratio is read on (D179 point 4's interval).
        Assert.Null(UsageProjection.Ratio([Read("session", 0.10, reset, 3), Read("session", 0.14, reset, 1)], [Turn(2, 100_000)], "session", Now));
        // A rise with no turn of Daoris's beside it is someone else's use: nothing to divide by.
        Assert.Null(UsageProjection.Ratio([Read("session", 0.10, reset, 3), Read("session", 0.40, reset, 1)], [], "session", Now));
        // A run of one reading rose by nothing it can say.
        Assert.Null(UsageProjection.Ratio([Read("session", 0.10, reset, 3)], [Turn(2, 100_000)], "session", Now));
    }

    [Fact]
    public void Readings_past_the_horizon_another_epoch_s_turns_and_a_drop_inside_one_reset_are_not_read_as_a_rise()
    {
        var reset = Now.AddDays(3);
        var old = Now.AddDays(-UsageProjection.Horizon.TotalDays - 1);
        ReadPoint[] readings =
        [
            new("weekly", 0.10, reset, old, 1), new("weekly", 0.60, reset, old.AddHours(1), 1),
            // A drop beside the same reset is a window begun again that did not say so: the run ends there.
            Read("weekly", 0.50, reset, 30), Read("weekly", 0.20, reset, 29), Read("weekly", 0.28, reset, 28),
        ];
        SpentTurn[] turns = [new(old.AddMinutes(30), 1_000_000, 1), Turn(29.5, 1_000_000), Turn(28.5, 400_000), Turn(28.4, 9_000_000, epoch: 2)];

        Assert.Equal(0.08 / 400_000, UsageProjection.Ratio(readings, turns, "weekly", Now)!.Value, 12);
    }

    // ——— The projection.

    [Fact]
    public void The_projection_is_the_last_reading_plus_the_work_since_by_the_ratio_with_when_and_how()
    {
        var (gone, reset) = (Now.AddHours(-20), Now.AddHours(3));
        ReadPoint[] readings = [Read("session", 0.10, gone, 24), Read("session", 0.30, gone, 22)];
        SpentTurn[] turns = [Turn(23, 2_000_000), Turn(0.1, 1_500_000)];
        var said = new AccountSaid([Said("session", 0.40, reset, 10), Said("weekly", 0.25, Now.AddDays(4), 10)]);

        var projected = UsageProjection.Of(said, readings, turns, Now)!;

        var session = projected.Of("session")!;
        Assert.Equal(ProjectedBy.Measured, session.How);
        Assert.Equal(0.40, session.Read, 6);
        Assert.Equal(0.55, session.Projected, 6);
        Assert.Equal(1_500_000, session.Spent);
        Assert.Equal(Now.AddMinutes(-10), session.Seen);
        // The week has no history of its own to weigh the turn since by: its reading alone, never a guess upward from nothing,
        // and said unweighed, so a start asks it.
        var week = projected.Of("weekly")!;
        Assert.Equal(ProjectedBy.Unweighed, week.How);
        Assert.Equal(0.25, week.Projected, 6);
        Assert.Equal(1_500_000, week.Spent);
        Assert.Equal(Now.AddMinutes(-10), projected.Oldest);
    }

    [Fact]
    public void With_nothing_to_weigh_its_turns_by_or_nothing_spent_since_the_projection_is_the_last_reading_alone()
    {
        var said = new AccountSaid([Said("session", 0.40, Now.AddHours(3), 10)]);

        // Too little history to weigh what was spent since: the reading alone, said unweighed.
        var none = UsageProjection.Of(said, [], [Turn(0.1, 9_000_000)], Now)!.Of("session")!;
        Assert.Equal((ProjectedBy.Unweighed, 0.40), (none.How, none.Projected));

        // A turn since that nothing weighs (a report of unknown meaning, or no counts), beside a ratio that stands.
        var gone = Now.AddHours(-20);
        ReadPoint[] measured = [Read("session", 0.10, gone, 24), Read("session", 0.30, gone, 22)];
        var unknown = UsageProjection.Of(said, measured, [Turn(23, 2_000_000), new SpentTurn(Now.AddMinutes(-1), null, 1)], Now)!.Of("session")!;
        Assert.Equal((ProjectedBy.Unweighed, 0.40), (unknown.How, unknown.Projected));

        var idle = UsageProjection.Of(said, measured, [Turn(23, 2_000_000)], Now)!.Of("session")!;
        Assert.Equal((ProjectedBy.Reading, 0.40), (idle.How, idle.Projected));

        Assert.Null(UsageProjection.Of(null, [], [], Now));
        Assert.Null(UsageProjection.Of(new AccountSaid([new WindowSaid("session", null, Now.AddHours(3), "clear", false, Now, null)]), [], [], Now));
    }

    [Fact]
    public void A_projection_is_never_more_than_the_whole_window()
    {
        var gone = Now.AddHours(-20);
        var said = new AccountSaid([Said("session", 0.80, Now.AddHours(3), 10)]);

        var session = UsageProjection.Of(said, [Read("session", 0.10, gone, 24), Read("session", 0.30, gone, 22)], [Turn(23, 2_000_000), Turn(0.1, 50_000_000)], Now)!.Of("session")!;

        Assert.Equal(1.0, session.Projected, 6);
    }

    // ——— When a start asks.

    [Fact]
    public void A_start_owes_a_question_only_at_its_set_points()
    {
        var use = RotationUse.Default;
        var reset = Now.AddHours(3);
        AccountSaid Account(params WindowSaid[] windows) => new(windows);
        AccountProjection? Project(AccountSaid said) => UsageProjection.Of(said, [], [], Now);
        ProbeWhy? Owed(AccountSaid? said, DateTimeOffset? limit = null) =>
            UsageProjection.Owed(said, said is null ? null : Project(said), use, Now, limit);

        // Fresh enough and far from its curve: nothing asked, older than either reader's floor as it is.
        Assert.Null(Owed(Account(Said("session", 0.40, reset, 10))));
        // Never read, or read with no number: unknown, asked.
        Assert.Equal(ProbeWhy.Unread, Owed(null));
        Assert.Equal(ProbeWhy.Unread, Owed(Account(new WindowSaid("session", null, reset, "clear", false, Now.AddMinutes(-10), null))));
        // No reading for the interval.
        Assert.Null(Owed(Account(Said("session", 0.40, reset, UsageProjection.Interval.TotalMinutes - 1))));
        Assert.Equal(ProbeWhy.Stale, Owed(Account(Said("session", 0.40, reset, UsageProjection.Interval.TotalMinutes))));
        // Within the margin of its curve (the smart session's 90): 81 is not, 80 is not, 81 is.
        Assert.Null(Owed(Account(Said("session", 0.80, reset, 10))));
        Assert.Equal(ProbeWhy.Near, Owed(Account(Said("session", 0.81, reset, 10))));
        Assert.Equal(ProbeWhy.Near, Owed(Account(Said("session", 0.95, reset, 10))));
        // A limit's word or a credit's on its reading, and a limit's sentence after its reading.
        Assert.Equal(ProbeWhy.Limit, Owed(Account(Said("session", 0.40, reset, 10, standing: AccountReadings.RefusedWord))));
        Assert.Equal(ProbeWhy.Credits, Owed(Account(Said("session", 0.40, reset, 10, credits: true))));
        Assert.Equal(ProbeWhy.Limit, Owed(Account(Said("session", 0.40, reset, 10)), limit: Now.AddMinutes(-5)));
        Assert.Null(Owed(Account(Said("session", 0.40, reset, 10)), limit: Now.AddMinutes(-15)));
        // Daoris's own turns since its reading that nothing weighs: no projection can show it is far from its curve.
        var spent = Account(Said("session", 0.40, reset, 10));
        Assert.Equal(ProbeWhy.Unweighed, UsageProjection.Owed(spent, UsageProjection.Of(spent, [], [Turn(0.1, 1000)], Now), use, Now));
        Assert.Equal(ProbeWhy.Unweighed, UsageProjection.Owed(spent, UsageProjection.Of(spent, [], [new SpentTurn(Now.AddMinutes(-1), null)], Now), use, Now));
    }

    [Fact]
    public void A_run_with_a_turn_nothing_weighs_is_no_evidence_for_the_ratio()
    {
        var (first, second) = (Now.AddHours(-20), Now.AddHours(-2));
        ReadPoint[] readings =
        [
            Read("session", 0.10, first, 24), Read("session", 0.20, first, 22),
            Read("session", 0.01, second, 6), Read("session", 0.07, second, 4),
        ];
        // The first run's rise is part a turn of unknown size: it is passed, never divided by the part that is known.
        SpentTurn[] turns = [Turn(23, 100_000), new(Now.AddHours(-23), null, 1), Turn(5, 300_000)];

        Assert.Equal(0.06 / 300_000, UsageProjection.Ratio(readings, turns, "session", Now)!.Value, 12);
    }

    [Fact]
    public void A_projection_over_its_curve_asks_and_never_makes_the_account_near()
    {
        var use = RotationUse.Default;
        var gone = Now.AddHours(-20);
        var said = new AccountSaid([Said("session", 0.50, Now.AddHours(3), 10)]);
        var projected = UsageProjection.Of(said, [Read("session", 0.10, gone, 24), Read("session", 0.30, gone, 22)], [Turn(23, 2_000_000), Turn(0.1, 5_000_000)], Now)!;

        Assert.Equal(1.0, projected.Of("session")!.Projected, 6);
        Assert.Equal(ProbeWhy.Near, UsageProjection.Owed(said, projected, use, Now));
        // Near and over are the maker's reading's alone (D130 point 9).
        Assert.False(AccountReadings.Near(said, use, Now));
        Assert.Equal(-10, UsageProjection.Room(projected, use, Now)!.Value, 6);
    }

    // ——— From a recorded history.

    [Fact]
    public void The_ratio_read_from_a_kept_history_projects_within_its_range()
    {
        // A week of one account's history in USAGECAP1a2's shape: frames at whole hundredths and reads at whole percents of the
        // five-hour window, each run inside one reset, Daoris's turns between, and a burst of the person's own use. The account's
        // true cost is 1e-7 of its window per reference-equivalent token; the person's use only raises what is read.
        const double True = 1e-7;
        var binding = new TurnBinding("claude-code", "account-1", "claude-code", UsageHistory.PerTurn);
        var seq = 0;
        for (var day = 6; day >= 1; day--)
        {
            var start = Now.AddDays(-day);
            var reset = start.AddHours(5);
            var used = 0.0;
            for (var hour = 0; hour < 4; hour++)
            {
                var at = start.AddHours(hour);
                Keep(at, reset, Math.Round(used, 2));
                // 100,000 input, 20,000 output, 500,000 cache reads, 40,000 cache writes: 300,000 reference-equivalent.
                var ended = at.AddMinutes(30);
                UsageHistory.Turned(_home, binding, new TurnSeen("3f9c2a71", ++seq, at.AddMinutes(5), ended, new TurnTokens(100_000, 20_000, 500_000, 40_000), null, false), ended);
                used += True * 300_000;
                // The person's own use, which Daoris never sees, on the second hour of each day.
                if (hour == 1) used += 0.01;
            }

            Keep(start.AddHours(4), reset, Math.Round(used, 2));
        }

        // A turn of an account no start bound, and another account's, are no spend of this one's.
        UsageHistory.Turned(_home, null, new TurnSeen("9b1e7c20", 1, null, Now.AddMinutes(-3), new TurnTokens(9_000_000, 0, 0, 0), null, false), Now);
        UsageHistory.Turned(_home, binding with { Account = "account-2" }, new TurnSeen("9b1e7c21", 1, null, Now.AddMinutes(-3), new TurnTokens(9_000_000, 0, 0, 0), null, false), Now);
        // This account's turns since its last reading.
        UsageHistory.Turned(_home, binding, new TurnSeen("3f9c2a72", 1, null, Now.AddMinutes(-4), new TurnTokens(100_000, 20_000, 500_000, 40_000), null, false), Now);
        UsageHistory.Turned(_home, binding, new TurnSeen("3f9c2a72", 2, null, Now.AddMinutes(-2), new TurnTokens(100_000, 20_000, 500_000, 40_000), null, false), Now);

        var (readings, turns) = new UsageLedger(_home).Of("claude-code", "account-1", Now);

        var ratio = UsageProjection.Ratio(readings, turns, "session", Now)!.Value;
        // Never below the truth, since what Daoris never sees only adds; and the burst is a third of the rise at most.
        Assert.InRange(ratio, True * 0.95, True * 1.5);

        var said = new AccountSaid([Said("session", 0.20, Now.AddHours(4), 5)]);
        var session = UsageProjection.Of(said, readings, turns, Now)!.Of("session")!;
        Assert.Equal(ProjectedBy.Measured, session.How);
        Assert.Equal(600_000, session.Spent);
        Assert.InRange(session.Projected, 0.20 + True * 600_000 * 0.95, 0.20 + True * 600_000 * 1.5);

        void Keep(DateTimeOffset at, DateTimeOffset reset, double used) =>
            UsageHistory.Kept(
                _home, new ReadingFrom("claude-code", "account-1", "claude-code", UsageHistory.SourceFrame, AccountReadings.HistoryReader, at),
                [new WindowSeen("five_hour", "session", null, 300, used, 0.01, reset, UsageHistory.ResetSaid)], at);
    }

    [Fact]
    public void The_ledger_reads_on_as_the_history_grows_and_keeps_only_its_horizon()
    {
        var binding = new TurnBinding("claude-code", "account-1", "claude-code", UsageHistory.PerTurn);
        var ledger = new UsageLedger(_home);
        Assert.Empty(ledger.Of("claude-code", "account-1", Now).Turns);

        UsageHistory.Turned(_home, binding, new TurnSeen("3f9c2a71", 1, null, Now.AddMinutes(-2), new TurnTokens(1000, null, null, null), null, false), Now);
        Assert.Equal([1000.0], ledger.Of("claude-code", "account-1", Now).Turns.Select(turn => turn.Work));

        UsageHistory.Turned(_home, binding, new TurnSeen("3f9c2a71", 2, null, Now.AddMinutes(-1), new TurnTokens(2000, null, null, null), null, false), Now);
        // A turn whose report's meaning is not known (D179 point 8), and one that said no count, are held as spend nothing weighs.
        UsageHistory.Turned(_home, binding with { Meaning = UsageHistory.MeaningUnknown }, new TurnSeen("3f9c2a71", 3, null, Now.AddMinutes(-1), new TurnTokens(5000, null, null, null), null, false), Now);
        UsageHistory.Turned(_home, binding, new TurnSeen("3f9c2a71", 4, null, Now.AddMinutes(-1), null, null, true), Now);
        Assert.Equal(new double?[] { 1000, 2000, null, null }, ledger.Of("claude-code", "account-1", Now).Turns.Select(turn => turn.Work));

        // Past its horizon, a turn is no longer held.
        Assert.Empty(ledger.Of("claude-code", "account-1", Now + UsageProjection.Horizon + TimeSpan.FromMinutes(1)).Turns);
    }
}
