using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A role's last seven days as Ask Daoris's room says them, its new starts by choice
/// from <c>role_starts</c> and its moves by condition from <c>role_moves</c>, read from the store and nothing else.
/// </summary>
public sealed class StaffWeekTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staff-week", Guid.NewGuid().ToString("N")[..8]);

    private readonly MovedClock _clock = new();

    public StaffWeekTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static readonly StaffKey Sonnet = new("claude-code", "sonnet", "high");

    private static readonly StaffKey Opus = new("claude-code", "opus", "high");

    [Fact]
    public void A_home_with_no_store_has_no_week_and_creates_none()
    {
        Assert.Empty(StaffWeek.Read(_home, _clock));
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }

    [Fact]
    public void A_role_s_starts_by_choice_and_moves_by_condition_count_for_seven_days_and_no_longer()
    {
        var ledger = new StaffLedger(_home, () => StoreSettled.Yes, _clock);
        var moves = new StaffMoves(_home, () => StoreSettled.Yes, _clock);
        Assert.True(ledger.Record(new StaffStart("old", "build", Sonnet, "weight")));
        Assert.True(moves.Record("1", "build", "old", StaffConditions.Limit, StaffActions.Next, "choice 1"));

        _clock.Advance(TimeSpan.FromDays(6));
        Assert.True(ledger.Record(new StaffStart("s1", "build", Sonnet, "weight")));
        Assert.True(ledger.Record(new StaffStart("s2", "build", Sonnet, "weight")));
        Assert.True(ledger.Record(new StaffStart("s3", "build", Opus, "weight", StaffConditions.Limit)));
        Assert.True(ledger.Record(new StaffStart("c1", "chat", Sonnet, "order")));
        Assert.True(moves.Record("2", "build", "s2", StaffConditions.Limit, StaffActions.Next, "choice 1"));
        Assert.True(moves.Record("3", "build", "s3", StaffConditions.Transient, StaffActions.Weighted, "choice 1"));
        Assert.True(moves.Approve("3", "build"));

        _clock.Advance(TimeSpan.FromDays(2));
        var week = StaffWeek.Read(_home, _clock);

        // The first start and move are 8 days old; the approval is no move.
        var build = week["build"];
        Assert.Equal(3, build.Starts);
        Assert.Equal([(Sonnet, 2), (Opus, 1)], build.ByChoice);
        Assert.Equal([(StaffConditions.Limit, 1), (StaffConditions.Transient, 1)], build.Moved);
        Assert.Equal(1, week["chat"].Starts);
        Assert.Empty(week["chat"].Moved);
    }

    [Fact]
    public void A_week_says_itself_in_the_designs_words()
    {
        var week = new StaffWeekRole(14, [(Sonnet, 10), (Opus, 4)], [(StaffConditions.Limit, 1), (StaffConditions.Transient, 1)]);

        Assert.Equal("last 7 days: 14 starts (claude-code sonnet high ×10, claude-code opus high ×4), moved: limit ×1, transient ×1", week.Said);
        Assert.Equal("last 7 days: no new start", new StaffWeekRole(0, [], []).Said);
        Assert.Equal("last 7 days: 1 start (claude-code sonnet high ×1)", new StaffWeekRole(1, [(Sonnet, 1)], []).Said);
    }
}
