using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The kept slots' store: a build before its step never reads the tables, a migration interrupted leaves the store untouched, one claim
/// per slot and one per line, a visit and a claim racing for one slot with one refused, a dead run's claim taken over and a live one's
/// never, and two lookers reading one claim dead with only one taking it.
/// </summary>
/// <remarks>Each case has its own scratch home under <c>_fixtures/</c>; "another process" is a second pool under a run of its own, and a dead run is the liveness the case hands in.</remarks>
public sealed class TreePoolTests : IDisposable
{
    private const string Workspace = "w";

    private const string Repository = "engine";

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tree-pool", Guid.NewGuid().ToString("N")[..8]);

    public TreePoolTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private TreePool Pool(string run, Func<StoreRun, bool>? alive = null) => new(_home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private static Func<StoreRun, bool> DeadAre(params string[] runs) => run => !runs.Contains(run.Run);

    private static TreeClaimOpening Opening(int number, string kind, string line, string phase = TreeClaimPhases.Judged) =>
        new(Workspace, Repository, number, kind, "quest-1", line, $"daoris/{line}", "abc123", "abc123", $"daoris-pool-{number}") { Phase = phase };

    private static SlotSettled Settled(int number, string line = "", string tip = "abc123") =>
        new($"k-{number}", $"trees/k-{number}", line, tip, KeptStates.Kept, "", 100, "tc-1");

    private ClaimFence Made(TreePool pool, int number)
    {
        var claimed = pool.Claim(Opening(number, TreeClaimKinds.Make, "", TreeClaimPhases.Opening));
        Assert.NotNull(claimed.Fence);
        pool.Complete(claimed.Fence, Settled(number));
        return claimed.Fence;
    }

    private static LineEnd End(string line, int number, string kind, DateTimeOffset at) =>
        new(Workspace, Repository, line, $"daoris/{line}", "session-1", number, kind, "abc123", at, "", "tc-1", at);

    [Fact]
    public void A_slot_is_kept_whole_and_the_line_it_serves_is_found_by_where_is()
    {
        var pool = Pool("run-a");
        Made(pool, 1);
        var claim = pool.Claim(Opening(1, TreeClaimKinds.Take, "s-1111"));
        pool.Complete(claim.Fence!, Settled(1, "s-1111") with { Tip = "" });

        var slot = pool.WhereIs(Workspace, Repository, "s-1111")!;
        Assert.Equal((1, "k-1", "trees/k-1", "s-1111", "", KeptStates.Kept, 100L, "tc-1"),
            (slot.Number, slot.Registration, slot.Path, slot.Line, slot.Tip, slot.State, slot.Bytes, slot.Toolchain));
        Assert.Equal([1], pool.Slots(Workspace, Repository).Select(each => each.Number));
        Assert.Null(pool.WhereIs(Workspace, Repository, "s-2222"));
        Assert.Null(pool.WhereIs(Workspace, "other", "s-1111"));
    }

    [Fact]
    public void A_line_holds_one_slot_so_a_second_slot_naming_it_is_refused_when_the_claim_completes()
    {
        var pool = Pool("run-a");
        Made(pool, 1);
        Made(pool, 2);
        pool.Complete(pool.Claim(Opening(1, TreeClaimKinds.Take, "s-1111")).Fence!, Settled(1, "s-1111"));

        var second = pool.Claim(Opening(2, TreeClaimKinds.Take, "s-1111"));

        Assert.NotNull(second.Fence);
        Assert.Throws<TreePoolRefusedException>(() => pool.Complete(second.Fence, Settled(2, "s-1111")));
        Assert.Equal(1, pool.WhereIs(Workspace, Repository, "s-1111")!.Number);
        Assert.NotNull(pool.ClaimOn(Workspace, Repository, 2));
    }

    [Fact]
    public void A_build_before_the_step_never_reads_the_tables_and_a_store_this_build_made_is_written_on_by_it_untouched()
    {
        var at = DesktopStore.LocationIn(_home);
        using (var older = DesktopStore.Open(at, new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(TreePool.Since - 1)], Settled = () => StoreSettled.Yes }))
        {
            Assert.Equal(TreePool.Since - 1, older.Stamp.Version);
        }

        var reading = Pool("run-a");
        Assert.Empty(reading.Slots(Workspace, Repository));
        Assert.Null(reading.WhereIs(Workspace, Repository, "s-1111"));
        using (var inspect = new SqliteConnection($"Data Source={at};Pooling=False;Mode=ReadOnly"))
        {
            inspect.Open();
            using var tables = inspect.CreateCommand();
            tables.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('kept_trees', 'tree_claims', 'tree_visits', 'line_ends')";
            Assert.Equal(0L, Convert.ToInt64(tables.ExecuteScalar()));
        }

        Made(reading, 1);
        using (var older = DesktopStore.Open(at, new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(TreePool.Since - 1)], Settled = () => StoreSettled.Yes }))
        {
            Assert.Equal((DesktopStore.Steps.Count, DesktopStore.WriterStamp), older.Stamp);
            older.Write("older", write => write.Execute("UPDATE store_version SET at = at"));
        }

        Assert.Single(Pool("run-a").Slots(Workspace, Repository));
    }

    [Fact]
    public void The_step_makes_the_four_tables_with_every_column_required_and_leaves_the_writer_stamp()
    {
        var step = Assert.Single(DesktopStore.Steps, each => each.Version == TreePool.Since);
        Assert.Equal(DesktopStore.PolicyWriter, step.Writer);
        Assert.Equal(TreePool.Since + 1, TreePool.ExpectSince);

        using var store = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        foreach (var table in new[] { "kept_trees", "tree_claims", "tree_visits", "line_ends" })
        {
            var columns = store.Query($"SELECT name, \"notnull\" FROM pragma_table_info('{table}')", row => (row.GetString(0), row.GetInt64(1)));
            Assert.NotEmpty(columns);
            Assert.All(columns, column => Assert.True(column.Item2 == 1, $"{table}.{column.Item1} is not required"));
        }

        Assert.Contains("toolchain", store.Query("SELECT name FROM pragma_table_info('kept_trees')", row => row.GetString(0)));
        Assert.Contains("toolchain", store.Query("SELECT name FROM pragma_table_info('line_ends')", row => row.GetString(0)));
        Assert.Equal(DesktopStore.PolicyWriter, store.Stamp.Writer);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_migration_interrupted_at_the_step_leaves_the_store_as_it_was(int interrupted)
    {
        var at = DesktopStore.LocationIn(_home);
        IReadOnlyList<StoreMigration> Steps(bool fail) =>
        [
            .. DesktopStore.Steps.Select(step => step.Version == TreePool.Since && fail ? step with { Apply = write => { step.Apply(write); if (interrupted == 1) throw new IOException("killed"); } } : step),
            new(DesktopStore.Steps.Count + 1, DesktopStore.PolicyWriter, write =>
            {
                write.Execute("CREATE TABLE after_pool (x TEXT)");
                if (fail && interrupted == 2) throw new IOException("killed");
            }),
        ];

        Assert.Throws<IOException>(() => DesktopStore.Open(at, new DesktopStoreOptions { Steps = Steps(fail: true), Settled = () => StoreSettled.Yes }));

        using (var inspect = new SqliteConnection($"Data Source={at};Pooling=False"))
        {
            inspect.Open();
            using var tables = inspect.CreateCommand();
            tables.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            var names = new List<string>();
            using (var reader = tables.ExecuteReader()) while (reader.Read()) names.Add(reader.GetString(0));
            Assert.Equal(interrupted == 2, names.Contains("kept_trees"));
            Assert.DoesNotContain("after_pool", names);
            using var version = inspect.CreateCommand();
            version.CommandText = "SELECT version FROM store_version";
            Assert.Equal(interrupted == 2 ? (long)DesktopStore.Steps.Count : TreePool.Since - 1L, Convert.ToInt64(version.ExecuteScalar()));
        }

        using var again = DesktopStore.Open(at, new DesktopStoreOptions { Steps = Steps(fail: false), Settled = () => StoreSettled.Yes });
        Assert.Equal(DesktopStore.Steps.Count + 1, again.Stamp.Version);
    }

    [Fact]
    public void The_claims_record_of_where_the_slot_stood_is_the_last_step_and_a_claim_written_before_it_reads_as_detached_serving_no_line()
    {
        var step = Assert.Single(DesktopStore.Steps, each => each.Version == TreePool.ExpectSince);
        Assert.Equal(DesktopStore.PolicyWriter, step.Writer);
        Assert.Equal(TreePool.ExpectSince, DesktopStore.Steps.Count);

        using var store = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        var columns = store.Query("SELECT name, \"notnull\", dflt_value FROM pragma_table_info('tree_claims')", row => (row.GetString(0), row.GetInt64(1), row.IsDBNull(2) ? null : row.GetString(2)));
        Assert.Contains(("expect_branch", 1L, "''"), columns);
        Assert.Contains(("expect_line", 1L, "''"), columns);
    }

    [Fact]
    public void A_claim_records_the_branch_and_the_line_the_slot_stood_on_and_holds_that_line_while_it_waits_to_be_put_back()
    {
        var pool = Pool("run-a");
        Made(pool, 1);
        Made(pool, 2);
        var claimed = pool.Claim(Opening(1, TreeClaimKinds.Take, "s-2222") with { ExpectBranch = "daoris/s-1111", ExpectLine = "s-1111" });
        Assert.Equal(("daoris/s-1111", "s-1111"), (claimed.Claim!.ExpectBranch, claimed.Claim.ExpectLine));
        Assert.Null(pool.ClaimOf(Workspace, Repository, "s-1111"));

        pool.Undoing(claimed.Fence!);

        Assert.Equal(1, pool.ClaimOf(Workspace, Repository, "s-1111")?.Number);
        Assert.Contains("line", pool.Claim(Opening(2, TreeClaimKinds.Return, "s-1111")).Refused);
    }

    [Fact]
    public void One_claim_a_slot_and_one_a_line_from_the_shell_a_share_loop_and_a_verb_racing()
    {
        Made(Pool("run-a"), 1);
        Made(Pool("run-a"), 2);
        var shell = Pool("shell");
        var loop = Pool("loop");
        var verb = Pool("verb");

        var first = shell.Claim(Opening(1, TreeClaimKinds.Take, "s-1111"));
        Assert.NotNull(first.Fence);

        var sameSlot = loop.Claim(Opening(1, TreeClaimKinds.Take, "s-2222"));
        Assert.Null(sameSlot.Fence);
        Assert.Contains("slot", sameSlot.Refused);

        var sameLine = verb.Claim(Opening(2, TreeClaimKinds.Return, "s-1111"));
        Assert.Null(sameLine.Fence);
        Assert.Contains("line", sameLine.Refused);

        var other = verb.Claim(Opening(2, TreeClaimKinds.Take, "s-3333"));
        Assert.NotNull(other.Fence);
        Assert.Equal("s-1111", shell.ClaimOn(Workspace, Repository, 1)!.Line);
        Assert.Equal(2, shell.ClaimOf(Workspace, Repository, "s-3333")!.Number);
    }

    [Fact]
    public async Task Claims_racing_for_one_slot_leave_one_winner()
    {
        Made(Pool("run-a"), 1);

        var claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(each =>
            Task.Run(() => Pool($"run-{each}x").Claim(Opening(1, TreeClaimKinds.Take, $"s-{each}")))));

        Assert.Single(claims, each => each.Fence is not null);
    }

    [Fact]
    public void A_visit_and_a_claim_racing_for_one_slot_leave_one_refused()
    {
        var pool = Pool("run-a");
        Made(pool, 1);
        Made(pool, 2);

        Assert.Null(pool.Visit(Workspace, Repository, 1, SlotVisitKinds.Terminal, "terminal-1"));
        var switching = Pool("run-b").Claim(Opening(1, TreeClaimKinds.Take, "s-1111"));
        Assert.Null(switching.Fence);
        Assert.Contains("terminal", switching.Refused);

        // A claim that only goes on in place, and an end, go on beside the person's terminal.
        Assert.NotNull(Pool("run-b").Claim(Opening(1, TreeClaimKinds.End, "s-1111")).Fence);
        pool.LetGo(Workspace, Repository, 1, "terminal-1");

        var claim = Pool("run-b").Claim(Opening(2, TreeClaimKinds.Take, "s-2222"));
        Assert.NotNull(claim.Fence);
        var refused = pool.Visit(Workspace, Repository, 2, SlotVisitKinds.Terminal, "terminal-2");
        Assert.NotNull(refused);
        Assert.Empty(pool.Visits(Workspace, Repository, 2));
    }

    [Fact]
    public void A_take_claim_in_place_goes_on_beside_a_terminal_and_a_visit_by_a_dead_run_holds_nothing()
    {
        var pool = Pool("run-a");
        Made(pool, 1);
        Pool("run-gone").Visit(Workspace, Repository, 1, SlotVisitKinds.Served, "served-1");

        var second = Pool("run-b");
        var inPlace = second.Claim(Opening(1, TreeClaimKinds.Take, "s-1111") with { InPlace = true });
        Assert.NotNull(inPlace.Fence);
        second.Complete(inPlace.Fence, Settled(1, "s-1111"));

        var late = Pool("run-b", DeadAre("run-gone")).Claim(Opening(1, TreeClaimKinds.Finish, "s-1111"));
        Assert.NotNull(late.Fence);
        Assert.Empty(pool.Visits(Workspace, Repository, 1));
    }

    [Fact]
    public void A_claim_is_taken_over_only_from_a_dead_run_and_the_claim_it_lost_writes_nothing_more()
    {
        var first = Pool("run-a");
        Made(first, 1);
        var claim = first.Claim(Opening(1, TreeClaimKinds.Take, "s-1111")).Fence!;

        Assert.Contains("still runs", Pool("run-b").TakeOver(Workspace, Repository, 1, 1).Refused);
        Assert.Contains("own", Pool("run-a").TakeOver(Workspace, Repository, 1, 1).Refused);
        var taken = Pool("run-b", DeadAre("run-a")).TakeOver(Workspace, Repository, 1, 1);

        Assert.Equal(new ClaimFence(Workspace, Repository, 1, "run-b", 2), taken.Fence);
        Assert.Equal(("run-b", 2L), (taken.Claim!.ClaimRun, taken.Claim.ClaimGeneration));
        Assert.Throws<TreePoolRefusedException>(() => first.Advance(claim, TreeClaimPhases.Switched));
        Assert.Equal(TreeClaimPhases.Judged, first.ClaimOn(Workspace, Repository, 1)!.Phase);
        Assert.Contains("took it first", Pool("run-c", DeadAre("run-a", "run-b")).TakeOver(Workspace, Repository, 1, 1).Refused);
    }

    [Fact]
    public void A_paused_live_run_s_claim_is_never_taken_however_long_it_waits()
    {
        var first = Pool("run-a");
        Made(first, 1);
        var claim = first.Claim(Opening(1, TreeClaimKinds.Take, "s-1111")).Fence!;

        for (var each = 0; each < 3; each++) Assert.NotNull(Pool("run-b").TakeOver(Workspace, Repository, 1, 1).Refused);

        first.Advance(claim, TreeClaimPhases.Switched);
        Assert.Equal(TreeClaimPhases.Switched, first.ClaimOn(Workspace, Repository, 1)!.Phase);
    }

    [Fact]
    public async Task Two_lookers_reading_one_claim_dead_leave_one_taker()
    {
        var first = Pool("run-a");
        Made(first, 1);
        first.Claim(Opening(1, TreeClaimKinds.Take, "s-1111"));
        var dead = DeadAre("run-a");

        var takes = await Task.WhenAll(Enumerable.Range(0, 4).Select(each => Task.Run(() => Pool($"run-{each}x", dead).TakeOver(Workspace, Repository, 1, 1))));

        Assert.Single(takes, each => each.Fence is not null);
        Assert.Equal(2, Pool("run-a").ClaimOn(Workspace, Repository, 1)!.ClaimGeneration);
    }

    [Fact]
    public void A_claim_written_with_its_whole_intent_carries_the_open_s_key_and_the_record_it_made()
    {
        var pool = Pool("run-a");
        Made(pool, 1);
        var fence = pool.Claim(Opening(1, TreeClaimKinds.Take, "s-1111")).Fence!;

        pool.Advance(fence, TreeClaimPhases.OpeningRecord, opens: "open-key-1");
        pool.Advance(fence, TreeClaimPhases.Opened, record: "record-7");

        var claim = pool.ClaimOn(Workspace, Repository, 1)!;
        Assert.Equal(("take", "quest-1", "s-1111", "daoris/s-1111", "abc123", "abc123", "open-key-1", "daoris-pool-1", "record-7", TreeClaimPhases.Opened),
            (claim.Kind, claim.Holder, claim.Line, claim.Branch, claim.Start, claim.Expect, claim.Opens, claim.Job, claim.Record, claim.Phase));
    }

    [Fact]
    public void A_line_s_ends_are_kept_and_the_latest_is_the_tip_the_next_judgement_compares()
    {
        var pool = Pool("run-a");
        var early = new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);
        pool.RecordEnd(End("s-1111", 1, LineEndKinds.Output, early));
        pool.RecordEnd(End("s-1111", 2, LineEndKinds.NoOutput, early.AddHours(1)));

        var last = pool.LastEnd(Workspace, Repository, "s-1111")!;
        Assert.Equal((2, LineEndKinds.NoOutput, "tc-1"), (last.Number, last.Kind, last.Toolchain));
        Assert.Equal(2, pool.Ends(Workspace, Repository, "s-1111").Count);
        Assert.Null(pool.LastEnd(Workspace, Repository, "s-2222"));
    }

    [Fact]
    public void A_home_with_no_store_reads_as_holding_no_slot_and_is_left_without_one()
    {
        var pool = Pool("run-a");

        Assert.Empty(pool.Slots(Workspace, Repository));
        Assert.Null(pool.WhereIs(Workspace, Repository, "s-1111"));
        Assert.Null(pool.LastEnd(Workspace, Repository, "s-1111"));
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }
}
