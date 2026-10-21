using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEP1a's foundations (D195 points 1, 2 and 4; the cleanup design §2.1, §2.3 and §3.1): the kinds' table and the one effective policy
/// every reader of the set-aside days reads, <c>keeping.json</c> written only through the keep lock and keeping what another writer
/// wrote, and the run lock tried and never waited for, its holder named. Files only, in a home under <c>_fixtures/</c>; the holder a
/// real ended process left is <see cref="KeepingSlimTests"/>'.
/// </summary>
public sealed class KeepingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "keeping", Guid.NewGuid().ToString("N")[..8]);

    public KeepingTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static KeepingLast Last(int removed = 2, long bytes = 4096, string? kept = null) =>
        new(Now, KeepingRun.Look, "run-1", removed, bytes, kept is null ? new Dictionary<string, int>() : new Dictionary<string, int> { [kept] = 1 });

    /// <summary>
    /// §2.1: the set-aside kind in the table, its days fourteen, its package folders an hour after the move, the person's days within 1
    /// to 90 and <c>whole</c> theirs to set; and the one effective policy, which every reader of the days reads, still the default.
    /// </summary>
    [Fact]
    public void The_set_aside_kind_and_its_one_policy_keep_fourteen_days_and_an_hour()
    {
        var kind = KeepKinds.SetAside;
        Assert.Same(kind, KeepKinds.Named("set-aside"));
        Assert.Equal(["set-aside"], KeepKinds.Table.Select(each => each.Id));
        Assert.Equal((TimeSpan.FromDays(14), TimeSpan.FromHours(1), 1, 90), (kind.Days, KeepKinds.SetAsideSettle, kind.MinDays, kind.MaxDays));
        Assert.Equal([HomeEntry.Trees.Name], kind.Entries);
        Assert.Equal(new SetAsidePolicy(TimeSpan.FromDays(14), Whole: false), KeepPolicy.Of(_home).SetAside);
    }

    /// <summary>§2.3: the pass's last run of a kind, written under the keep lock as <c>last.&lt;kind&gt;</c>, the record's shape.</summary>
    [Fact]
    public void A_kind_s_last_run_is_written_as_the_record_says()
    {
        Assert.Null(KeepingRecord.WriteLast(_home, KeepingRecord.Read(_home), KeepKinds.SetAside.Id, Last(kept: SetAsideKept.Written)));

        using var written = JsonDocument.Parse(File.ReadAllText(KeepingRecord.PathOf(_home)));
        var last = written.RootElement.GetProperty("last").GetProperty("set-aside");
        Assert.Equal(1, written.RootElement.GetProperty("schema").GetInt32());
        Assert.Equal(("2026-10-21T09:00:00.000Z", "look", "run-1"), (last.GetProperty("at").GetString(), last.GetProperty("door").GetString(), last.GetProperty("run").GetString()));
        Assert.Equal((2, 4096L), (last.GetProperty("removed").GetProperty("count").GetInt32(), last.GetProperty("removed").GetProperty("bytes").GetInt64()));
        Assert.Equal(1, last.GetProperty("kept").GetProperty("written").GetInt32());
        var read = KeepingRecord.Read(_home);
        Assert.Null(read.Problem);
        Assert.Equal(Last(kept: SetAsideKept.Written) with { Kept = read.Last["set-aside"].Kept }, read.Last["set-aside"]);
        Assert.Equal(1, read.Last["set-aside"].Kept[SetAsideKept.Written]);
    }

    /// <summary>
    /// 🔴 §2.3 (Codex 4.1): another writer's change landing between this writer's read and its write is kept, its kinds, its first-unused
    /// times and a key this build has no field for; only <c>last.set-aside</c> is this edit's. An atomic rename alone lost it (CONFIGLOCK1).
    /// </summary>
    [Fact]
    public void Another_writer_s_change_between_the_read_and_the_write_keeps_both()
    {
        var read = KeepingRecord.Read(_home);
        File.WriteAllText(KeepingRecord.PathOf(_home),
            """{ "schema": 1, "last": { "downloads": { "at": "2026-10-20T09:00:00.000Z", "door": "look" } }, "unused": { "agent/claude-code/1.0.0": "2026-10-19T09:00:00.000Z" }, "later": true }""" + "\n");

        Assert.Null(KeepingRecord.WriteLast(_home, read, KeepKinds.SetAside.Id, Last()));

        using var written = JsonDocument.Parse(File.ReadAllText(KeepingRecord.PathOf(_home)));
        var root = written.RootElement;
        Assert.Equal("look", root.GetProperty("last").GetProperty("downloads").GetProperty("door").GetString());
        Assert.Equal(2, root.GetProperty("last").GetProperty("set-aside").GetProperty("removed").GetProperty("count").GetInt32());
        Assert.Equal("2026-10-19T09:00:00.000Z", root.GetProperty("unused").GetProperty("agent/claude-code/1.0.0").GetString());
        Assert.True(root.GetProperty("later").GetBoolean());
        Assert.False(File.Exists(KeepLock.PathOf(KeepingRecord.PathOf(_home))));
    }

    /// <summary>§2.3: a record that does not read is never written over; the write says why and leaves the file as it was.</summary>
    [Fact]
    public void A_record_that_does_not_read_is_never_written_over()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(KeepingRecord.PathOf(_home))!);
        File.WriteAllText(KeepingRecord.PathOf(_home), "{ not json\n");

        var read = KeepingRecord.Read(_home);
        var refused = KeepingRecord.WriteLast(_home, read, KeepKinds.SetAside.Id, Last());

        Assert.NotNull(read.Problem);
        Assert.Contains("keeping.json", refused, StringComparison.Ordinal);
        Assert.Equal("{ not json\n", File.ReadAllText(KeepingRecord.PathOf(_home)));
    }

    /// <summary>
    /// 🔴 §3.1 (Fable F3.1): the run lock is tried, never waited for: a second taker meets it held at once and is told who holds it,
    /// by the holder's own file; once let go, the file is gone and the next takes it.
    /// </summary>
    [Fact]
    public void The_run_lock_is_tried_and_a_second_taker_is_told_its_holder()
    {
        using (var first = KeepingRun.TryTake(_home, KeepingRun.Look))
        {
            Assert.NotNull(first);
            var started = DateTime.UtcNow;
            Assert.Null(KeepingRun.TryTake(_home, KeepingRun.Terminal));
            Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
            var holder = KeepingRun.Holder(_home);
            Assert.NotNull(holder);
            Assert.Equal((Environment.ProcessId, KeepingRun.Look, false), (holder.Pid, holder.Door, holder.Gone));
            Assert.Contains($"process {Environment.ProcessId}", holder.Said, StringComparison.Ordinal);
        }

        Assert.Null(KeepingRun.Holder(_home));
        Assert.False(File.Exists(KeepingRun.HolderPathOf(_home)));
        using var again = KeepingRun.TryTake(_home, KeepingRun.Look);
        Assert.NotNull(again);
    }

    /// <summary>§3.1, §3.3 (Fable F3.3): a holder's file whose process is gone is a run a crash cut short, read so, never as one under way.</summary>
    [Fact]
    public void A_holder_s_file_whose_process_is_gone_reads_as_a_run_cut_short()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(KeepingRun.HolderPathOf(_home))!);
        File.WriteAllText(KeepingRun.HolderPathOf(_home), """{"pid":2147483647,"since":"2026-10-21T08:00:00.000Z","door":"terminal"}""" + "\n");

        var holder = KeepingRun.Holder(_home, lives: _ => false);

        Assert.NotNull(holder);
        Assert.Equal((int.MaxValue, KeepingRun.Terminal, true), (holder.Pid, holder.Door, holder.Gone));
        Assert.Contains("stopped before its end", holder.Said, StringComparison.Ordinal);
        using var taken = KeepingRun.TryTake(_home, KeepingRun.Look);
        Assert.NotNull(taken);
        Assert.Equal(KeepingRun.Look, KeepingRun.Holder(_home)!.Door);
    }
}
