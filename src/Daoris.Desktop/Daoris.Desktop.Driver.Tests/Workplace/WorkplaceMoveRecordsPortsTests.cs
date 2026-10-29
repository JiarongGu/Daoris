using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i part 1, the delete's port to the move records: Workplace declares the move records' facet and factory, and the Course
/// implements and installs them, so a record's delete takes its moves without the workplace naming a Course type. The installed store's own
/// cases over a real store are <see cref="MoveStoreTests"/>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class WorkplaceMoveRecordsPortsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-move-ports", Guid.NewGuid().ToString("N")[..8]);

    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);

    public WorkplaceMoveRecordsPortsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Remove(_home);
    }

    [Fact]
    public void The_installed_factory_is_the_course_s_and_opens_the_move_store_over_the_home_making_no_store()
    {
        Assert.IsType<WorkplaceMoveRecordStoreOperations>(WorkplaceMoveRecords.Current);
        var opened = WorkplaceMoveRecords.Current.Open(_home);
        Assert.IsType<MoveStore>(opened);
        Assert.NotSame(opened, WorkplaceMoveRecords.Current.Open(_home));
        Assert.Equal(DesktopStore.LocationIn(_home), opened.StoreLocation);
        Assert.Empty(opened.MovesOf(["s1"]));
        Assert.Empty(opened.ForgetMoves(["s1"]));
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }

    [Fact]
    public void Deletion_opens_the_supplied_factory_once_after_the_files_and_the_bindings_names_the_moves_and_ignores_unknown_ids()
    {
        var actual = WorkplaceMoveRecords.Current;
        var moves = new Moves { Forgets = ["s1"] };
        Directory.CreateDirectory(Sessions);
        File.WriteAllText(Path.Combine(Sessions, "s1.events.jsonl"), "abc");
        try
        {
            WorkplaceMoveRecords.Current = new Stores(moves);
            var removed = new SessionHomeFiles(_home).Remove(["s1", "s2", "laptop/s3"], null);

            Assert.Equal([$"open:{_home}", "forget:s1,s2"], moves.Calls);
            Assert.Equal(["conversation", SessionHomeFiles.HandOver], removed.Went["s1"]);
            Assert.Empty(removed.Went["s2"]);
            Assert.Empty(removed.Failed);
        }
        finally { WorkplaceMoveRecords.Current = actual; }
    }

    [Theory]
    [InlineData("store")]
    [InlineData("sqlite")]
    [InlineData("io")]
    [InlineData("access")]
    public void A_refused_forget_is_a_failure_at_the_store_and_the_held_moves_stay_under_the_hand_over_name(string failure)
    {
        var actual = WorkplaceMoveRecords.Current;
        var moves = new Moves { ForgetFails = Failure(failure), Holds = ["s2"] };
        try
        {
            WorkplaceMoveRecords.Current = new Stores(moves);
            var removed = new SessionHomeFiles(_home).Remove(["s1", "s2"], null);

            Assert.Equal([$"open:{_home}", "forget:s1,s2", "holding:s1,s2"], moves.Calls);
            Assert.Equal(["moves-here"], removed.Failed);
            Assert.Equal([SessionHomeFiles.HandOver], removed.Stayed["s2"]);
            Assert.Empty(removed.Stayed["s1"]);
        }
        finally { WorkplaceMoveRecords.Current = actual; }
    }

    [Fact]
    public void A_refused_forget_whose_held_read_is_refused_too_holds_every_session_and_other_failures_propagate_unchanged()
    {
        var actual = WorkplaceMoveRecords.Current;
        try
        {
            WorkplaceMoveRecords.Current = new Stores(new Moves { ForgetFails = Failure("store"), HoldingFails = Failure("io") });
            var removed = new SessionHomeFiles(_home).Remove(["s1", "s2"], null);
            Assert.Equal(["moves-here"], removed.Failed);
            Assert.Equal([SessionHomeFiles.HandOver], removed.Stayed["s1"]);
            Assert.Equal([SessionHomeFiles.HandOver], removed.Stayed["s2"]);

            var other = new InvalidOperationException("not a store's");
            WorkplaceMoveRecords.Current = new Stores(new Moves { ForgetFails = other });
            Assert.Same(other, Assert.Throws<InvalidOperationException>(() => new SessionHomeFiles(_home).Remove(["s1"], null)));
            var unread = new InvalidOperationException("not a read's");
            WorkplaceMoveRecords.Current = new Stores(new Moves { ForgetFails = Failure("store"), HoldingFails = unread });
            Assert.Same(unread, Assert.Throws<InvalidOperationException>(() => new SessionHomeFiles(_home).Remove(["s1"], null)));
        }
        finally { WorkplaceMoveRecords.Current = actual; }
    }

    [Fact]
    public void A_missing_factory_is_required_only_where_deletion_reaches_the_moves()
    {
        var actual = WorkplaceMoveRecords.Current;
        try
        {
            WorkplaceMoveRecords.Current = null!;

            Assert.Empty(new SessionHomeFiles(_home).Remove([], null).Went);
            Assert.Empty(new SessionHomeFiles(_home).Remove(["laptop/s1"], null).Went["laptop/s1"]);
            var missing = Assert.Throws<InvalidOperationException>(() => new SessionHomeFiles(_home).Remove(["s1"], null));
            Assert.Contains("the Course installs it", missing.Message);
        }
        finally { WorkplaceMoveRecords.Current = actual; }
    }

    private static Exception Failure(string kind) => kind switch
    {
        "store" => new DesktopStoreException("busy"),
        "sqlite" => new SqliteException("busy", 5),
        "io" => new IOException("held"),
        _ => new UnauthorizedAccessException("held"),
    };

    private sealed class Stores(Moves moves) : IWorkplaceMoveRecordStores
    {
        public IWorkplaceMoveRecords Open(string home)
        {
            moves.Calls.Add("open:" + home);
            return moves;
        }
    }

    private sealed class Moves : IWorkplaceMoveRecords
    {
        public List<string> Calls { get; } = [];
        public string[] Forgets { get; init; } = [];
        public string[] Holds { get; init; } = [];
        public Exception? ForgetFails { get; init; }
        public Exception? HoldingFails { get; init; }

        public string StoreLocation => "moves-here";

        public IReadOnlyList<string> ForgetMoves(IReadOnlyCollection<string> sessions)
        {
            Calls.Add("forget:" + string.Join(',', sessions));
            return ForgetFails is null ? Forgets : throw ForgetFails;
        }

        public IReadOnlyList<string> MovesOf(IReadOnlyCollection<string> sessions)
        {
            Calls.Add("holding:" + string.Join(',', sessions));
            return HoldingFails is null ? Holds : throw HoldingFails;
        }
    }
}
