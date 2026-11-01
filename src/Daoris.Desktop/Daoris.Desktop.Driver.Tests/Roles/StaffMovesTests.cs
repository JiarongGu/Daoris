using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STAFFPOLICY1e (D226; the staff policy design §4.2, §11 point 5): a line's moves to another choice are kept by the line's identity in
/// <c>role_moves</c>, so an in-start model or sign-in replacement, which writes no part link, counts beside a condition's move, a restart
/// keeps the count, and one approval at the bound releases one move.
/// </summary>
public sealed class StaffMovesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staff-moves", Guid.NewGuid().ToString("N")[..8]);

    private readonly MovedClock _clock = new();

    public StaffMovesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private StaffMoves Moves() => new(_home, () => StoreSettled.Yes, _clock);

    private static readonly StaffPolicy Most3 = new(StaffRules.Weight, [], new Dictionary<string, StaffMove> { [StaffConditions.Transient] = new(StaffActions.Next) }, 3);

    private static StaffAction Judged(StaffMoved moved, string condition = StaffConditions.Transient) =>
        StaffJudge.OnCondition(Most3, condition, new StaffLine(true, moved.Moves));

    [Fact]
    public void A_line_in_a_home_without_a_store_counts_nothing_and_reads_none_moving()
    {
        Assert.Equal(new StaffMoved(0, false), Moves().Of("42"));
        Assert.Empty(Moves().Moving());
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }

    [Fact]
    public void In_start_replacements_and_condition_moves_count_together_against_most_and_a_restart_keeps_them()
    {
        var moves = Moves();
        // Two replacements inside one start, which write no part link, and a condition's move after it.
        Assert.True(moves.Record("42", "build", "s1", StaffConditions.ModelRefused, StaffActions.Next, "choice 1"));
        Assert.True(moves.Record("42", "build", "s1", StaffConditions.SignedOut, StaffActions.Next, "choice 1"));
        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(moves.Record("42", "build", "s2", StaffConditions.Transient, StaffActions.Next, "choice 2"));
        Assert.True(moves.Record("43", "build", "s9", StaffConditions.Limit, StaffActions.Next, "choice 1"));

        // A new reader over the same file, as after a restart: the line has made three moves of the three it may, another line one.
        var restarted = Moves();
        Assert.Equal(3, restarted.Of("42").Moves);
        Assert.Equal(1, restarted.Of("43").Moves);
        Assert.Equal(StaffActions.Ask, Judged(restarted.Of("42")).Do);
        Assert.True(Judged(restarted.Of("42")).MovesBound);
        Assert.Equal(StaffActions.Next, Judged(restarted.Of("43")).Do);

        // Past 24 hours a move no longer counts.
        _clock.Advance(StaffMoves.Window + TimeSpan.FromMinutes(1));
        Assert.Equal(0, Moves().Of("42").Moves);
    }

    [Fact]
    public void One_approval_at_the_bound_releases_one_move_and_the_next_waits_for_a_new_yes()
    {
        var moves = Moves();
        foreach (var session in new[] { "s1", "s2", "s3" })
        {
            Assert.True(moves.Record("42", "build", session, StaffConditions.Transient, StaffActions.Next, "choice 1"));
        }

        Assert.True(Judged(moves.Of("42")).MovesBound);

        Assert.True(moves.Approve("42", "build"));
        var released = moves.Of("42");
        Assert.True(released.Approved);
        Assert.False(Judged(released).MovesBound);

        // The move the yes released is counted: at the bound again, the yes spent.
        Assert.True(moves.Decided("42", "build", "s4", StaffConditions.Transient, StaffActions.Next, "choice 1", moved => !Judged(moved).MovesBound));
        var after = moves.Of("42");
        Assert.False(after.Approved);
        Assert.True(Judged(after).MovesBound);
        Assert.False(moves.Decided("42", "build", "s5", StaffConditions.Transient, StaffActions.Next, "choice 1", moved => !Judged(moved).MovesBound));
    }

    [Fact]
    public void The_move_a_part_ended_on_is_what_the_planner_carries_the_line_on_by()
    {
        var moves = new StaffLedger(_home, () => StoreSettled.Yes, _clock).Moves;
        Assert.True(moves.Record("42", "build", "s1", StaffConditions.Transient, StaffActions.Next, "choice 1"));

        var moving = Assert.Single(Moves().Moving());
        Assert.Equal(("42", "s1", StaffConditions.Transient), (moving.Key, moving.Value.Session, moving.Value.Condition));
    }
}
